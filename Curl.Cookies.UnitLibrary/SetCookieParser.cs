using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Reads one <c>Set-Cookie</c> header into a <see cref="Cookie"/> the way libcurl 8.21.0 does, or
/// refuses it where curl drops it. Every rule here was measured on curl 8.21.0 on 2026-09-26 by
/// serving the header from a loopback server and reading curl's <c>-c</c> jar, as
/// <c>SetCookieParserTests</c> records.
/// </summary>
/// <remarks>
/// The Public Suffix List is not consulted here: a <c>Domain</c> that is a public suffix is refused
/// by <see cref="CookieStore"/>, not by this parser.
/// </remarks>
public static class SetCookieParser
{
    /// <summary>
    /// The longest header value curl reads, counted from just after the colon, leading whitespace
    /// included; a longer one is dropped whole.
    /// </summary>
    public const int LongestHeaderValue = 4998;

    /// <summary>The most characters the cookie's name and value may hold between them.</summary>
    public const int LongestNameAndValue = 4096;

    /// <summary>An <c>Expires</c> value this long or longer is ignored, as if it were absent.</summary>
    private const int ShortestIgnoredExpires = 80;

    /// <summary>curl caps a cookie's lifetime at 400 days from the time it is received.</summary>
    private const long LongestLifetimeSeconds = 400L * 24 * 60 * 60;

    /// <summary>What curl adds to the capped expiry before rounding it down to a whole minute.</summary>
    private const long CappedExpiryRoundingSeconds = 30;

    /// <summary>curl's expiry for a cookie that arrived already expired.</summary>
    private const long AlreadyExpired = 1;

    /// <summary>Reads <paramref name="headerValue"/>, received in answer to <paramref name="requestUrl"/>, as curl does.</summary>
    /// <remarks>
    /// <para>
    /// The value is split at <c>;</c>. The first part is the cookie: a name, <c>=</c>, and a value,
    /// both trimmed; a part without <c>=</c> or with an empty name is refused, and so is a name and
    /// value longer than <see cref="LongestNameAndValue"/> together. A value keeps its quotes. Each
    /// later part is an attribute, its name read case-insensitively up to <c>=</c> or a tab:
    /// </para>
    /// <list type="bullet">
    /// <item><c>Secure</c> and <c>HttpOnly</c> count only without <c>=</c>; <c>Secure</c> from an
    /// origin that is not secure (not <c>https</c>, <c>wss</c>, <c>localhost</c>, <c>127.0.0.1</c> or
    /// <c>::1</c>) refuses the cookie.</item>
    /// <item><c>Path</c>, <c>Domain</c>, <c>Max-Age</c> and <c>Expires</c> count only with <c>=</c>
    /// and a value that is not empty; the last <c>Path</c> wins, and every <c>Domain</c> must match.</item>
    /// <item><c>Path</c> loses one leading quote and then one trailing one, becomes <c>/</c> when it
    /// does not start with <c>/</c>, and loses one trailing <c>/</c> unless it is <c>/</c>. Without one,
    /// the path is the request path up to its last <c>/</c>, or <c>/</c>.</item>
    /// <item><c>Domain</c> loses one leading dot and must then be the request host or a parent of it,
    /// compared case-insensitively, or the cookie is refused; for an IP-address host it must be the
    /// address itself and the cookie stays host-only.</item>
    /// <item><c>Max-Age</c> is read as leading digits after an optional quote, and always wins over
    /// <c>Expires</c>; no digits, or <c>0</c>, means already expired, and too many means the cap.</item>
    /// <item><c>Expires</c> counts only when no <c>Max-Age</c> or earlier date was read and it is
    /// shorter than 80 characters; a text <see cref="CurlDateParser"/> refuses is ignored, and a date
    /// at or before the epoch means already expired.</item>
    /// <item>Any other attribute is ignored.</item>
    /// </list>
    /// <para>
    /// A tab inside a trimmed value refuses the cookie; a part that ends at a tab ends the reading, the
    /// rest ignored. A control character other than tab anywhere refuses the cookie. An expiry more
    /// than 400 days after <paramref name="now"/> becomes 400 days and 30 seconds after it, rounded down
    /// to the minute. A name starting <c>__Secure-</c> (that case only) must be <c>Secure</c>, and one
    /// starting <c>__Host-</c> must be <c>Secure</c>, have the path <c>/</c> and no host-name
    /// <c>Domain</c>.
    /// </para>
    /// </remarks>
    /// <param name="headerValue">
    /// The <c>Set-Cookie</c> header's value as received, everything after the colon, without the line
    /// ending.
    /// </param>
    /// <param name="requestUrl">The request the header answered; it gives the host, the path and whether the origin is secure.</param>
    /// <param name="now">The time the header was received, which <c>Max-Age</c> counts from.</param>
    /// <returns>The cookie; <see langword="null"/> when curl drops it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> or <paramref name="requestUrl"/> is <see langword="null"/>.</exception>
    public static Cookie? Parse(string headerValue, CurlUrl requestUrl, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headerValue);
        ArgumentNullException.ThrowIfNull(requestUrl);

