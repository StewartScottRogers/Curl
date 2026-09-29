using System.Buffers;

namespace Curl.Quic;

/// <summary>
/// Turns what each packet number space has to send into protected packets, coalesced into
/// datagrams of at most 1200 bytes, the size every QUIC path carries (RFC 9000 section 14).
/// A datagram that carries an Initial packet is padded to 1200 bytes with PADDING frames in
/// that Initial packet (section 14.1), as curl's build pads its first Initial. Packets for
/// different destination connection IDs never share a datagram (section 12.2). Packets that
/// carry more than an acknowledgement stop once they have used the congestion allowance.
/// </summary>
internal static class QuicDatagramAssembler
{
    /// <summary>The size of every datagram that carries an Initial packet, and the most any datagram carries.</summary>
    public const int DatagramSize = 1200;

    /// <summary>Builds every packet the spaces have frames for, in the order given, and returns the datagrams and what loss recovery remembers of each packet.</summary>
    /// <param name="spaces">The spaces, Initial first.</param>
    /// <param name="address">The connection IDs and token the packets carry.</param>
    /// <param name="now">The time now: each packet's send time and the base of its ACK Delay.</param>
    /// <param name="congestionAllowance">The bytes the congestion window lets go; acknowledgement-only packets go regardless, and a packet that starts inside the allowance may end past it.</param>
    /// <returns>The datagrams to send, in order, and the packets they carry.</returns>
    public static QuicAssembly Assemble(IEnumerable<QuicPacketNumberSpace> spaces, QuicPacketAddress address, TimeSpan now, long congestionAllowance)
    {
        QuicAssembly assembly = new([], []);
        List<PlannedPacket> datagram = [];
        foreach (var planned in PlanPackets(spaces, address, now, congestionAllowance))
        {
            if (StartsNewDatagram(datagram, planned))
            {
                Seal(datagram, now, assembly);
                datagram = [];
            }

            datagram.Add(planned);
        }

        if (datagram.Count > 0)
        {
            Seal(datagram, now, assembly);
        }

        return assembly;
    }

    private static List<PlannedPacket> PlanPackets(IEnumerable<QuicPacketNumberSpace> spaces, QuicPacketAddress address, TimeSpan now, long congestionAllowance)
    {
        List<PlannedPacket> packets = [];
        foreach (var space in spaces)
        {
            while (space.HasFramesToSend && (congestionAllowance > 0 || space.HasOnlyAcknowledgementToSend))
            {
                var planned = Plan(space, address, now);
                congestionAllowance -= planned.Size;
                packets.Add(planned);
            }
        }

        return packets;
    }

    // A packet that does not fit in what is left of a datagram, goes to another connection
    // ID, or would follow a short header packet, which has no length and so ends its datagram
    // (RFC 9000 section 12.2), starts the next one; a datagram always takes its first packet.
    private static bool StartsNewDatagram(List<PlannedPacket> datagram, PlannedPacket next) =>
        datagram.Count > 0
        && (datagram[^1].Space.PacketType == QuicPacketType.OneRtt
            || datagram.Sum(packet => packet.Size) + next.Size > DatagramSize
            || !datagram[0].Destination.Span.SequenceEqual(next.Destination.Span));

    private static PlannedPacket Plan(QuicPacketNumberSpace space, QuicPacketAddress address, TimeSpan now)
    {
        // The frames get what a datagram has left after the longest header this packet can have and the tag.
        var longestHeader = QuicPacketCodec.Encode(Build(space.PacketType, address, 4, 0, [])).Length;
        var (frames, packetNumber) = space.TakeFrames(DatagramSize - longestHeader - QuicPacketProtection.TagLength, now);
        var packetNumberLength = QuicPacketNumber.GetEncodedLength(packetNumber, space.LargestAcknowledged);
        var payload = QuicFrameCodec.Encode(frames);

        // The header protection sample starts four bytes after the packet number; pad a short payload so it exists (RFC 9001 section 5.4.2).
        var shortfall = Math.Max(0, QuicHeaderProtection.SampleOffset - packetNumberLength - payload.Length);
        payload = [.. payload, .. new byte[shortfall]];
        var packet = Build(space.PacketType, address, packetNumberLength, QuicPacketNumber.Truncate(packetNumber, packetNumberLength), payload);
        var destination = space.PacketType == QuicPacketType.OneRtt ? address.ShortHeaderDestination : address.LongHeaderDestination;
        return new PlannedPacket(space, packet, packetNumber, frames, shortfall > 0, destination, QuicPacketCodec.Encode(packet).Length + QuicPacketProtection.TagLength);
    }

    private static QuicPacket Build(QuicPacketType type, QuicPacketAddress address, int packetNumberLength, uint truncatedPacketNumber, byte[] payload) => type == QuicPacketType.OneRtt
        ? new QuicShortHeaderPacket(address.ShortHeaderDestination, packetNumberLength, truncatedPacketNumber, payload)
        : new QuicLongHeaderPacket(type, QuicPacketCodec.Version1, address.LongHeaderDestination, address.Source, type == QuicPacketType.Initial ? address.Token : ReadOnlyMemory<byte>.Empty, packetNumberLength, truncatedPacketNumber, payload);

    private static void Seal(List<PlannedPacket> packets, TimeSpan now, QuicAssembly assembly)
    {
        var padding = DatagramSize - packets.Sum(packet => packet.Size);
        var datagram = new ArrayBufferWriter<byte>();
        foreach (var planned in packets)
        {
            var toSend = planned;
            if (padding > 0 && planned.Packet is QuicLongHeaderPacket { Type: QuicPacketType.Initial } initial)
            {
                toSend = planned with { Packet = initial with { Payload = (byte[])[.. initial.Payload.Span, .. new byte[padding]] }, Padded = true };
                padding = 0;
            }

            var bytes = toSend.Space.SendProtection!.Protect(toSend.Packet, toSend.PacketNumber);
            datagram.Write(bytes);
            assembly.Packets.Add((toSend.Space, SentPacket(toSend, now, bytes.Length)));
        }

        assembly.Datagrams.Add(datagram.WrittenSpan.ToArray());
    }

    // A packet is in flight when it asks for an acknowledgement or carries padding (RFC 9002 section 2).
    private static QuicSentPacket SentPacket(PlannedPacket planned, TimeSpan now, int sentBytes)
    {
        var ackEliciting = planned.Frames.Any(frame => frame.IsAckEliciting);
        return new QuicSentPacket(planned.PacketNumber, now, ackEliciting, ackEliciting || planned.Padded, sentBytes, planned.Frames);
    }

    private sealed record PlannedPacket(QuicPacketNumberSpace Space, QuicPacket Packet, ulong PacketNumber, List<QuicFrame> Frames, bool Padded, ReadOnlyMemory<byte> Destination, int Size);
}
