using System.Buffers;

namespace Curl.Quic;

/// <summary>
/// One packet number space of a connection (RFC 9000 section 12.3): Initial, Handshake or
/// application data. It holds that level's keys in each direction, the next packet number
/// to send, the packet numbers received (to acknowledge them and drop duplicates), the
/// CRYPTO bytes received and still to send, the CRYPTO ranges lost packets carried, and
/// control and stream frames waiting to go out.
/// </summary>
internal sealed class QuicPacketNumberSpace(QuicPacketType packetType) : IDisposable
{
    /// <summary>The most a CRYPTO frame's type, offset and length fields take: 1, 8 and 4 bytes.</summary>
    public const int CryptoFrameOverhead = 13;

    // The frames whose loss sends nothing again (RFC 9000 section 13.3).
    private static readonly HashSet<QuicFrameType> NeverResent =
    [
        QuicFrameType.Padding,
        QuicFrameType.Ping,
        QuicFrameType.Ack,
        QuicFrameType.AckWithEcnCounts,
        QuicFrameType.PathChallenge,
        QuicFrameType.PathResponse,
        QuicFrameType.ConnectionClose,
        QuicFrameType.ConnectionCloseApplication,
    ];

    private readonly SortedSet<ulong> received = [];

    private readonly List<QuicFrame> pendingFrames = [];

    private readonly ArrayBufferWriter<byte> cryptoSent = new();

    private readonly List<(int Offset, int Length)> cryptoLost = [];

    private int cryptoQueuedFrom;

    private bool acknowledgementDue;

    private TimeSpan largestReceivedTime;

    /// <summary>Gets the type of the packets this space sends and receives.</summary>
    public QuicPacketType PacketType => packetType;

    /// <summary>Gets the space loss recovery knows this one as.</summary>
    public QuicPacketNumberSpaceId Id => packetType switch
    {
        QuicPacketType.Initial => QuicPacketNumberSpaceId.Initial,
        QuicPacketType.Handshake => QuicPacketNumberSpaceId.Handshake,
        _ => QuicPacketNumberSpaceId.ApplicationData,
    };

    /// <summary>Gets or sets the keys that protect what this endpoint sends, or <see langword="null" /> before they are known or after they are discarded.</summary>
    public QuicPacketProtection? SendProtection { get; set; }

    /// <summary>Gets or sets the keys that open what the peer sends, or <see langword="null" /> before they are known or after they are discarded.</summary>
    public QuicPacketProtection? ReceiveProtection { get; set; }

    /// <summary>Gets or sets the exponent this endpoint's ACK Delay fields are encoded with: its own <c>ack_delay_exponent</c> (RFC 9000 section 18.2).</summary>
    public int AckDelayExponent { get; set; } = (int)QuicTransportParameters.DefaultAckDelayExponent;

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

    /// <summary>Gets a value indicating whether the space has an acknowledgement, a frame or CRYPTO bytes to send and keys to send them with.</summary>
    public bool HasFramesToSend =>
        SendProtection is not null && (acknowledgementDue || pendingFrames.Count > 0 || cryptoLost.Count > 0 || cryptoQueuedFrom < cryptoSent.WrittenCount);

    /// <summary>Gets a value indicating whether what waits to be sent is only an acknowledgement, which the congestion window does not hold back (RFC 9002 section 7).</summary>
    public bool HasOnlyAcknowledgementToSend => acknowledgementDue && pendingFrames.Count == 0 && cryptoLost.Count == 0 && cryptoQueuedFrom == cryptoSent.WrittenCount;

    /// <summary>Records a received packet number and when it arrived; returns <see langword="false" /> for a duplicate, which is dropped unread.</summary>
    /// <param name="packetNumber">The packet number.</param>
    /// <param name="receivedAt">When it arrived, for the ACK Delay of the acknowledgement that names it as the largest.</param>
    /// <returns>Whether it was new.</returns>
    public bool RecordReceived(ulong packetNumber, TimeSpan receivedAt)
    {
        if (LargestReceived is not { } largest || packetNumber > largest)
        {
            largestReceivedTime = receivedAt;
        }

        return received.Add(packetNumber);
    }

    /// <summary>Marks that an ack-eliciting packet has arrived, so an ACK frame goes out with the next packet.</summary>
    public void RequireAcknowledgement() => acknowledgementDue = true;

    /// <summary>Records the peer's ACK frame, which sets how many bytes of each packet number to send.</summary>
    /// <param name="frame">The frame.</param>
    /// <exception cref="QuicTransportException">The frame acknowledges a packet never sent here (<see cref="QuicTransportErrorCode.ProtocolViolation" />, RFC 9000 section 13.1).</exception>
    public void ReceiveAcknowledgement(QuicAckFrame frame) =>
        LargestAcknowledged = frame.LargestAcknowledged < NextPacketNumber
            ? Math.Max(LargestAcknowledged ?? 0, frame.LargestAcknowledged)
            : throw new QuicTransportException(QuicTransportErrorCode.ProtocolViolation, $"An ACK frame acknowledges packet {frame.LargestAcknowledged}, which was never sent.");