        return ParseFor(headerValue, requestUrl, now);
    }

    /// <summary>
    /// Reads <paramref name="headerValue"/> from a <c>Set-Cookie:</c> line of a cookie file, which answered
    /// no request, as curl 8.21.0 does.
    /// </summary>
    /// <remarks>
    /// The rules are <see cref="Parse"/>'s, except where they need the request: <c>Secure</c> is always
    /// accepted; any <c>Domain</c> is accepted, loses one leading dot and includes subdomains, even an IP
    /// address; without one the domain is empty, and the cookie is sent to every host and never written
    /// to the jar; without a <c>Path</c> the path is empty, which sorts before <c>/</c> and matches every path.
    /// </remarks>
    /// <param name="headerValue">Everything after the <c>Set-Cookie:</c> prefix and the blanks that follow it, without the line ending.</param>
    /// <param name="now">The time the file is read, which <c>Max-Age</c> counts from and the 400-day cap applies to.</param>
    /// <returns>The cookie; <see langword="null"/> when curl drops it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> is <see langword="null"/>.</exception>
    public static Cookie? ParseFromCookieFile(string headerValue, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(headerValue);

        return ParseFor(headerValue, requestUrl: null, now);
    }

    private static Cookie? ParseFor(string headerValue, CurlUrl? requestUrl, DateTimeOffset now)
    {
        if (headerValue.Length > LongestHeaderValue || CookieFieldRules.ContainsRefusedControlCharacter(headerValue))
        {
            return null;
        }

        CookieUnderConstruction cookie = new(requestUrl, now.ToUnixTimeSeconds());
        return TryReadParts(headerValue, cookie) ? cookie.Finish() : null;
    }

    /// <summary>Reads every part in turn; <see langword="false"/> as soon as one refuses the cookie.</summary>
    private static bool TryReadParts(string text, CookieUnderConstruction cookie)
    {
        int position = 0;
        bool isFirstPart = true;
        while (true)
        {
            if (!TryReadPart(text, ref position, out HeaderPart part))
            {
                return false;
            }

            bool accepted = isFirstPart ? cookie.TrySetNameAndValue(part) : cookie.TryApplyAttribute(part);
            if (!accepted)
            {
                return false;
            }

            isFirstPart = false;
            if (position >= text.Length || text[position] != ';')
            {
                return true;
            }

            position++;
        }
    }

    /// <summary>
    /// Reads one part from <paramref name="position"/>, leaving <paramref name="position"/> on the
    /// character that ended it; <see langword="false"/> when its value holds a tab.
    /// </summary>
    private static bool TryReadPart(string text, ref int position, out HeaderPart part)
    {
        int nameEnd = text.IndexOfAny([';', '\t', '='], position);
        nameEnd = nameEnd < 0 ? text.Length : nameEnd;
        string name = text[position..nameEnd].Trim(' ');
        bool hasEquals = nameEnd < text.Length && text[nameEnd] == '=';
        position = nameEnd;
        string value = hasEquals ? ReadValue(text, ref position) : string.Empty;
        part = new HeaderPart(name, hasEquals, value);
        return !value.Contains('\t', StringComparison.Ordinal);
    }

    /// <summary>Reads from just after the <c>=</c> at <paramref name="position"/> up to the next <c>;</c>, trimmed.</summary>
    private static string ReadValue(string text, ref int position)
    {
        int valueStart = position + 1;
        int valueEnd = text.IndexOf(';', valueStart);
        position = valueEnd < 0 ? text.Length : valueEnd;
        return text[valueStart..position].Trim(' ', '\t');
    }

    /// <summary>One <c>;</c>-separated part: its trimmed name, whether <c>=</c> followed it, and its trimmed value.</summary>
    private readonly struct HeaderPart(string name, bool hasEquals, string value)
    {
        public string Name { get; } = name;

        public bool HasEquals { get; } = hasEquals;

        public string Value { get; } = value;
    }

    /// <summary>
    /// The fields read so far, as curl fills its <c>struct Cookie</c> while it parses; <paramref name="requestUrl"/>
    /// is <see langword="null"/> for a line of a cookie file.
    /// </summary>
    private sealed class CookieUnderConstruction(CurlUrl? requestUrl, long nowUnixSeconds)
    {
        private readonly string? host = requestUrl is null ? null : CookieOrigin.HostOf(requestUrl);

        private readonly bool hostIsIpAddress = requestUrl is not null && CookieOrigin.IsIpAddress(requestUrl);

        private string name = string.Empty;

        private string value = string.Empty;

        private string? domain;

        private bool includesSubdomains;

        private string? path;

        private bool isSecure;

        private bool isHttpOnly;

        private long expiresUnixSeconds;

        public bool TrySetNameAndValue(HeaderPart part)
        {
            name = part.Name;
            value = part.Value;
            return part.HasEquals && name.Length > 0 && name.Length + value.Length <= LongestNameAndValue;
        }

        public bool TryApplyAttribute(HeaderPart part)
        {
            if (!part.HasEquals)
            {
                return TryApplyFlag(part.Name);
            }

            return part.Value.Length == 0 || TryApplyValuedAttribute(part.Name.ToLowerInvariant(), part.Value);
        }

        private bool TryApplyFlag(string flag)
        {
            if (flag.Equals("secure", StringComparison.OrdinalIgnoreCase))
            {
                isSecure = true;
                return requestUrl is null || CookieOrigin.IsSecure(requestUrl);
            }

            isHttpOnly |= flag.Equals("httponly", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private bool TryApplyValuedAttribute(string attribute, string attributeValue)
        {
            switch (attribute)
            {
                case "path":
                    path = CookieFieldRules.SanitizePath(attributeValue);
                    return true;
                case "domain":
                    return TrySetDomain(attributeValue);
                case "max-age":
                    expiresUnixSeconds = ExpiryFromMaxAge(attributeValue);
                    return true;
                case "expires":
                    SetExpiryFromExpires(attributeValue);
                    return true;
                default:
                    return true;
            }
        }

        private bool TrySetDomain(string attributeValue)
        {
            string candidate = attributeValue.StartsWith('.') ? attributeValue[1..] : attributeValue;
            if (host is null)
            {
                domain = candidate;
                includesSubdomains = true;
                return true;
            }

            bool matches = hostIsIpAddress ? string.Equals(candidate, host, StringComparison.Ordinal) : CookieOrigin.IsDomainOrSubdomain(candidate, host);
            domain = candidate;
            includesSubdomains = !hostIsIpAddress;
            return matches;
        }

        private long ExpiryFromMaxAge(string attributeValue)
        {
            string unquoted = attributeValue.StartsWith('"') ? attributeValue[1..] : attributeValue;
            string digits = new([.. unquoted.TakeWhile(char.IsAsciiDigit)]);
            if (digits.Length == 0)
            {
                return AlreadyExpired;
            }

            if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
            {
                return long.MaxValue;
            }

            return seconds == 0 ? AlreadyExpired : AddCapped(seconds);
        }

        private long AddCapped(long seconds) => long.MaxValue - nowUnixSeconds < seconds ? long.MaxValue : nowUnixSeconds + seconds;

        private void SetExpiryFromExpires(string attributeValue)
        {
            if (expiresUnixSeconds != 0 || attributeValue.Length >= ShortestIgnoredExpires || !CurlDateParser.TryParse(attributeValue, out long seconds))
            {
                return;
            }

            expiresUnixSeconds = seconds <= 0 ? AlreadyExpired : seconds;
        }

        public Cookie? Finish()
        {
            string finalPath = path ?? (requestUrl is null ? string.Empty : DefaultPath(requestUrl.AbsolutePath));
            if (!CookieFieldRules.SatisfiesNamePrefix(name, isSecure, finalPath, includesSubdomains))
            {
                return null;
            }

            return new Cookie(name, value, domain ?? host, includesSubdomains, finalPath, isSecure, isHttpOnly, CapExpiry(expiresUnixSeconds));
        }

        /// <summary>The request path up to, not including, its last <c>/</c>; <c>/</c> when that leaves nothing.</summary>
        private static string DefaultPath(string requestPath)
        {
            int lastSlash = requestPath.LastIndexOf('/');
            return lastSlash > 0 ? requestPath[..lastSlash] : "/";
        }

        /// <summary>curl's <c>cap_expires</c>: no cookie outlives 400 days from now, rounded down to the minute.</summary>
        private long CapExpiry(long expiry)
        {
            long cap = nowUnixSeconds + LongestLifetimeSeconds;
            return expiry > cap ? (cap + CappedExpiryRoundingSeconds) / 60 * 60 : expiry;
        }
    }
}
