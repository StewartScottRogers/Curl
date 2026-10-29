using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

/// <summary>
/// A URL set on a handle that already holds one, as upstream curl 8.22.0's <c>curl_url_set</c>
/// takes it (<c>Curl_is_absolute_url</c> and <c>redirect_url</c> in <c>lib/urlapi.c</c>, ADR-0602):
/// absolute when it starts with a scheme and a colon, else resolved against the URL held.
/// </summary>
internal static class UrlRelativeReference
{
    /// <summary>The longest scheme upstream looks for (<c>MAX_SCHEME_LEN</c>).</summary>
    private const int MaximumSchemeLength = 40;

    /// <summary>
    /// Whether <paramref name="text" /> starts with a scheme and <c>:</c>, as <c>Curl_is_absolute_url</c>
    /// says: under <c>CURLU_GUESS_SCHEME</c> or <c>CURLU_DEFAULT_SCHEME</c> the colon must be followed by <c>/</c>,
    /// and on Windows a one-letter scheme is a drive letter, so no scheme at all (CGR-255).
    /// </summary>
    /// <param name="text">The URL set.</param>
    /// <param name="flags">The set's flags.</param>
    /// <returns><see langword="true" /> when the text is an absolute URL.</returns>
    public static bool IsAbsolute(string text, CurlUrlFlags flags)
    {
        int length = SchemeLength(text);
        if (length == 0 || length == text.Length || text[length] != ':')
        {
            return false;
        }

        bool guessing = (flags & (CurlUrlFlags.GuessScheme | CurlUrlFlags.DefaultScheme)) != 0;

        return !guessing || (!IsWindowsDriveLetter(length) && text.AsSpan(length + 1).StartsWith("/", StringComparison.Ordinal));
    }

    private static bool IsWindowsDriveLetter(int schemeLength) => schemeLength == 1 && OperatingSystem.IsWindows();

    /// <summary>
    /// The URL <paramref name="relative" /> names from <paramref name="current" />, as upstream's
    /// <c>redirect_url</c> builds it: the reference's spaces and bytes outside ASCII encoded, then
    /// <c>//</c> keeping the scheme, <c>/</c> the authority, <c>#</c> or a blank reference all but the fragment, <c>?</c> the
    /// path, and anything else the path up to its last <c>/</c>.
    /// </summary>
    /// <param name="current">The URL the handle holds, with a scheme and <c>://</c>.</param>
    /// <param name="relative">The reference set.</param>
    /// <returns>The URL to parse.</returns>
    public static string Resolve(string current, string relative)
    {
        string reference = UrlPartText.Encode(relative, query: false);
        int authority = current.IndexOf("://", StringComparison.Ordinal) + 3;
        if (reference.StartsWith("//", StringComparison.Ordinal))
        {
            return current[..(authority - 2)] + reference;
        }

        if (reference.StartsWith('/'))
        {
            return current[..current.IndexOf('/', authority)] + reference;
        }

        string withoutFragment = Before(current, '#');
        if (reference.Length == 0 || reference[0] == '#')
        {
            return withoutFragment + reference;
        }

        string withoutQuery = Before(withoutFragment, '?');

        return reference.StartsWith('?')
            ? withoutQuery + reference
            : withoutQuery[..(withoutQuery.LastIndexOf('/') + 1)] + reference;
    }

    private static string Before(string text, char separator)
    {
        int index = text.IndexOf(separator);

        return index < 0 ? text : text[..index];
    }

    /// <summary>The length of the scheme <paramref name="text" /> starts with: a letter, then letters, digits, <c>+</c>, <c>-</c> or <c>.</c>.</summary>
    private static int SchemeLength(string text)
    {
        if (text.Length == 0 || !char.IsAsciiLetter(text[0]))
        {
            return 0;
        }

        int length = 1;
        while (length < MaximumSchemeLength && length < text.Length && IsSchemeCharacter(text[length]))
        {
            length++;
        }

        return length;
    }

    private static bool IsSchemeCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.';
}
