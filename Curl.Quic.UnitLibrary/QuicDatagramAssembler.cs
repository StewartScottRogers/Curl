using System.Buffers;

namespace Curl.Quic;

/// <summary>
/// Turns what each packet number space has to send into protected packets, coalesced into
/// datagrams of at most 1200 bytes, the size every QUIC path carries (RFC 9000 section 14).
/// A datagram that carries an Initial packet is padded to 1200 bytes with PADDING frames in
/// that Initial packet (section 14.1), as curl's build pads its first Initial.
/// </summary>
internal static class QuicDatagramAssembler
{
    /// <summary>The size of every datagram that carries an Initial packet, and the most any datagram carries.</summary>
    public const int DatagramSize = 1200;

    /// <summary>Builds every packet the spaces have frames for, in the order given, and returns the datagrams.</summary>
    /// <param name="spaces">The spaces, Initial first.</param>
    /// <param name="address">The connection IDs and token the packets carry.</param>
    /// <returns>The datagrams to send, in order.</returns>
    public static List<byte[]> Assemble(IEnumerable<QuicPacketNumberSpace> spaces, QuicPacketAddress address)
    {
        List<byte[]> datagrams = [];
        List<PlannedPacket> datagram = [];
        foreach (var space in spaces)
        {
            while (space.HasFramesToSend)
            {
                var planned = Plan(space, address);
                if (Overflows(datagram, planned))
                {
                    datagrams.Add(Seal(datagram));
                    datagram = [];
                }

                datagram.Add(planned);
            }
        }

        if (datagram.Count > 0)
        {
            datagrams.Add(Seal(datagram));
        }

        return datagrams;
    }

    // A packet that does not fit in what is left of a datagram starts the next one; a datagram always takes its first packet.
    private static bool Overflows(List<PlannedPacket> datagram, PlannedPacket next) =>
        datagram.Count > 0 && datagram.Sum(packet => packet.Size) + next.Size > DatagramSize;

    private static PlannedPacket Plan(QuicPacketNumberSpace space, QuicPacketAddress address)
    {
        // The frames get what a datagram has left after the longest header this packet can have and the tag.
        var longestHeader = QuicPacketCodec.Encode(Build(space.PacketType, address, 4, 0, [])).Length;
        var (frames, packetNumber) = space.TakeFrames(DatagramSize - longestHeader - QuicPacketProtection.TagLength);
        var packetNumberLength = QuicPacketNumber.GetEncodedLength(packetNumber, space.LargestAcknowledged);
        var payload = QuicFrameCodec.Encode(frames);

        // The header protection sample starts four bytes after the packet number; pad a short payload so it exists (RFC 9001 section 5.4.2).
        var shortfall = Math.Max(0, QuicHeaderProtection.SampleOffset - packetNumberLength - payload.Length);
        payload = [.. payload, .. new byte[shortfall]];
        var packet = Build(space.PacketType, address, packetNumberLength, QuicPacketNumber.Truncate(packetNumber, packetNumberLength), payload);
        return new PlannedPacket(space, packet, packetNumber, QuicPacketCodec.Encode(packet).Length + QuicPacketProtection.TagLength);
    }

    private static QuicPacket Build(QuicPacketType type, QuicPacketAddress address, int packetNumberLength, uint truncatedPacketNumber, byte[] payload) => type == QuicPacketType.OneRtt
        ? new QuicShortHeaderPacket(address.ShortHeaderDestination, packetNumberLength, truncatedPacketNumber, payload)
        : new QuicLongHeaderPacket(type, QuicPacketCodec.Version1, address.LongHeaderDestination, address.Source, type == QuicPacketType.Initial ? address.Token : ReadOnlyMemory<byte>.Empty, packetNumberLength, truncatedPacketNumber, payload);

    private static byte[] Seal(List<PlannedPacket> packets)
    {
        var padding = DatagramSize - packets.Sum(packet => packet.Size);
        var datagram = new ArrayBufferWriter<byte>();
        foreach (var planned in packets)
        {
            var packet = planned.Packet;
            if (padding > 0 && packet is QuicLongHeaderPacket { Type: QuicPacketType.Initial } initial)
            {
                packet = initial with { Payload = (byte[])[.. initial.Payload.Span, .. new byte[padding]] };
                padding = 0;
            }

            datagram.Write(planned.Space.SendProtection!.Protect(packet, planned.PacketNumber));
        }

        return datagram.WrittenSpan.ToArray();
    }

    private sealed record PlannedPacket(QuicPacketNumberSpace Space, QuicPacket Packet, ulong PacketNumber, int Size);
}
