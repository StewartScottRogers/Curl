using System.Globalization;
using System.Text;

namespace Curl.Extended.Protocol.Grpc.Wire;

/// <summary>
/// Decodes the <c>grpc-message</c> value of PROTOCOL-HTTP2.md "Responses": UTF-8 text with every byte
/// outside printable ASCII, and <c>%</c> itself, percent-encoded.
/// </summary>
internal static class GrpcStatusMessage
{
    /// <summary>
    /// Decodes <paramref name="encoded" />. As the specification requires, an invalid escape never fails
    /// the decode: a <c>%</c> not followed by two hexadecimal digits is kept as it is, and bytes that are
    /// not UTF-8 become U+FFFD.
    /// </summary>
    /// <param name="encoded">The header value as sent.</param>
    /// <returns>The status message.</returns>
    public static string Decode(string encoded)
    {
        var bytes = new byte[Encoding.UTF8.GetMaxByteCount(encoded.Length)];
        var written = 0;
        var index = 0;
        while (index < encoded.Length)
        {
            if (TryReadEscape(encoded, index, out var escaped))
            {
                bytes[written++] = escaped;
                index += 3;
            }
            else
            {
                written += Encoding.UTF8.GetBytes(encoded.AsSpan(index, 1), bytes.AsSpan(written));
                index++;
            }
        }

        return Encoding.UTF8.GetString(bytes, 0, written);
    }

    private static bool TryReadEscape(string encoded, int index, out byte escaped)
    {
        escaped = 0;
        return encoded[index] == '%'
            && index + 3 <= encoded.Length
            && byte.TryParse(encoded.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out escaped);
    }
}
