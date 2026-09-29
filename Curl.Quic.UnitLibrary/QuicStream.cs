namespace Curl.Quic;

/// <summary>
/// One QUIC stream of a <see cref="QuicStreamSet" /> (RFC 9000 sections 2 and 3), with no
/// I/O. Its receiving part reassembles STREAM frames that arrive out of order or more than
/// once into the bytes <see cref="Read" /> hands out in order, checks the final size, and
/// holds the peer's RESET_STREAM; its sending part queues what <see cref="Write" /> is given
/// until the stream's and the connection's flow control let it go in STREAM frames, and
/// answers the peer's STOP_SENDING with RESET_STREAM. A bidirectional stream has both parts,
/// a unidirectional one only the part its opener's direction needs.
/// </summary>
public sealed class QuicStream
{
    private readonly QuicStreamSet owner;

    private readonly QuicSendCredit? sendCredit;

    private readonly QuicReceiveCredit? receiveCredit;

    private readonly SortedList<ulong, byte[]> outOfOrder = [];

    private readonly Queue<byte[]> readable = new();

    private readonly Queue<byte[]> unsent = new();

    private int readableHeadOffset;

    private int unsentHeadOffset;

    private ulong nextReceiveOffset;

    private ulong? finalSize;

    private bool finQueued;

    internal QuicStream(QuicStreamSet owner, ulong id, QuicSendCredit? sendCredit, QuicReceiveCredit? receiveCredit)
    {
        this.owner = owner;
        Id = id;
        this.sendCredit = sendCredit;
        this.receiveCredit = receiveCredit;
    }

    /// <summary>Gets the stream ID: its two low bits say who opened it and whether it is unidirectional (RFC 9000 section 2.1).</summary>
    public ulong Id { get; }

    /// <summary>Gets a value indicating whether the client opened the stream.</summary>
    public bool IsClientInitiated => (Id & 1) == 0;

    /// <summary>Gets a value indicating whether the stream carries bytes one way only.</summary>
    public bool IsUnidirectional => (Id & 2) != 0;

    /// <summary>Gets a value indicating whether the stream has a receiving part: it is bidirectional, or the server opened it.</summary>
    public bool CanRead => receiveCredit is not null;

    /// <summary>Gets a value indicating whether the stream has a sending part: it is bidirectional, or the client opened it.</summary>
    public bool CanWrite => sendCredit is not null;

    /// <summary>Gets how many bytes <see cref="Read" /> can hand out now.</summary>
    public int ReadableLength { get; private set; }

    /// <summary>Gets a value indicating whether every byte up to the peer's FIN has been read, or the peer reset the stream.</summary>
    public bool IsReadComplete => finalSize is { } size && receiveCredit!.Consumed == size;

    /// <summary>Gets the application error code of the peer's RESET_STREAM, or <see langword="null" /> while the peer has not reset the stream.</summary>
    public ulong? PeerResetErrorCode { get; private set; }

    /// <summary>Gets the application error code of the peer's STOP_SENDING, or <see langword="null" /> while the peer has not asked the client to stop.</summary>
    public ulong? PeerStopSendingErrorCode { get; private set; }

    /// <summary>Gets a value indicating whether nothing more may be written: FIN is queued or the sending part is reset.</summary>
    public bool IsWriteEnded => finQueued || IsSendReset;

    /// <summary>Gets a value indicating whether the client has reset the sending part, by <see cref="Abort" /> or in answer to STOP_SENDING.</summary>
    public bool IsSendReset { get; private set; }

    /// <summary>Gets a value indicating whether the client has asked the peer to stop sending, by <see cref="Abort" />; what arrives after is discarded.</summary>
    public bool IsReadAbandoned { get; private set; }

    /// <summary>Gets the offset of the next byte the client sends, which is also how many it has sent.</summary>
    public ulong SentOffset { get; private set; }

    /// <summary>Gets how many written bytes wait for flow control to let them go.</summary>
    public long UnsentLength { get; private set; }

    /// <summary>Gets a value indicating whether FIN has gone out in a STREAM frame.</summary>
    internal bool IsFinSent { get; private set; }

    /// <summary>Gets a value indicating whether both parts are done: all read or reset, and FIN sent or reset (RFC 9000 section 3).</summary>
    internal bool IsClosed => (!CanRead || IsReadComplete) && (!CanWrite || IsFinSent || IsSendReset);

    /// <summary>Gets a value indicating whether the stream has been counted as closed, towards MAX_STREAMS.</summary>
    internal bool IsReleased { get; set; }

    /// <summary>Gets a value indicating whether a MAX_STREAM_DATA frame is due: the peer may still send, and less than half the window is left.</summary>
    internal bool IsLimitRaiseDue => finalSize is null && PeerResetErrorCode is null && receiveCredit is { IsRaiseDue: true };

