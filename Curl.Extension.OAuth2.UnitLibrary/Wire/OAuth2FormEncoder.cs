using System.Buffers;
using System.Globalization;
using System.Text;

namespace Curl.Extension.OAuth2.Wire;

/// <summary>
/// Writes <c>application/x-www-form-urlencoded</c> text as RFC 6749 Appendix B says: each name and
/// value is UTF-8 encoded, then every octet but <c>A-Z a-z 0-9 - . _ *</c> is percent-encoded, with a
/// space written as <c>+</c>.
/// </summary>
public static class OAuth2FormEncoder
{
    private static readonly SearchValues<byte> UnencodedOctets =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._*"u8);

    /// <summary>Encodes the fields as <c>name=value</c> pairs joined by <c>&amp;</c>, in the order given.</summary>
    /// <param name="fields">The names and values.</param>
    /// <returns>The encoded text.</returns>
    public static string EncodeFields(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join('&', fields.Select(field => EncodeComponent(field.Key) + "=" + EncodeComponent(field.Value)));

    /// <summary>Encodes one name or value.</summary>
    /// <param name="value">The name or value.</param>
    /// <returns>The encoded text.</returns>
    public static string EncodeComponent(string value)
    {
        var encoded = new StringBuilder(value.Length);
        foreach (var octet in Encoding.UTF8.GetBytes(value))
        {
            AppendOctet(encoded, octet);
        }

        return encoded.ToString();
    }

    private static void AppendOctet(StringBuilder encoded, byte octet)
    {
        if (UnencodedOctets.Contains(octet))
        {
            encoded.Append((char)octet);
        }
        else if (octet == (byte)' ')
        {
            encoded.Append('+');
        }
        else
        {
            encoded.Append('%').Append(octet.ToString("X2", CultureInfo.InvariantCulture));
        }
    }
}
