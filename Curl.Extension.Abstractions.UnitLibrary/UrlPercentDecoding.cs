using System.Net;
using System.Text;
using System.Text.Unicode;

namespace Curl.Extension.Abstractions;

/// <summary>
/// Percent-decodes a part of an extension protocol's URL to the bytes it names (ADR-1418). Every <c>%XX</c> becomes its
/// byte, whether or not the bytes are valid UTF-8; a <c>%</c> not followed by two hex digits, and <c>+</c>, stay as they are.
/// </summary>
public static class UrlPercentDecoding
{
    /// <summary>Gives the bytes <paramref name="escaped"/> names, its unescaped characters as UTF-8.</summary>
    /// <param name="escaped">The percent-encoded part of a URL.</param>
    /// <returns>The decoded bytes.</returns>
    public static byte[] BytesOf(string escaped)
    {
        ArgumentNullException.ThrowIfNull(escaped);

        // WebUtility reads '+' as a space, which a URL path does not mean, so '+' is escaped first.
        byte[] utf8 = Encoding.UTF8.GetBytes(escaped.Replace("+", "%2B", StringComparison.Ordinal));
        return WebUtility.UrlDecodeToBytes(utf8, 0, utf8.Length)!;
    }

    /// <summary>Gives the text <paramref name="escaped"/> names, for a field that can only hold text.</summary>
    /// <param name="escaped">The percent-encoded part of a URL.</param>
    /// <returns>The decoded text, or <see langword="null"/> when the decoded bytes are not valid UTF-8.</returns>
    public static string? Utf8TextOf(string escaped)
    {
        byte[] bytes = BytesOf(escaped);
        return Utf8.IsValid(bytes) ? Encoding.UTF8.GetString(bytes) : null;
    }
}
