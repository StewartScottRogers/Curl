namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>
/// CRC-32C (Castagnoli, reflected polynomial 0x82F63B78), which record batch v2 carries over its bytes from the
/// attributes onward: the reference broker's own, computed bit by bit so it shares no table with the client's.
/// </summary>
public static class KafkaReferenceCrc32C
{
    private const uint ReflectedPolynomial = 0x82F63B78;

    /// <summary>Computes the CRC-32C of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The checksum.</returns>
    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ (ReflectedPolynomial & (0 - (crc & 1)));
            }
        }

        return ~crc;
    }
}
