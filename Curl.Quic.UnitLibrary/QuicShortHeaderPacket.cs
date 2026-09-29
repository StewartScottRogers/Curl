namespace Curl.Quic;

/// <summary>
/// A 1-RTT packet (RFC 9000 section 17.3.1): the short header, whose destination
/// connection ID has no length field, then a packet number and a payload running to the
/// end of the datagram.
/// </summary>
/// <param name="DestinationConnectionId">The destination connection ID, at most 20 bytes.</param>
/// <param name="PacketNumberLength">How many bytes of the packet number the packet carries, 1 to 4.</param>
/// <param name="TruncatedPacketNumber">Those bytes of the packet number (see <see cref="QuicPacketNumber" />).</param>
/// <param name="Payload">What follows the packet number: the frames, or once protected the ciphertext and its authentication tag.</param>
/// <param name="SpinBit">The latency spin bit (section 17.4).</param>
/// <param name="KeyPhase">The Key Phase bit (RFC 9001 section 6).</param>
/// <param name="ReservedBits">The two Reserved Bits, which must be zero once protection is removed.</param>
public sealed record QuicShortHeaderPacket(
    ReadOnlyMemory<byte> DestinationConnectionId,
    int PacketNumberLength,
    uint TruncatedPacketNumber,
    ReadOnlyMemory<byte> Payload,
    bool SpinBit = false,
    bool KeyPhase = false,
    byte ReservedBits = 0) : QuicPacket
{
    /// <inheritdoc />
    internal override void WriteTo(QuicWriter writer)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(DestinationConnectionId.Length, QuicFrameCodec.MaximumConnectionIdLength, nameof(DestinationConnectionId));
        ArgumentOutOfRangeException.ThrowIfLessThan(PacketNumberLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PacketNumberLength, 4);
        var spin = SpinBit ? 0x20 : 0;
        var keyPhase = KeyPhase ? 0x04 : 0;
        writer.WriteByte((byte)(0x40 | spin | (ReservedBits & 0x03) << 3 | keyPhase | (PacketNumberLength - 1)));
        writer.WriteBytes(DestinationConnectionId.Span);
        writer.WriteUInt(TruncatedPacketNumber, PacketNumberLength);
        writer.WriteBytes(Payload.Span);
    }
}
