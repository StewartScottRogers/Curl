using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Presents an <see cref="IConnection" /> as the <see cref="Stream" /> the HTTP/2 frame layer
/// reads and writes, reading ahead into a buffer so that several streams of one
/// <see cref="Http2Session" /> can wait for the connection's next frame
/// (<see cref="ReadTowardsNextFrameAsync" />) without taking its bytes, and only the one that
/// then holds the session's frame lock reads it (BL-717). A frame is read only once it is
/// buffered whole, so a wait cancelled part way through one loses none of it (BL-5976). Writes
/// and flushes pass straight through. Only the asynchronous members work; the synchronous ones
/// throw <see cref="NotSupportedException" />.
/// </summary>
/// <remarks>
/// One read of the connection is in flight at a time, shared by every waiter. It is not
/// cancelled when a waiter gives up, since other streams may still want its bytes; closing the
/// connection ends it.
/// </remarks>
/// <param name="connection">The connection; the stream does not dispose it.</param>
internal sealed class ReadAheadConnectionStream(IConnection connection) : Stream
{
    /// <summary>The most bytes buffered: one frame of the largest size this client accepts.</summary>
    internal const int BufferSize = Http2FrameCodec.HeaderLength + Http2FrameCodec.DefaultMaximumFrameSize;

    private readonly byte[] buffer = new byte[BufferSize];

    private readonly Lock gate = new();

    private int start;

    private int end;

    private bool isEnded;

    private TaskCompletionSource? filling;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Gets a value indicating whether a read returns at once: bytes are buffered, or the
    /// connection has ended.
    /// </summary>
    public bool HasBytesOrEnded
    {
        get
        {
            lock (gate)
            {
                return start < end || isEnded;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a frame can be read without waiting: the next one is
    /// buffered whole - or its header alone when it is longer than the client accepts, which the
    /// frame layer refuses from the header - or the connection has ended.
    /// </summary>
    public bool HasFrameOrEnded
    {
        get
        {
            lock (gate)
            {
                return end - start >= BytesOfNextFrame() || isEnded;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a write was cancelled, which may have left part of a
    /// frame on the wire.
    /// </summary>
    public bool IsWriteCancelled { get; private set; }

    /// <summary>
    /// Waits until a read would return at once (<see cref="HasBytesOrEnded" />), reading the
    /// connection when nobody is already reading it; the bytes stay buffered for whoever reads.
    /// </summary>
    /// <param name="cancellationToken">Ends this wait, not the read of the connection.</param>
    /// <returns>A task that completes when bytes are buffered or the connection has ended.</returns>
    /// <exception cref="IOException">The read of the connection failed.</exception>
    public Task WaitForBytesAsync(CancellationToken cancellationToken) => ReadUnlessBufferedAsync(static () => 1, cancellationToken);

    /// <summary>
    /// Waits for one read of the connection to add to the buffer, starting it when nobody is
    /// already reading, unless a frame can be read already (<see cref="HasFrameOrEnded" />).
    /// A frame that arrives in several pieces takes several calls.
    /// </summary>
    /// <param name="cancellationToken">Ends this wait, not the read of the connection.</param>
    /// <returns>A task that completes when the read has, or at once when none is needed.</returns>
    /// <exception cref="IOException">The read of the connection failed.</exception>
    public Task ReadTowardsNextFrameAsync(CancellationToken cancellationToken) => ReadUnlessBufferedAsync(BytesOfNextFrame, cancellationToken);

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        await WaitForBytesAsync(cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            int count = Math.Min(end - start, destination.Length);
            buffer.AsSpan(start, count).CopyTo(destination.Span);
            start += count;
            return count;
        }
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.WriteAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            IsWriteCancelled = true;
            throw;
        }
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        connection.FlushAsync(cancellationToken).AsTask();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    // Called holding the gate: the next frame's length, or its header's while that is not all
    // buffered or when the frame is too long to accept.
    private int BytesOfNextFrame()
    {
        if (end - start < Http2FrameCodec.HeaderLength)
        {
            return Http2FrameCodec.HeaderLength;
        }

        int payloadLength = (buffer[start] << 16) | (buffer[start + 1] << 8) | buffer[start + 2];
        return payloadLength > Http2FrameCodec.DefaultMaximumFrameSize ? Http2FrameCodec.HeaderLength : Http2FrameCodec.HeaderLength + payloadLength;
    }

    // Waits for the read in flight, or one started here, unless the buffer holds the bytes asked
    // for or the connection has ended. A read starts with the buffered bytes moved to the front,
    // so there is room for the rest of the largest frame behind them.
    private Task ReadUnlessBufferedAsync(Func<int> bytesNeeded, CancellationToken cancellationToken)
    {
        TaskCompletionSource? started = null;
        Task fill;
        lock (gate)
        {
            if (end - start >= bytesNeeded() || isEnded)
            {
                return Task.CompletedTask;
            }

            if (filling is null)
            {
                filling = started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                buffer.AsSpan(start, end - start).CopyTo(buffer);
                end -= start;
                start = 0;
            }

            fill = filling.Task;
        }

        if (started is not null)
        {
            _ = FillAsync(started);
        }

        return fill.WaitAsync(cancellationToken);
    }

    private async Task FillAsync(TaskCompletionSource started)
    {
        try
        {
            int read = await connection.ReadAsync(buffer.AsMemory(end), CancellationToken.None).ConfigureAwait(false);
            lock (gate)
            {
                end += read;
                isEnded = read == 0;
                filling = null;
            }

            started.SetResult();
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                filling = null;
            }

            started.SetException(exception);
        }
    }
}
