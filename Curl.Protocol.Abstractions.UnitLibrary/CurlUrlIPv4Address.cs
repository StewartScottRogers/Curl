using System.Globalization;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Reads a host as an IPv4 address the way curl 8.21.0's <c>ipv4_normalize</c> does:
/// one to four parts separated by dots, each decimal, octal after a leading <c>0</c> or
/// hexadecimal after <c>0x</c>, the last part filling the bytes that remain, as
/// <c>inet_aton</c> reads them.
/// </summary>
internal static class CurlUrlIPv4Address
{
    private const int MaximumParts = 4;

    /// <summary>
    /// The largest value each part count allows in its last part: 32, 24, 16 and 8 bits.
    /// </summary>
    private static readonly uint[] LastPartMaximum = [uint.MaxValue, 0xffffff, 0xffff, 0xff];

    /// <summary>
    /// Reads <paramref name="host" /> as an IPv4 address and prints it in dotted-quad
    /// form, or returns <see langword="false" /> when it is a name.
    /// </summary>
    public static bool TryNormalize(string host, out string address)
    {
        address = string.Empty;
        var parts = new List<uint>(MaximumParts);
        int index = 0;
        while (TryReadPart(host, ref index, out uint part))
        {
            parts.Add(part);
            if (index == host.Length)
            {
                return TryFormat(parts, out address);
            }

            if (host[index] != '.' || parts.Count == MaximumParts)
            {
                return false;
            }

            index++;
        }

        return false;
    }

    private static bool TryReadPart(string host, ref int index, out uint part)
    {
        int start = index;
        NumberStyles style = NumberStyles.None;
        int radix = 10;
        if (host.AsSpan(index).StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            start += 2;
            style = NumberStyles.AllowHexSpecifier;
            radix = 16;
        }
        else if (host.AsSpan(index).StartsWith('0'))
        {
            radix = 8;
        }

        int end = start;
        while (end < host.Length && IsDigit(host[end], radix))
        {
            end++;
        }

        index = end;

        return TryParsePart(host.AsSpan(start, end - start), style, radix, out part);
    }

    private static bool TryParsePart(ReadOnlySpan<char> digits, NumberStyles style, int radix, out uint part)
    {
        part = 0;
        if (radix != 8)
        {
            return uint.TryParse(digits, style, CultureInfo.InvariantCulture, out part);
        }

        ulong value = 0;
        foreach (char digit in digits)
        {
            value = (value * 8) + (uint)(digit - '0');
            if (value > uint.MaxValue)
            {
                return false;
            }
        }

        part = (uint)value;

        return true;
    }

    private static bool IsDigit(char character, int radix) =>
        char.IsAsciiHexDigit(character) && HexDigitValue(character) < radix;

    private static int HexDigitValue(char character) =>
        char.IsAsciiDigit(character) ? character - '0' : (character | 0x20) - 'a' + 10;

    private static bool TryFormat(List<uint> parts, out string address)
    {
        address = string.Empty;
        int last = parts.Count - 1;
        for (int index = 0; index < last; index++)
        {
            if (parts[index] > 0xff)
            {
                return false;
            }
        }

        if (parts[last] > LastPartMaximum[last])
        {
            return false;
        }

        uint value = parts[last];
        for (int index = 0; index < last; index++)
        {
            value |= parts[index] << (24 - (8 * index));
        }

        address = string.Join('.', (value >> 24) & 0xff, (value >> 16) & 0xff, (value >> 8) & 0xff, value & 0xff);

        return true;
    }
}
