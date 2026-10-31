namespace Curl.Extension.QuicMigration;

/// <summary>Reads a QUIC packet's header form from its first byte (RFC 9000 section 17).</summary>
internal static class QuicPacketForm
{
    private const byte HeaderFormBit = 0x80;
    private const byte FixedBit = 0x40;

    /// <summary>
    /// Says whether <paramref name="datagram" /> starts with a short header packet, the 1-RTT packet (RFC 9000 section
    /// 17.3.1): header form bit 0 and fixed bit 1. Only 1-RTT keys protect such a packet, so an endpoint sends and
    /// receives one only once its side of the handshake is complete.
    /// </summary>
    /// <param name="datagram">A UDP datagram's payload.</param>
    /// <returns><see langword="true" /> for a 1-RTT packet; <see langword="false" /> for a long header packet or an empty datagram.</returns>
    public static bool IsOneRtt(ReadOnlySpan<byte> datagram) =>
        !datagram.IsEmpty && (datagram[0] & (HeaderFormBit | FixedBit)) == FixedBit;
}
