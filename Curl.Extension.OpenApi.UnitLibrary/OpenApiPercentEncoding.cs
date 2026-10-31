using System.Text;

namespace Curl.Extension.OpenApi;

/// <summary>Percent-encodes parameter names and values for a URL (OpenAPI 3.1 §4.8.12.4, RFC 3986 §2).</summary>
internal static class OpenApiPercentEncoding
{
    private const string Reserved = ":/?#[]@!$&'()*+,;=";

    /// <summary>Encodes every character but RFC 3986's unreserved ones, and its reserved ones too when they are allowed.</summary>
    /// <param name="text">The name or value.</param>
    /// <param name="allowReserved">Whether reserved characters stay as they are (<c>allowReserved</c>, §4.8.12.1).</param>
    /// <returns>The encoded text.</returns>
    internal static string Encode(string text, bool allowReserved)
    {
        if (!allowReserved)
        {
            return Uri.EscapeDataString(text);
        }

        StringBuilder encoded = new();
        foreach (Rune rune in text.EnumerateRunes())
        {
            string character = rune.ToString();
            encoded.Append(Reserved.Contains(character, StringComparison.Ordinal) ? character : Uri.EscapeDataString(character));
        }

        return encoded.ToString();
    }
}
