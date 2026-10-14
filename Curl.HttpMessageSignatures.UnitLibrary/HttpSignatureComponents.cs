using System.Globalization;

namespace Curl.HttpMessageSignatures;

/// <summary>
/// The RFC 9421 component identifiers curl 8.22.0 signs, from the <c>--httpsig-headers</c> text
/// or the default set, as upstream's <c>parse_components</c> builds them.
/// </summary>
public static class HttpSignatureComponents
{
    /// <summary>The most components curl signs (<c>HTTPSIG_MAX_COMPONENTS</c>).</summary>
    public const int MaximumCount = 16;

    /// <summary>The <c>@method</c> derived component (RFC 9421 section 2.2.1).</summary>
    public const string Method = "@method";

    /// <summary>The <c>@authority</c> derived component (RFC 9421 section 2.2.3).</summary>
    public const string Authority = "@authority";

    /// <summary>The <c>@path</c> derived component (RFC 9421 section 2.2.6).</summary>
    public const string Path = "@path";

    /// <summary>The <c>@query</c> derived component (RFC 9421 section 2.2.7).</summary>
    public const string Query = "@query";

    /// <summary>
    /// Returns the component identifiers to sign. With no <paramref name="componentsText" />, or an
    /// empty one, that is <c>@method</c>, <c>@authority</c>, <c>@path</c> and, when
    /// <paramref name="urlHasQuery" />, <c>@query</c>. Otherwise each blank-separated word is a bare
    /// derived component name (<c>method</c>, <c>authority</c>, <c>path</c>, <c>query</c>) or a
    /// header field name ending in a colon, both matched and kept in lower case.
    /// </summary>
    /// <param name="componentsText">The <c>--httpsig-headers</c> text, or <see langword="null" />.</param>
    /// <param name="urlHasQuery">Whether the request URL has a query.</param>
    /// <returns>The identifiers in signing order, such as <c>@method</c> or <c>content-type</c>.</returns>
    /// <exception cref="HttpSignatureException">A word holds a quote or backslash, names an unknown
    /// derived component or an empty header, a component repeats, there are none, or there are
    /// more than <see cref="MaximumCount" />.</exception>
    public static IReadOnlyList<string> Parse(string? componentsText, bool urlHasQuery)
    {
        if (string.IsNullOrEmpty(componentsText))
        {
            return DefaultComponents(urlHasQuery);
        }

        var components = ReadComponents(componentsText);
        RejectDuplicates(components);
        return components;
    }

    private static string[] DefaultComponents(bool urlHasQuery) =>
        urlHasQuery ? [Method, Authority, Path, Query] : [Method, Authority, Path];

    private static List<string> ReadComponents(string componentsText)
    {
        var components = new List<string>();
        var position = 0;
        while (components.Count < MaximumCount && TryReadWord(componentsText, ref position, out var word))
        {
            components.Add(ToIdentifier(word));
        }

        if (components.Count == 0)
        {
            throw new HttpSignatureException("httpsig: no signature components specified");
        }

        if (position < componentsText.Length)
        {
            throw new HttpSignatureException(string.Create(CultureInfo.InvariantCulture, $"httpsig: too many signature components (max {MaximumCount})"));
        }

        return components;
    }

    // Reads the next word after any blanks and steps past the one blank that ends it, as
    // upstream's loop does, so text left over after the last word it reads means "too many".
    private static bool TryReadWord(string text, ref int position, out string word)
    {
        while (position < text.Length && IsBlank(text[position]))
        {
            position++;
        }

        var start = position;
        while (position < text.Length && !IsBlank(text[position]))
        {
            RejectQuoteOrBackslash(text[position]);
            position++;
        }

        word = text[start..position];
        position = Math.Min(position + 1, text.Length);
        return word.Length > 0;
    }

    private static void RejectQuoteOrBackslash(char character)
    {
        if (character is '"' or '\\')
        {
            throw new HttpSignatureException(HttpSignatureException.BadArgumentMessage);
        }
    }

    private static string ToIdentifier(string word)
    {
        var lower = ToAsciiLower(word);
        if (lower.EndsWith(':'))
        {
            return ToHeaderIdentifier(lower[..^1]);
        }

        return lower switch
        {
            "method" => Method,
            "authority" => Authority,
            "path" => Path,
            "query" => Query,
            _ => throw new HttpSignatureException($"httpsig: unknown component '{lower}'"),
        };
    }

    private static string ToHeaderIdentifier(string name) =>
        name.Length == 0 ? throw new HttpSignatureException("httpsig: empty header component name") : name;

    private static void RejectDuplicates(List<string> components)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            if (!seen.Add(component))
            {
                throw new HttpSignatureException($"httpsig: duplicate signature component '{component}'");
            }
        }
    }

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private static string ToAsciiLower(string word) =>
        string.Create(word.Length, word, static (destination, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                destination[index] = char.IsAsciiLetterUpper(source[index]) ? (char)(source[index] | 0x20) : source[index];
            }
        });
}
