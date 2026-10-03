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

    /// <summary>The characters that end a part's name: <c>;</c>, tab and <c>=</c>.</summary>
    private const string NameEnds = ";\t=";

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
    /// A part is checked before it is read: a control character other than tab in its name, or a tab or
    /// other control character inside its trimmed value, refuses the cookie. A part that ends at a tab
    /// ends the reading, the rest ignored and not checked. An expiry more
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
    public static Cookie? Parse(string headerValue, CurlUrl requestUrl, DateTimeOffset now) => Parse(headerValue, requestUrl, now, out _);

    /// <summary>
    /// Reads <paramref name="headerValue"/> as <see cref="Parse(string, CurlUrl, DateTimeOffset)"/> does, and
    /// gives the line curl 8.21.0 prints under <c>-v</c> when it drops the cookie. Measured on 2026-09-27
    /// (BL-443).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>invalid octets in name, cookie dropped</c>: a control character other than tab in a part's name.</item>
    /// <item><c>invalid octets in value, cookie dropped</c>: a tab or other control character inside a part's trimmed value.</item>
    /// <item><c>invalid cookie, dropped</c>: the first part has no <c>=</c>, or an empty name.</item>
    /// <item><c>skipped cookie because not 'secure'</c>: <c>Secure</c> from an origin that is not secure.</item>
    /// <item><c>skipped cookie with bad tailmatch domain: </c> and the rest of the header from the
    /// <c>Domain</c> value that failed to match, its leading blanks and one leading dot left out.</item>
    /// <item><c>oversized cookie dropped, name/val </c><i>n</i><c> + </c><i>v</i><c> bytes</c>: the trimmed
    /// name's <i>n</i> and value's <i>v</i> characters are more than <see cref="LongestNameAndValue"/> together
    /// (measured on curl 8.21.0, 2026-10-02, BL-1225: a 4000-byte name and a 97-byte value print
    /// <c>oversized cookie dropped, name/val 4000 + 97 bytes</c>).</item>
    /// </list>
    /// <para>
    /// curl prints nothing for a header longer than <see cref="LongestHeaderValue"/>, or a name whose
    /// <c>__Secure-</c> or <c>__Host-</c> prefix is not satisfied, so <paramref name="refusal"/> is
    /// <see langword="null"/> for those.
    /// </para>
    /// </remarks>
    /// <param name="headerValue">As for <see cref="Parse(string, CurlUrl, DateTimeOffset)"/>.</param>
    /// <param name="requestUrl">As for <see cref="Parse(string, CurlUrl, DateTimeOffset)"/>.</param>
    /// <param name="now">As for <see cref="Parse(string, CurlUrl, DateTimeOffset)"/>.</param>
    /// <param name="refusal">
    /// The <c>-v</c> line, without curl's <c>* </c>, when curl drops the cookie with one; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <returns>The cookie; <see langword="null"/> when curl drops it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> or <paramref name="requestUrl"/> is <see langword="null"/>.</exception>
    public static Cookie? Parse(string headerValue, CurlUrl requestUrl, DateTimeOffset now, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(headerValue);
        ArgumentNullException.ThrowIfNull(requestUrl);

        return ParseFor(headerValue, requestUrl, now, out refusal);
    }

    /// <summary>
    /// Reads <paramref name="headerValue"/> from a <c>Set-Cookie:</c> line of a cookie file, which answered
    /// no request, as curl 8.21.0 does.
    /// </summary>
    /// <remarks>
    /// The rules are <see cref="Parse(string, CurlUrl, DateTimeOffset)"/>'s, except where they need the request: <c>Secure</c> is always
    /// accepted; any <c>Domain</c> is accepted, loses one leading dot and includes subdomains, even an IP
    /// address; without one the domain is empty, and the cookie is sent to every host and never written
    /// to the jar; without a <c>Path</c> the path is empty, which sorts before <c>/</c> and matches every path.
    /// </remarks>
    /// <param name="headerValue">Everything after the <c>Set-Cookie:</c> prefix and the blanks that follow it, without the line ending.</param>
    /// <param name="now">The time the file is read, which <c>Max-Age</c> counts from and the 400-day cap applies to.</param>
    /// <returns>The cookie; <see langword="null"/> when curl drops it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> is <see langword="null"/>.</exception>
    public static Cookie? ParseFromCookieFile(string headerValue, DateTimeOffset now) => ParseFromCookieFile(headerValue, now, out _);

    /// <summary>
    /// Reads <paramref name="headerValue"/> as <see cref="ParseFromCookieFile(string, DateTimeOffset)"/> does, and
    /// gives the line curl 8.21.0 prints under <c>-v</c> when it drops the cookie. Measured on 2026-09-27
    /// (BL-461).
    /// </summary>
    /// <remarks>
    /// The lines are <see cref="Parse(string, CurlUrl, DateTimeOffset, out string?)"/>'s for the invalid octets
    /// and for a first part with no <c>=</c> or a blank name; <c>Secure</c> and <c>Domain</c> never refuse a
    /// cookie file line, and a first part with no name at all is skipped without a line.
    /// </remarks>
    /// <param name="headerValue">As for <see cref="ParseFromCookieFile(string, DateTimeOffset)"/>.</param>
    /// <param name="now">As for <see cref="ParseFromCookieFile(string, DateTimeOffset)"/>.</param>
    /// <param name="refusal">
    /// The <c>-v</c> line, without curl's <c>* </c>, when curl drops the cookie with one; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <returns>The cookie; <see langword="null"/> when curl drops it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerValue"/> is <see langword="null"/>.</exception>
    public static Cookie? ParseFromCookieFile(string headerValue, DateTimeOffset now, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(headerValue);

        return ParseFor(headerValue, requestUrl: null, now, out refusal);
    }

    private static Cookie? ParseFor(string headerValue, CurlUrl? requestUrl, DateTimeOffset now, out string? refusal)
    {
        refusal = null;
        if (headerValue.Length > LongestHeaderValue)
        {
            return null;
        }

        CookieUnderConstruction cookie = new(headerValue, requestUrl, now.ToUnixTimeSeconds());
        if (!TryReadParts(headerValue, cookie))
        {
            refusal = cookie.Refusal;
            return null;
        }

        return cookie.Finish();
    }

    /// <summary>Reads every part in turn; <see langword="false"/> as soon as one refuses the cookie.</summary>
    /// <remarks>
    /// A part that starts with no name at all - at a <c>;</c>, a tab, a <c>=</c> or the end - is skipped,
    /// and the reading stops there unless a <c>;</c> follows at once: <c>a=b;=x; Path=/q</c> keeps the default
    /// path. A header's first part is the exception, refused as <c>invalid cookie, dropped</c>; a cookie
    /// file line skips it too, so <c>;a=b</c> there is the cookie <c>a=b</c> and <c>=v</c> is dropped
    /// silently (measured 2026-09-27, BL-461 Notes).
    /// </remarks>
    private static bool TryReadParts(string text, CookieUnderConstruction cookie)
    {
        int position = 0;
        bool isFirstPart = true;
        while (true)
        {
            if (!SkipsNamelessPart(text, position, isFirstPart, cookie))
            {
                HeaderPart part = ReadPart(text, ref position);
                if (!cookie.TryApplyPart(part, isFirstPart))
                {
                    return false;
                }

                isFirstPart = false;
            }

            if (!IsSemicolonAt(text, position))
            {
                return !isFirstPart;
            }

            position++;
        }
    }

    /// <summary>
    /// Tells whether the part at <paramref name="position"/> is skipped: it has no name - it starts at a <c>;</c>, a
    /// tab, a <c>=</c> or the end - and is not a header's first part.
    /// </summary>
    private static bool SkipsNamelessPart(string text, int position, bool isFirstPart, CookieUnderConstruction cookie) =>
        StartsNameless(text, position) && (!isFirstPart || cookie.IsFromCookieFile);

    /// <summary>Tells whether the part at <paramref name="position"/> starts at a <c>;</c>, a tab, a <c>=</c> or the end.</summary>
    private static bool StartsNameless(string text, int position) =>
        position >= text.Length || NameEnds.Contains(text[position], StringComparison.Ordinal);

    /// <summary>Tells whether a <c>;</c> is at <paramref name="position"/>.</summary>
    private static bool IsSemicolonAt(string text, int position) =>
        position < text.Length && text[position] == ';';

    /// <summary>
    /// Reads one part from <paramref name="position"/>, leaving <paramref name="position"/> on the
    /// character that ended it.
    /// </summary>
    private static HeaderPart ReadPart(string text, ref int position)
    {
        int nameEnd = text.IndexOfAny([';', '\t', '='], position);
        nameEnd = nameEnd < 0 ? text.Length : nameEnd;
        string name = text[position..nameEnd].Trim(' ');
        bool hasEquals = nameEnd < text.Length && text[nameEnd] == '=';
        position = nameEnd;
        if (!hasEquals)
        {
            return new HeaderPart(name, hasEquals: false, string.Empty, nameEnd);
        }

        string value = ReadValue(text, ref position, out int valueStart);
        return new HeaderPart(name, hasEquals: true, value, valueStart);
    }

    /// <summary>
    /// Reads from just after the <c>=</c> at <paramref name="position"/> up to the next <c>;</c>, trimmed;
    /// <paramref name="valueStart"/> is where the trimmed value starts in <paramref name="text"/>.
    /// </summary>
    private static string ReadValue(string text, ref int position, out int valueStart)
    {
        int untrimmedStart = position + 1;
        int valueEnd = text.IndexOf(';', untrimmedStart);
        position = valueEnd < 0 ? text.Length : valueEnd;
        string withoutLeadingBlanks = text[untrimmedStart..position].TrimStart(' ', '\t');
        valueStart = position - withoutLeadingBlanks.Length;
        return withoutLeadingBlanks.TrimEnd(' ', '\t');
    }

    /// <summary>
    /// One <c>;</c>-separated part: its trimmed name, whether <c>=</c> followed it, its trimmed value, and
    /// where that value starts in the header.
    /// </summary>
    private readonly struct HeaderPart(string name, bool hasEquals, string value, int valueStart)
    {
        public string Name { get; } = name;

        public bool HasEquals { get; } = hasEquals;

        public string Value { get; } = value;

        public int ValueStart { get; } = valueStart;
    }

    /// <summary>
    /// The fields read so far from <paramref name="headerValue"/>, as curl fills its <c>struct Cookie</c> while it
    /// parses; <paramref name="requestUrl"/> is <see langword="null"/> for a line of a cookie file.
    /// </summary>
    private sealed class CookieUnderConstruction(string headerValue, CurlUrl? requestUrl, long nowUnixSeconds)
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

        /// <summary>The <c>-v</c> line curl prints for the refusal; <see langword="null"/> when it prints none.</summary>
        public string? Refusal { get; private set; }

        /// <summary>Gets whether the header is a line of a cookie file, which answered no request.</summary>
        public bool IsFromCookieFile => requestUrl is null;

        /// <summary>Checks <paramref name="part"/>'s octets, then reads it as the cookie or as an attribute.</summary>
        public bool TryApplyPart(HeaderPart part, bool isFirstPart)
        {
            if (CookieFieldRules.ContainsRefusedControlCharacter(part.Name))
            {
                return Refuse("invalid octets in name, cookie dropped");
            }

            if (part.Value.Contains('\t', StringComparison.Ordinal) || CookieFieldRules.ContainsRefusedControlCharacter(part.Value))
            {
                return Refuse("invalid octets in value, cookie dropped");
            }

            return isFirstPart ? TrySetNameAndValue(part) : TryApplyAttribute(part);
        }

        private bool Refuse(string refusal)
        {
            Refusal = refusal;
            return false;
        }

        private bool TrySetNameAndValue(HeaderPart part)
        {
            name = part.Name;
            value = part.Value;
            if (!part.HasEquals || name.Length == 0)
            {
                return Refuse("invalid cookie, dropped");
            }

            return name.Length + value.Length <= LongestNameAndValue
                || Refuse(string.Create(CultureInfo.InvariantCulture, $"oversized cookie dropped, name/val {name.Length} + {value.Length} bytes"));
        }

        private bool TryApplyAttribute(HeaderPart part)
        {
            if (!part.HasEquals)
            {
                return TryApplyFlag(part.Name);
            }

            return part.Value.Length == 0 || TryApplyValuedAttribute(part.Name.ToLowerInvariant(), part);
        }

        private bool TryApplyFlag(string flag)
        {
            if (flag.Equals("secure", StringComparison.OrdinalIgnoreCase))
            {
                isSecure = true;
                return requestUrl is null || CookieOrigin.IsSecure(requestUrl) || Refuse("skipped cookie because not 'secure'");
            }

            isHttpOnly |= flag.Equals("httponly", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private bool TryApplyValuedAttribute(string attribute, HeaderPart part)
        {
            switch (attribute)
            {
                case "path":
                    path = CookieFieldRules.SanitizePath(part.Value);
                    return true;
                case "domain":
                    return TrySetDomain(part);
                case "max-age":
                    expiresUnixSeconds = ExpiryFromMaxAge(part.Value);
                    return true;
                case "expires":
                    SetExpiryFromExpires(part.Value);
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Sets the domain, or refuses the cookie when the host may not set it; curl's refusal line then
        /// prints the rest of the header from the domain, as its <c>%s</c> of a pointer into the line does.
        /// </summary>
        private bool TrySetDomain(HeaderPart part)
        {
            int dotLength = part.Value.StartsWith('.') ? 1 : 0;
            string candidate = part.Value[dotLength..];
            if (host is null)
            {
                domain = candidate;
                includesSubdomains = true;
                return true;
            }

            bool matches = hostIsIpAddress ? string.Equals(candidate, host, StringComparison.Ordinal) : CookieOrigin.IsDomainOrSubdomain(candidate, host);
            domain = candidate;
            includesSubdomains = !hostIsIpAddress;
            return matches || Refuse($"skipped cookie with bad tailmatch domain: {headerValue[(part.ValueStart + dotLength)..]}");
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