    /// <summary>Queues CRYPTO bytes to send, after those queued before.</summary>
    /// <param name="bytes">The bytes.</param>
    public void QueueCrypto(byte[] bytes) => cryptoSent.Write(bytes);

    /// <summary>Queues every CRYPTO byte sent so far to go again from offset 0, as after a Retry (RFC 9000 section 17.2.5.2).</summary>
    public void RequeueCrypto()
    {
        cryptoQueuedFrom = 0;
        cryptoLost.Clear();
    }

    /// <summary>Queues a frame to go out with the next packet.</summary>
    /// <param name="frame">The frame.</param>
    public void QueueFrame(QuicFrame frame) => pendingFrames.Add(frame);

    /// <summary>
    /// Queues what the frames of a lost packet carried to go again in new packets (RFC 9000
    /// section 13.3): CRYPTO data by its range, sent before new CRYPTO data; STREAM and
    /// control frames as they were. ACK, PADDING and PING frames, CONNECTION_CLOSE, and
    /// PATH_CHALLENGE and PATH_RESPONSE are never sent again.
    /// </summary>
    /// <param name="frames">The lost packet's frames.</param>
    public void RequeueLost(IEnumerable<QuicFrame> frames)
    {
        foreach (var frame in frames.Where(frame => !NeverResent.Contains(frame.Type)))
        {
            if (frame is QuicCryptoFrame crypto)
            {
                cryptoLost.Add(((int)crypto.Offset, crypto.Data.Length));
            }
            else
            {
                pendingFrames.Add(frame);
            }
        }
    }

    /// <summary>
    /// Takes the frames for the next packet: the ACK when one is due, the queued frames, then
    /// lost CRYPTO ranges and new CRYPTO bytes as far as what is left of
    /// <paramref name="payloadRoom" /> allows, and reserves its packet number.
    /// </summary>
    /// <param name="payloadRoom">The most bytes the packet's frames may take.</param>
    /// <param name="now">The time now, which sets the ACK Delay.</param>
    /// <returns>The frames and the packet number.</returns>
    public (List<QuicFrame> Frames, ulong PacketNumber) TakeFrames(int payloadRoom, TimeSpan now)
    {
        List<QuicFrame> frames = [];
        if (acknowledgementDue)
        {
            frames.Add(BuildAcknowledgement(now));
            acknowledgementDue = false;
        }

        frames.AddRange(pendingFrames);
        pendingFrames.Clear();
        var room = payloadRoom - QuicFrameCodec.Encode(frames).Length;
        room = TakeLostCrypto(frames, room);
        var cryptoLength = Math.Min(room - CryptoFrameOverhead, cryptoSent.WrittenCount - cryptoQueuedFrom);
        if (cryptoLength > 0)
        {
            frames.Add(CryptoFrame(cryptoQueuedFrom, cryptoLength));
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

    /// <summary>Drops the CRYPTO bytes not yet sent and the lost ranges not yet sent again, as when the connection closes instead.</summary>
    public void DropUnsentCrypto()
    {
        cryptoQueuedFrom = cryptoSent.WrittenCount;
        cryptoLost.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SendProtection?.Dispose();
        ReceiveProtection?.Dispose();
    }

    private QuicCryptoFrame CryptoFrame(int offset, int length) => new((ulong)offset, cryptoSent.WrittenMemory.Slice(offset, length).ToArray());

    // Lost CRYPTO ranges go first, split where the packet runs out of room; returns the room left.
    private int TakeLostCrypto(List<QuicFrame> frames, int room)
    {
        while (cryptoLost.Count > 0 && room > CryptoFrameOverhead)
        {
            var (offset, length) = cryptoLost[0];
            var taken = Math.Min(length, room - CryptoFrameOverhead);
            frames.Add(CryptoFrame(offset, taken));
            room -= CryptoFrameOverhead + taken;
            if (taken == length)
            {
                cryptoLost.RemoveAt(0);
            }
            else
            {
                cryptoLost[0] = (offset + taken, length - taken);
            }
        }

        return room;
    }

    private QuicAckFrame BuildAcknowledgement(TimeSpan now)
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

        // The delay since the largest arrived, in microseconds scaled down by the exponent (section 19.3).
        var ackDelay = (ulong)Math.Max(0, (long)(now - largestReceivedTime).TotalMicroseconds) >> AckDelayExponent;
        return new QuicAckFrame(runs[0].Largest, ackDelay, runs[0].Largest - runs[0].Smallest, ranges, null);
    }
}
