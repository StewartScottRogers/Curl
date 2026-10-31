namespace Curl.Extension.Protocol.Kafka.Wire;

/// <summary>
/// CRC-32C (Castagnoli, reflected polynomial <c>0x82F63B78</c>), the checksum a record batch carries ("Record Batch":
/// "The CRC is the CRC32-C ... covering the data from the attributes to the end of the batch").
/// </summary>
internal static class Crc32C
{
    private static readonly uint[] Table = CreateTable();

    /// <summary>Gives the CRC-32C of <paramref name="bytes"/>.</summary>
    internal static uint Compute(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte next in bytes)
        {
            crc = Table[(crc ^ next) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint entry = index;
            for (int bit = 0; bit < 8; bit++)
            {
                entry = (entry >> 1) ^ ((entry & 1) * 0x82F63B78);
            }

            table[index] = entry;
        }

        return table;
    }
}
