using System.Globalization;

namespace Curl.Core.Hsts;

/// <summary>
/// Reads a <c>Strict-Transport-Security</c> header value (RFC 6797) as libcurl 8.21.0's
/// <c>Curl_hsts_parse</c> does, measured on 2026-09-29 UTC (BL-620's notes, ADR-0177).
/// </summary>
/// <remarks>
/// Directives are separated by <c>;</c>, blanks (space and tab) around them skipped. A
/// directive that starts with <c>max-age</c> in any case is <c>max-age</c>: blanks, <c>=</c>,
/// blanks, then decimal digits, optionally in double quotes; no digits, a sign or a missing
/// closing quote refuses the whole value, and so does a second <c>max-age</c>. A directive
/// that starts with <c>includeSubDomains</c> in any case sets it; a second one refuses the
/// value. Anything else, and whatever follows a directive before the next <c>;</c>, is
/// skipped, so <c>max-age=60x</c> is 60 and <c>max-age=60, includeSubDomains</c> has no
/// <c>includeSubDomains</c>. A value without <c>max-age</c> is refused. A number too big for
/// 64 bits is <see cref="long.MaxValue" /> when unquoted, and refuses the value when quoted.
/// </remarks>
public static class HstsHeaderParser
{
    private const string MaxAgeName = "max-age";

    private const string IncludeSubDomainsName = "includesubdomains";

    /// <summary>Reads <paramref name="headerValue" />.</summary>
    /// <param name="headerValue">The header value, as received; a trailing CR LF is harmless.</param>
    /// <returns>What it says, or <see langword="null" /> when curl refuses it.</returns>
    public static HstsHeader? Parse(string headerValue)
    {
        ArgumentNullException.ThrowIfNull(headerValue);

        long? maxAgeSeconds = null;
        bool includeSubDomains = false;
        int position = 0;
        do
        {
            position = SkipBlanks(headerValue, position);
            if (!TryReadDirective(headerValue, ref position, ref maxAgeSeconds, ref includeSubDomains))
            {
                return null;
            }

            position = SkipBlanks(headerValue, position);
            position += position < headerValue.Length && headerValue[position] == ';' ? 1 : 0;
        }
        while (position < headerValue.Length);

        return maxAgeSeconds is null ? null : new HstsHeader(maxAgeSeconds.Value, includeSubDomains);
    }

    private static bool TryReadDirective(string text, ref int position, ref long? maxAgeSeconds, ref bool includeSubDomains)
    {
        if (StartsWith(text, position, MaxAgeName))
        {
            if (maxAgeSeconds is not null)
            {
                return false;
            }

            bool read = TryReadMaxAge(text, ref position, out long seconds);
            maxAgeSeconds = seconds;
            return read;
        }

        if (StartsWith(text, position, IncludeSubDomainsName))
        {
            position += IncludeSubDomainsName.Length;
            bool first = !includeSubDomains;
            includeSubDomains = true;
            return first;
        }

        while (position < text.Length && text[position] != ';')
        {
            position++;
        }

        return true;
    }

    private static bool TryReadMaxAge(string text, ref int position, out long seconds)
    {
        seconds = 0;
        position = SkipBlanks(text, position + MaxAgeName.Length);
        if (!TryTake(text, ref position, '='))
        {
            return false;
        }

        position = SkipBlanks(text, position);
        bool quoted = TryTake(text, ref position, '"');
        return TryReadNumber(text, ref position, out seconds) && (!quoted || TryTake(text, ref position, '"'));
    }

    /// <summary>
    /// Reads decimal digits as libcurl's <c>curlx_str_number</c> does: a number too big for 64
    /// bits reads as <see cref="long.MaxValue" /> and leaves <paramref name="position" /> on its
    /// first digit.
    /// </summary>
    private static bool TryReadNumber(string text, ref int position, out long number)
    {
        int end = position;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        if (!long.TryParse(text.AsSpan(position, end - position), NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            number = long.MaxValue;
            return end > position;
        }

        position = end;
        return true;
    }

    private static bool TryTake(string text, ref int position, char expected)
    {
        if (position < text.Length && text[position] == expected)
        {
            position++;
            return true;
        }

        return false;
    }

    private static bool StartsWith(string text, int position, string name) =>
        text.Length - position >= name.Length && AsciiText.EqualsIgnoringCase(text.AsSpan(position, name.Length), name);

    private static int SkipBlanks(string text, int position)
    {
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }
}
