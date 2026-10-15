using Curl.Quic;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// One packet number space of <see cref="Http3ServerConnection" /> (RFC 9000 section 12.3):
/// its keys each way, the packet numbers received (a repeat is dropped), the ACK it owes, the
/// client's CRYPTO stream put back in order, and the frames waiting to go. Nothing is resent:
/// in memory nothing is lost.
/// </summary>
/// <param name="packetType">The packet type the space's packets carry.</param>
/// <param name="level">The TLS encryption level the space's CRYPTO frames belong to.</param>
internal sealed class Http3ServerPacketSpace(QuicPacketType packetType, TlsEncryptionLevel level) : IDisposable
{
    /// <summary>The most frame bytes one packet carries, so a packet fits a 1200-byte datagram.</summary>
    public const int MaximumPayloadLength = 1100;

    /// <summary>The most data bytes one CRYPTO or STREAM frame carries.</summary>
    public const int MaximumChunkLength = 1000;

    private readonly SortedSet<ulong> received = [];

    private readonly List<QuicFrame> pending = [];

    private ulong nextPacketNumber;

    private ulong cryptoSentOffset;

    private bool acknowledgementOwed;

    /// <summary>Gets the packet type the space's packets carry.</summary>
    public QuicPacketType PacketType => packetType;

    /// <summary>Gets the TLS encryption level the space's CRYPTO frames belong to.</summary>
    public TlsEncryptionLevel Level => level;

    /// <summary>Gets or sets the protection of the server's packets, once TLS has installed it.</summary>
    public QuicPacketProtection? SendProtection { get; set; }

    /// <summary>Gets or sets the protection of the client's packets, once TLS has installed it.</summary>
    public QuicPacketProtection? ReceiveProtection { get; set; }

    /// <summary>Gets the client's CRYPTO frames, put back in order.</summary>
    public QuicCryptoReassembler CryptoReceived { get; } = new();

    /// <summary>Gets the largest packet number received, or <see langword="null" /> before the first.</summary>
    public ulong? LargestReceived => received.Count == 0 ? null : received.Max;

    /// <summary>Records a received packet number.</summary>
    /// <param name="packetNumber">The packet number.</param>
    /// <returns><see langword="false" /> when the packet repeats one already received, which is then dropped.</returns>
    public bool RecordReceived(ulong packetNumber) => received.Add(packetNumber);

    /// <summary>Owes the client an ACK frame, sent in the space's next packet.</summary>
    public void RequireAcknowledgement() => acknowledgementOwed = true;

    /// <summary>Queues a frame for the space's next packet.</summary>
    /// <param name="frame">The frame.</param>
    public void QueueFrame(QuicFrame frame) => pending.Add(frame);

    /// <summary>Queues TLS handshake bytes as CRYPTO frames at the next offsets of the space's CRYPTO stream.</summary>
    /// <param name="bytes">The handshake bytes.</param>
    public void QueueCrypto(byte[] bytes)
    {
        for (var start = 0; start < bytes.Length; start += MaximumChunkLength)
        {
            var length = Math.Min(MaximumChunkLength, bytes.Length - start);
            pending.Add(new QuicCryptoFrame(cryptoSentOffset, bytes.AsMemory(start, length)));
            cryptoSentOffset += (ulong)length;
        }
    }

    /// <summary>Returns the next packet number to send with, and moves past it.</summary>
    /// <returns>The packet number.</returns>
    public ulong TakePacketNumber() => nextPacketNumber++;

    /// <summary>
    /// Returns the payloads of the packets carrying the ACK owed and every queued frame, each
    /// at most <see cref="MaximumPayloadLength" /> bytes, and empties the queue. Every frame
    /// queued is shorter than that, since data goes in chunks of <see cref="MaximumChunkLength" />.
    /// </summary>
    /// <returns>The encoded frames of each packet; none when nothing waits.</returns>
    public List<byte[]> TakePayloads()
    {
        List<QuicFrame> frames = acknowledgementOwed ? [BuildAcknowledgement(), .. pending] : [.. pending];
        acknowledgementOwed = false;
        pending.Clear();
        List<byte[]> payloads = [];
        List<QuicFrame> packet = [];
        var length = 0;
        foreach (var frame in frames)
        {
            var frameLength = QuicFrameCodec.Encode([frame]).Length;
            if (length + frameLength > MaximumPayloadLength)
            {
                payloads.Add(QuicFrameCodec.Encode(packet));
                packet = [];
                length = 0;
            }

            packet.Add(frame);
            length += frameLength;
        }

        if (packet.Count > 0)
        {
            payloads.Add(QuicFrameCodec.Encode(packet));
        }

        return payloads;
    }

    /// <summary>Returns the ACK frame of every packet number received, largest range first (RFC 9000 section 19.3).</summary>
    /// <returns>The ACK frame, with an ACK Delay of 0.</returns>
    public QuicAckFrame BuildAcknowledgement()
    {
        List<(ulong Smallest, ulong Largest)> ranges = [];
        foreach (var packetNumber in received.Reverse())
        {
            if (ranges.Count > 0 && ranges[^1].Smallest == packetNumber + 1)
            {
                ranges[^1] = (packetNumber, ranges[^1].Largest);
            }
            else
            {
                ranges.Add((packetNumber, packetNumber));
            }
        }

        List<QuicAckRange> further = [];
        for (var index = 1; index < ranges.Count; index++)
        {
            further.Add(new QuicAckRange(ranges[index - 1].Smallest - ranges[index].Largest - 2, ranges[index].Largest - ranges[index].Smallest));
        }

        return new QuicAckFrame(ranges[0].Largest, 0, ranges[0].Largest - ranges[0].Smallest, further, null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SendProtection?.Dispose();
        ReceiveProtection?.Dispose();
    }
}
