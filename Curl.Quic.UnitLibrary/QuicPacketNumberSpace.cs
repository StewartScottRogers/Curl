using System.Buffers;

namespace Curl.Quic;

/// <summary>
/// One packet number space of a connection (RFC 9000 section 12.3): Initial, Handshake or
/// application data. It holds that level's keys in each direction, the next packet number
/// to send, the packet numbers received (to acknowledge them and drop duplicates), the
/// CRYPTO bytes received and still to send, and control frames waiting to go out.
/// </summary>
internal sealed class QuicPacketNumberSpace(QuicPacketType packetType) : IDisposable
{
    /// <summary>The most a CRYPTO frame's type, offset and length fields take: 1, 8 and 4 bytes.</summary>
    public const int CryptoFrameOverhead = 13;

    private readonly SortedSet<ulong> received = [];

    private readonly List<QuicFrame> pendingFrames = [];

    private readonly ArrayBufferWriter<byte> cryptoSent = new();

    private int cryptoQueuedFrom;

    private bool acknowledgementDue;

    /// <summary>Gets the type of the packets this space sends and receives.</summary>
    public QuicPacketType PacketType => packetType;

    /// <summary>Gets or sets the keys that protect what this endpoint sends, or <see langword="null" /> before they are known or after they are discarded.</summary>
    public QuicPacketProtection? SendProtection { get; set; }

    /// <summary>Gets or sets the keys that open what the peer sends, or <see langword="null" /> before they are known or after they are discarded.</summary>
    public QuicPacketProtection? ReceiveProtection { get; set; }

    /// <summary>Gets a value indicating whether the keys have been discarded; nothing is sent or received here any more.</summary>
    public bool IsDiscarded { get; private set; }

    /// <summary>Gets the CRYPTO stream the peer sends at this level.</summary>
    public QuicCryptoReassembler CryptoReceived { get; } = new();

    /// <summary>Gets the largest packet number received, or <see langword="null" /> before any.</summary>
    public ulong? LargestReceived => received.Count == 0 ? null : received.Max;

    /// <summary>Gets the largest packet number the peer has acknowledged, or <see langword="null" /> before any.</summary>
    public ulong? LargestAcknowledged { get; private set; }

    /// <summary>Gets the packet number the next packet sent here carries.</summary>
    public ulong NextPacketNumber { get; private set; }

    /// <summary>Gets a value indicating whether the space has an acknowledgement, a control frame or CRYPTO bytes to send and keys to send them with.</summary>
    public bool HasFramesToSend =>
        SendProtection is not null && (acknowledgementDue || pendingFrames.Count > 0 || cryptoQueuedFrom < cryptoSent.WrittenCount);

    /// <summary>Records a received packet number; returns <see langword="false" /> for a duplicate, which is dropped unread.</summary>
    public bool RecordReceived(ulong packetNumber) => received.Add(packetNumber);

    /// <summary>Marks that an ack-eliciting packet has arrived, so an ACK frame goes out with the next packet.</summary>
    public void RequireAcknowledgement() => acknowledgementDue = true;

    /// <summary>Records the peer's ACK frame, which sets how many bytes of each packet number to send.</summary>
    /// <exception cref="QuicTransportException">The frame acknowledges a packet never sent here (<see cref="QuicTransportErrorCode.ProtocolViolation" />, RFC 9000 section 13.1).</exception>
    public void ReceiveAcknowledgement(QuicAckFrame frame) =>
        LargestAcknowledged = frame.LargestAcknowledged < NextPacketNumber
            ? Math.Max(LargestAcknowledged ?? 0, frame.LargestAcknowledged)
            : throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, $"An ACK frame acknowledges packet {frame.LargestAcknowledged}, which was never sent.");

    /// <summary>Queues CRYPTO bytes to send, after those queued before.</summary>
    public void QueueCrypto(byte[] bytes) => cryptoSent.Write(bytes);

    /// <summary>Queues every CRYPTO byte sent so far to go again from offset 0, as after a Retry (RFC 9000 section 17.2.5.2).</summary>
    public void RequeueCrypto() => cryptoQueuedFrom = 0;

    /// <summary>Queues a control frame to go out with the next packet.</summary>
    public void QueueFrame(QuicFrame frame) => pendingFrames.Add(frame);

    /// <summary>
    /// Takes the frames for the next packet: the ACK when one is due, the control frames,
    /// then as many CRYPTO bytes as fit in what is left of <paramref name="payloadRoom" />,
    /// and reserves its packet number.
    /// </summary>
    /// <param name="payloadRoom">The most bytes the packet's frames may take.</param>
    /// <returns>The frames and the packet number.</returns>
    public (List<QuicFrame> Frames, ulong PacketNumber) TakeFrames(int payloadRoom)
    {
        List<QuicFrame> frames = [];
        if (acknowledgementDue)
        {
            frames.Add(BuildAcknowledgement());
            acknowledgementDue = false;
        }

        frames.AddRange(pendingFrames);
        pendingFrames.Clear();
        var cryptoRoom = payloadRoom - QuicFrameCodec.Encode(frames).Length - CryptoFrameOverhead;
        var cryptoLength = Math.Min(cryptoRoom, cryptoSent.WrittenCount - cryptoQueuedFrom);
        if (cryptoLength > 0)
        {
            frames.Add(new QuicCryptoFrame((ulong)cryptoQueuedFrom, cryptoSent.WrittenMemory.Slice(cryptoQueuedFrom, cryptoLength).ToArray()));
            cryptoQueuedFrom += cryptoLength;
        }

        return (frames, NextPacketNumber++);
    }

    /// <summary>Discards the keys and everything waiting to be sent (RFC 9001 section 4.9).</summary>
    public void Discard()
    {
        Dispose();
        SendProtection = null;
        ReceiveProtection = null;
        IsDiscarded = true;
        acknowledgementDue = false;
        pendingFrames.Clear();
        DropUnsentCrypto();
    }

    /// <summary>Drops the CRYPTO bytes not yet sent, as when the connection closes instead.</summary>
    public void DropUnsentCrypto() => cryptoQueuedFrom = cryptoSent.WrittenCount;

    /// <inheritdoc />
    public void Dispose()
    {
        SendProtection?.Dispose();
        ReceiveProtection?.Dispose();
    }

    private QuicAckFrame BuildAcknowledgement()
    {
        List<(ulong Largest, ulong Smallest)> runs = [];
        foreach (var packetNumber in received.Reverse())
        {
            if (runs.Count > 0 && packetNumber + 1 == runs[^1].Smallest)
            {
                runs[^1] = (runs[^1].Largest, packetNumber);
            }
            else
            {
                runs.Add((packetNumber, packetNumber));
            }
        }

        // Each later range is the gap below the range before it, less two, and its own length less one (section 19.3.1).
        List<QuicAckRange> ranges = [.. runs.Skip(1).Select((run, index) => new QuicAckRange(runs[index].Smallest - run.Largest - 2, run.Largest - run.Smallest))];
        return new QuicAckFrame(runs[0].Largest, 0, runs[0].Largest - runs[0].Smallest, ranges, null);
    }
}
