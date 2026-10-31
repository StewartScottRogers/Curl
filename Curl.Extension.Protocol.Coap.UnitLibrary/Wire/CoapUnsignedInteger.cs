using System.Buffers.Binary;
using System.Numerics;

namespace Curl.Extension.Protocol.Coap.Wire;

/// <summary>
/// The <c>uint</c> option value format: a big-endian integer in as few bytes as it needs, 0 in no
/// bytes at all (RFC 7252 section 3.2).
/// </summary>
internal static class CoapUnsignedInteger
{
    /// <summary>The fewest big-endian bytes that hold <paramref name="value"/>.</summary>
    public static byte[] Encode(uint value)
    {
        int length = (32 - BitOperations.LeadingZeroCount(value) + 7) / 8;
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes[(4 - length)..];
    }

    /// <summary>The integer in <paramref name="value"/>, which may hold at most <paramref name="maximumLength"/> bytes.</summary>
    /// <exception cref="FormatException">The value is longer than <paramref name="maximumLength"/> bytes.</exception>
    public static uint Decode(ReadOnlySpan<byte> value, int maximumLength)
    {
        if (value.Length > maximumLength)
        {
            throw new FormatException($"A CoAP uint option value of {value.Length} bytes is longer than {maximumLength}.");
        }

        uint result = 0;
        foreach (byte b in value)
        {
            result = (result << 8) | b;
        }

        return result;
    }
}