    /// <summary>Copies bytes the peer sent, in order, into <paramref name="destination" />, and credits them back to flow control.</summary>
    /// <param name="destination">Where the bytes go.</param>
    /// <returns>How many bytes were copied; zero when none are ready, at the end, or after the peer's RESET_STREAM.</returns>
    /// <exception cref="InvalidOperationException">The stream has no receiving part.</exception>
    public int Read(Span<byte> destination)
    {
        if (!CanRead)
        {
            throw new InvalidOperationException($"QUIC stream {Id} is unidirectional from the client; it cannot be read.");
        }

        var copied = 0;
        while (copied < destination.Length && readable.TryPeek(out var head))
        {
            var length = Math.Min(destination.Length - copied, head.Length - readableHeadOffset);
            head.AsSpan(readableHeadOffset, length).CopyTo(destination[copied..]);
            copied += length;
            readableHeadOffset += length;
            if (readableHeadOffset == head.Length)
            {
                readable.Dequeue();
                readableHeadOffset = 0;
            }
        }

        ReadableLength -= copied;
        owner.OnConsumed(this, (ulong)copied);
        return copied;
    }

    /// <summary>Queues bytes to send, and FIN after them when <paramref name="endStream" /> is set; flow control decides when they go.</summary>
    /// <param name="bytes">The bytes; may be empty.</param>
    /// <param name="endStream">Whether the stream ends after them.</param>
    /// <exception cref="InvalidOperationException">The stream has no sending part, or it has ended or been reset.</exception>
    public void Write(ReadOnlySpan<byte> bytes, bool endStream)
    {
        if (!CanWrite || IsWriteEnded)
        {
            throw new InvalidOperationException($"QUIC stream {Id} cannot be written: it has no sending part, has ended or has been reset.");
        }

        if (!bytes.IsEmpty)
        {
            unsent.Enqueue(bytes.ToArray());
            UnsentLength += bytes.Length;
        }

        finQueued = endStream;
        owner.OnWritten();
    }

    /// <summary>Abandons the stream both ways: RESET_STREAM for the sending part unless it is reset already, STOP_SENDING for the receiving part unless it is complete or already abandoned.</summary>
    /// <param name="applicationErrorCode">The application's error code both frames carry.</param>
    public void Abort(ulong applicationErrorCode)
    {
        if (CanWrite)
        {
            ResetSending(applicationErrorCode);
        }

        if (CanRead && !IsReadComplete && !IsReadAbandoned)
        {
            IsReadAbandoned = true;
            owner.QueueControlFrame(new QuicStopSendingFrame(Id, applicationErrorCode));
            DiscardReadable();
        }

        owner.OnStateChanged(this);
    }

    /// <summary>Takes a STREAM frame the peer sent; returns by how much it moves the highest offset received, for connection flow control.</summary>
    internal ulong ReceiveData(QuicStreamFrame frame)
    {
        var end = frame.Offset + (ulong)frame.Data.Length;
        CheckFinalSize(end, frame.IsFin);
        var increase = receiveCredit!.Receive(end);
        if (frame.IsFin)
        {
            finalSize = end;
        }

        if (PeerResetErrorCode is null)
        {
            Reassemble(frame.Offset, frame.Data);
        }

        return increase;
    }

    /// <summary>Takes the peer's RESET_STREAM: the bytes not yet read are dropped and count as consumed; returns by how much the final size moves the highest offset received.</summary>
    internal ulong ReceiveReset(QuicResetStreamFrame frame)
    {
        CheckFinalSize(frame.FinalSize, isFinal: true);
        var increase = receiveCredit!.Receive(frame.FinalSize);
        finalSize = frame.FinalSize;
        if (PeerResetErrorCode is null)
        {
            PeerResetErrorCode = frame.ApplicationErrorCode;
            outOfOrder.Clear();
            readable.Clear();
            readableHeadOffset = 0;
            ReadableLength = 0;
            owner.OnConsumed(this, frame.FinalSize - receiveCredit.Consumed);
        }

        return increase;
    }

    /// <summary>Takes the peer's STOP_SENDING, which RESET_STREAM with the same code answers (RFC 9000 section 3.5).</summary>
    internal void ReceiveStopSending(QuicStopSendingFrame frame)
    {
        PeerStopSendingErrorCode ??= frame.ApplicationErrorCode;
        ResetSending(frame.ApplicationErrorCode);
    }

    /// <summary>Takes the peer's MAX_STREAM_DATA.</summary>
    internal void RaiseSendLimit(ulong limit) => sendCredit!.Raise(limit);

    /// <summary>Gets a value indicating whether a STREAM frame can go now: bytes the limits allow, or a FIN with nothing before it.</summary>
    internal bool CanSendNow(ulong connectionAvailable) =>
        !IsSendReset && (UnsentLength > 0 ? Math.Min(sendCredit!.Available, connectionAvailable) > 0 : finQueued && !IsFinSent);

