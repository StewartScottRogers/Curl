using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// A dictionary's <c>match</c> URL pattern resolved against the dictionary's URL (RFC 9842 section 2.1.1), and
/// the test of a request URL against it (section 2.2.2). It implements the URLPattern subset the RFC needs:
/// the pattern must be same-origin as the dictionary; in its path and query, <c>*</c> matches any run of
/// characters and <c>:name</c> one path segment (one query value); a pattern with no query matches any query.
/// A pattern with a regexp group (<c>(</c>) is rejected, as the RFC requires.
/// </summary>
public sealed class DictionaryMatchPattern
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly string _origin;
    private readonly Regex _path;
    private readonly Regex? _query;

    private DictionaryMatchPattern(string origin, Regex path, Regex? query)
    {
        _origin = origin;
        _path = path;
        _query = query;
    }

    /// <summary>
    /// Resolves <paramref name="match"/> against <paramref name="dictionaryUrl"/>; null when the pattern must be
    /// ignored: it does not resolve, is not same-origin as the dictionary, or holds a regexp group.
    /// </summary>
    public static DictionaryMatchPattern? Compile(Uri dictionaryUrl, string match)
    {
        if (match.Contains('(', StringComparison.Ordinal) || !Uri.TryCreate(dictionaryUrl, match, out var resolved))
        {
            return null;
        }

        var origin = OriginOf(dictionaryUrl);
        if (!string.Equals(OriginOf(resolved), origin, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = resolved.Query.Length > 1 ? ToRegex(resolved.Query[1..], segment: "[^&]*") : null;
        return new DictionaryMatchPattern(origin, ToRegex(resolved.AbsolutePath, segment: "[^/]+"), query);
    }

    /// <summary>
    /// Whether <paramref name="requestUrl"/> is same-origin as the dictionary and its path and query match the pattern
    /// (RFC 9842 section 2.2.2).
    /// </summary>
    public bool Matches(Uri requestUrl) =>
        string.Equals(OriginOf(requestUrl), _origin, StringComparison.OrdinalIgnoreCase)
        && _path.IsMatch(requestUrl.AbsolutePath)
        && (_query is null || _query.IsMatch(requestUrl.Query.TrimStart('?')));

    private static string OriginOf(Uri url) => url.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);

    private static Regex ToRegex(string pattern, string segment)
    {
        var expression = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            index = AppendPatternPart(expression, pattern, index, segment);
        }

        return new Regex(expression.Append('$').ToString(), RegexOptions.CultureInvariant, MatchTimeout);
    }

    /// <summary>Appends the expression for the pattern part at <paramref name="index"/>; returns the index of its last character.</summary>
    private static int AppendPatternPart(StringBuilder expression, string pattern, int index, string segment)
    {
        var character = pattern[index];
        if (character == '*')
        {
            expression.Append(".*");
            return index;
        }

        var nameEnd = NameEnd(pattern, index + 1);
        if (character == ':' && nameEnd > index + 1)
        {
            expression.Append(segment);
            return nameEnd - 1;
        }

        expression.Append(Regex.Escape(character.ToString()));
        return index;
    }

    private static int NameEnd(string pattern, int start)
    {
        var end = start;
        while (end < pattern.Length && IsNameCharacter(pattern[end]))
        {
            end++;
        }

        return end;
    }

    private static bool IsNameCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';
}
