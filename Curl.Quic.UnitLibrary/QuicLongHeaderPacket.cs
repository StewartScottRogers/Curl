namespace Curl.Quic;

/// <summary>
/// An Initial, 0-RTT or Handshake packet (RFC 9000 sections 17.2.2 to 17.2.4): a long
/// header with a Length field and a packet number, then the payload. The Length field is
/// written in at least two bytes, as RFC 9001 Appendix A.2 writes it, so the header's size
/// does not change while the payload is padded to fill a datagram.
/// </summary>
/// <param name="Type"><see cref="QuicPacketType.Initial" />, <see cref="QuicPacketType.ZeroRtt" /> or <see cref="QuicPacketType.Handshake" />.</param>
/// <param name="Version">The QUIC version, 1.</param>
/// <param name="DestinationConnectionId">The destination connection ID, at most 20 bytes.</param>
/// <param name="SourceConnectionId">The source connection ID, at most 20 bytes.</param>
/// <param name="Token">An Initial packet's token; empty for the other types.</param>
/// <param name="PacketNumberLength">How many bytes of the packet number the packet carries, 1 to 4.</param>
/// <param name="TruncatedPacketNumber">Those bytes of the packet number (see <see cref="QuicPacketNumber" />).</param>
/// <param name="Payload">What follows the packet number: the frames, or once protected the ciphertext and its authentication tag.</param>
/// <param name="ReservedBits">The two Reserved Bits of the first byte, which must be zero once protection is removed.</param>
public sealed record QuicLongHeaderPacket(
    QuicPacketType Type,
    uint Version,
    ReadOnlyMemory<byte> DestinationConnectionId,
    ReadOnlyMemory<byte> SourceConnectionId,
    ReadOnlyMemory<byte> Token,
    int PacketNumberLength,
    uint TruncatedPacketNumber,
    ReadOnlyMemory<byte> Payload,
    byte ReservedBits = 0) : QuicPacket
{
    /// <inheritdoc />
    internal override void WriteTo(QuicWriter writer)
    {
        RequireWritable();
        writer.WriteByte((byte)(0xc0 | (int)Type << 4 | (ReservedBits & 0x03) << 2 | (PacketNumberLength - 1)));
        writer.WriteUInt(Version, 4);
        WriteConnectionId(writer, DestinationConnectionId, QuicFrameCodec.MaximumConnectionIdLength);
        WriteConnectionId(writer, SourceConnectionId, QuicFrameCodec.MaximumConnectionIdLength);
        if (Type == QuicPacketType.Initial)
        {
            writer.WriteLengthPrefixedBytes(Token.Span);
        }

        var length = (ulong)(PacketNumberLength + Payload.Length);
        writer.WriteVariableLengthInteger(length, Math.Max(2, QuicVariableLengthInteger.GetEncodedLength(length)));
        writer.WriteUInt(TruncatedPacketNumber, PacketNumberLength);
        writer.WriteBytes(Payload.Span);
    }

    private void RequireWritable()
    {
        if (Type is not (QuicPacketType.Initial or QuicPacketType.ZeroRtt or QuicPacketType.Handshake))
        {
            throw new ArgumentException($"A {Type} packet does not have this header.", nameof(Type));
        }

        if (Type != QuicPacketType.Initial && !Token.IsEmpty)
        {
            throw new ArgumentException($"A {Type} packet carries no token.", nameof(Token));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(PacketNumberLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PacketNumberLength, 4);
    }
}