    /// <summary>Takes the next STREAM frame: as many unsent bytes as <paramref name="maximumLength" /> (at least one) and both limits allow, with FIN when they are the last.</summary>
    /// <returns>The frame, or <see langword="null" /> when nothing can go.</returns>
    internal QuicStreamFrame? TakeFrame(int maximumLength, ulong connectionAvailable)
    {
        if (!CanSendNow(connectionAvailable))
        {
            return null;
        }

        var length = (int)Math.Min((ulong)Math.Min(UnsentLength, maximumLength), Math.Min(sendCredit!.Available, connectionAvailable));
        var fin = finQueued && length == UnsentLength;
        var frame = new QuicStreamFrame(Id, SentOffset, TakeUnsent(length), fin);
        SentOffset += (ulong)length;
        sendCredit.Use((ulong)length);
        IsFinSent = fin;
        return frame;
    }

    /// <summary>Returns <see langword="true" /> once per limit when bytes wait and the stream's limit is all used, for STREAM_DATA_BLOCKED.</summary>
    internal bool TakeBlockedReport() => UnsentLength > 0 && sendCredit!.TakeBlockedReport();

    /// <summary>Gets the stream's send limit, which STREAM_DATA_BLOCKED names.</summary>
    internal ulong SendLimit => sendCredit!.Limit;

    /// <summary>Raises the receive limit and returns the MAX_STREAM_DATA frame that advertises it.</summary>
    internal QuicMaxStreamDataFrame TakeLimitRaise() => new(Id, receiveCredit!.TakeRaise());

    /// <summary>Records bytes consumed against the stream's receive limit.</summary>
    internal void Consume(ulong amount) => receiveCredit!.Consume(amount);

    private void ResetSending(ulong applicationErrorCode)
    {
        if (IsSendReset)
        {
            return;
        }

        IsSendReset = true;
        unsent.Clear();
        unsentHeadOffset = 0;
        UnsentLength = 0;
        owner.QueueControlFrame(new QuicResetStreamFrame(Id, applicationErrorCode, SentOffset));
    }

    // Data past a known final size, a second different final size, or a final size below what has arrived is a FINAL_SIZE_ERROR (RFC 9000 section 4.5).
    private void CheckFinalSize(ulong end, bool isFinal)
    {
        var violated = finalSize is { } known
            ? end > known || (isFinal && end != known)
            : isFinal && end < receiveCredit!.Received;
        if (violated)
        {
            throw new QuicTransportException(QuicTransportErrorCode.FinalSizeError, $"The peer broke the final size of stream {Id}.");
        }
    }

    // Bytes already delivered are dropped; the rest wait by offset until the gap before them fills.
    private void Reassemble(ulong offset, ReadOnlyMemory<byte> data)
    {
        var end = offset + (ulong)data.Length;
        if (end <= nextReceiveOffset)
        {
            return;
        }

        var skip = offset < nextReceiveOffset ? (int)(nextReceiveOffset - offset) : 0;
        Store(offset + (ulong)skip, data[skip..]);
        DeliverContiguous();
    }

    // Of two chunks at one offset the longer is kept.
    private void Store(ulong start, ReadOnlyMemory<byte> data)
    {
        if (!outOfOrder.TryGetValue(start, out var existing) || existing.Length < data.Length)
        {
            outOfOrder[start] = data.ToArray();
        }
    }

    // Chunks that start at or before the next offset expected fill the gap; what they repeat is dropped.
    private void DeliverContiguous()
    {
        while (outOfOrder.Count > 0 && outOfOrder.Keys[0] <= nextReceiveOffset)
        {
            var chunkStart = outOfOrder.Keys[0];
            var chunk = outOfOrder.Values[0];
            outOfOrder.RemoveAt(0);
            var overlap = (int)(nextReceiveOffset - chunkStart);
            if (overlap < chunk.Length)
            {
                Deliver(chunk[overlap..]);
            }
        }
    }

    private void Deliver(byte[] bytes)
    {
        nextReceiveOffset += (ulong)bytes.Length;
        if (IsReadAbandoned)
        {
            owner.OnConsumed(this, (ulong)bytes.Length);
            return;
        }

        readable.Enqueue(bytes);
        ReadableLength += bytes.Length;
    }

    private void DiscardReadable()
    {
        var discarded = (ulong)ReadableLength;
        readable.Clear();
        readableHeadOffset = 0;
        ReadableLength = 0;
        owner.OnConsumed(this, discarded);
    }

    private byte[] TakeUnsent(int length)
    {
        var bytes = new byte[length];
        var taken = 0;
        while (taken < length)
        {
            var head = unsent.Peek();
            var part = Math.Min(length - taken, head.Length - unsentHeadOffset);
            head.AsSpan(unsentHeadOffset, part).CopyTo(bytes.AsSpan(taken));
            taken += part;
            unsentHeadOffset += part;
            if (unsentHeadOffset == head.Length)
            {
                unsent.Dequeue();
                unsentHeadOffset = 0;
            }
        }

        UnsentLength -= length;
        return bytes;
    }
}
