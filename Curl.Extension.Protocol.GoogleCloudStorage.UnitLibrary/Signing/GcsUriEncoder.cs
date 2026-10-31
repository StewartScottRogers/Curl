using System.Globalization;
using System.Text;

namespace Curl.Extension.Protocol.GoogleCloudStorage.Signing;

/// <summary>
/// Percent-encodes as the Cloud Storage "V4 signing process" says of the canonical URI and query
/// string: every byte of the UTF-8 form except the unreserved characters <c>A-Z a-z 0-9 - . _ ~</c>
/// becomes <c>%XX</c> in upper-case hexadecimal.
/// </summary>
public static class GcsUriEncoder
{
    /// <summary>Encodes one path segment, a query parameter name or a query parameter value; <c>/</c> is encoded too.</summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>The encoded text.</returns>
    public static string Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var encoded = new StringBuilder(text.Length);
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            if (IsUnreserved(value))
            {
                encoded.Append((char)value);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    /// <summary>Encodes a path one segment at a time, keeping each <c>/</c>.</summary>
    /// <param name="path">The absolute path before encoding.</param>
    /// <returns>The canonical URI.</returns>
    public static string EncodePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.Join('/', path.Split('/').Select(Encode));
    }

    private static bool IsUnreserved(byte value) =>
        char.IsAsciiLetterOrDigit((char)value) || value is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~';
}
