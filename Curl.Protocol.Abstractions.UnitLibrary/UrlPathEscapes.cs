namespace Curl.Protocol.Abstractions;

/// <summary>
/// Writes the percent-escapes of a URL path as curl 8.22.0 sends them to the origin: each
/// <c>%</c> followed by two hex digits with those digits in upper case, so <c>/a%2fb%7e</c>
/// goes on the wire as <c>/a%2Fb%7E</c>. An escape that is not two hex digits (<c>%zz</c>,
/// a trailing <c>%a</c>) is left as it is (ADR-0821, CGR-054).
/// </summary>
public static class UrlPathEscapes
{
    /// <summary>Returns <paramref name="path" /> with the hex digits of each <c>%XX</c> escape upper cased.</summary>
    /// <param name="path">The path, as typed or as encoded for the wire.</param>
    /// <returns>The path with upper-case escapes, or <paramref name="path" /> itself when it holds no <c>%</c>.</returns>
    public static string WithUpperCaseHexDigits(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int percent = path.IndexOf('%');
        if (percent < 0)
        {
            return path;
        }

        char[] characters = path.ToCharArray();
        for (int index = percent; index + 2 < characters.Length; index++)
        {
            if (IsEscape(characters, index))
            {
                characters[index + 1] = char.ToUpperInvariant(characters[index + 1]);
                characters[index + 2] = char.ToUpperInvariant(characters[index + 2]);
                index += 2;
            }
        }

        return new string(characters);
    }

    private static bool IsEscape(char[] characters, int index) =>
        characters[index] == '%' && char.IsAsciiHexDigit(characters[index + 1]) && char.IsAsciiHexDigit(characters[index + 2]);
}
