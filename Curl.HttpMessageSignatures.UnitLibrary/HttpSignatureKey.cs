using System.Globalization;

namespace Curl.HttpMessageSignatures;

/// <summary>
/// Decodes the <c>--httpsig-key</c> text, hexadecimal digits on one line, as curl 8.22.0's
/// <c>decode_hex_key</c> does.
/// </summary>
public static class HttpSignatureKey
{
    /// <summary>The longest key text curl accepts, in characters (<c>CURL_MAX_INPUT_LENGTH</c>).</summary>
    public const int MaximumHexLength = 8_000_000;

    /// <summary>
    /// Decodes <paramref name="hexKey" /> after dropping trailing carriage returns and line feeds.
    /// </summary>
    /// <param name="hexKey">The key as hexadecimal digits, either case.</param>
    /// <returns>The key bytes.</returns>
    /// <exception cref="HttpSignatureException">The text is empty, has an odd length, is longer than
    /// <see cref="MaximumHexLength" />, or holds a character that is not a hexadecimal digit.</exception>
    public static byte[] DecodeHex(string hexKey)
    {
        var hex = hexKey.AsSpan().TrimEnd("\r\n");
        if (hex.Length == 0 || hex.Length % 2 != 0)
        {
            throw new HttpSignatureException(string.Create(CultureInfo.InvariantCulture, $"httpsig: invalid hex key (length {hex.Length})"));
        }

        if (hex.Length > MaximumHexLength)
        {
            throw new HttpSignatureException("httpsig: hex key too long");
        }

        var key = new byte[hex.Length / 2];
        for (var position = 0; position < hex.Length; position += 2)
        {
            key[position / 2] = DecodePair(hex[position], hex[position + 1], position);
        }

        return key;
    }

    private static byte DecodePair(char high, char low, int position)
    {
        if (!char.IsAsciiHexDigit(high) || !char.IsAsciiHexDigit(low))
        {
            throw new HttpSignatureException(string.Create(CultureInfo.InvariantCulture, $"httpsig: invalid hex at position {position} ('{high}{low}')"));
        }

        return (byte)((HexValue(high) << 4) | HexValue(low));
    }

    private static int HexValue(char digit) =>
        digit <= '9' ? digit - '0' : (digit | 0x20) - 'a' + 10;
}
