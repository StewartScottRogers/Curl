namespace Curl.Extension.WireGuard.IpStack;

/// <summary>The Internet checksum of RFC 1071: the ones' complement of the ones' complement sum of 16-bit words.</summary>
public static class InternetChecksum
{
    /// <summary>Adds <paramref name="data" /> to a running sum as big-endian 16-bit words, padding an odd last byte with zero.</summary>
    /// <param name="sum">The sum so far, unfolded.</param>
    /// <param name="data">The bytes to add; only the last span added may have an odd length.</param>
    /// <returns>The new unfolded sum.</returns>
    public static uint Add(uint sum, ReadOnlySpan<byte> data)
    {
        var index = 0;
        for (; index + 1 < data.Length; index += 2)
        {
            sum += (uint)((data[index] << 8) | data[index + 1]);
        }

        if (index < data.Length)
        {
            sum += (uint)(data[index] << 8);
        }

        return sum;
    }

    /// <summary>Folds the carries of a running sum into 16 bits and complements it.</summary>
    /// <param name="sum">The unfolded sum.</param>
    /// <returns>The checksum to write, or zero when the sum covered a correct checksum.</returns>
    public static ushort Finish(uint sum)
    {
        while (sum > 0xFFFF)
        {
            sum = (sum & 0xFFFF) + (sum >> 16);
        }

        return (ushort)~sum;
    }

    /// <summary>The checksum of one run of bytes.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The checksum.</returns>
    public static ushort Compute(ReadOnlySpan<byte> data) => Finish(Add(0, data));
}
