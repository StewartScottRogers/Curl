using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// The cookies a run of curl has received, kept and sent back the way libcurl 8.21.0 does. Every rule
/// here was measured on curl 8.21.0 on 2026-09-26 against a loopback server, as
/// <c>CookieStoreTests</c> records.
/// </summary>
/// <remarks>
/// The store never reads a clock: every call is handed the time it acts at. It does not yet refuse a
/// cookie set on a public suffix (BL-223), and it does not load or write a cookie file (BL-221).
/// </remarks>
public sealed class CookieStore : ICookieStore
{
    /// <summary>The most cookies curl stores from one response; the <c>Set-Cookie</c> headers after that are ignored.</summary>
    public const int MostCookiesStoredPerResponse = 50;

    /// <summary>The most cookies curl sends in one <c>Cookie</c> header.</summary>
    public const int MostCookiesSent = 150;

    /// <summary>The longest <c>Cookie</c> header value curl sends; the cookie that would make it longer, and every one after it, is left out.</summary>
    public const int LongestCookieHeader = 8183;

    /// <summary>The stored cookies, oldest first; a replaced cookie keeps its place.</summary>
    private readonly List<Cookie> cookies = [];

    /// <summary>The stored cookies, oldest first. A cookie that replaced a namesake sits where the namesake was.</summary>
    public IReadOnlyList<Cookie> Cookies => cookies;

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// A cookie is sent when it has not expired (its expiry, when it has one, is not before
    /// <paramref name="now"/>), its domain matches the host (the host itself for a host-only cookie, the
    /// host or a parent of it otherwise, in any case), its path matches the URL's path (the path is
    /// <c>/</c>, or is the URL's path or the start of it up to a <c>/</c>, case-sensitively), and, when
    /// it is <c>Secure</c>, the request is secure: <paramref name="secure"/>, or a loopback host
    /// (<c>localhost</c>, <c>127.0.0.1</c> or <c>::1</c>), as curl's <c>Curl_secure_context</c> decides.
    /// </para>
    /// <para>
    /// Only the <see cref="MostCookiesSent"/> oldest matching cookies are sent. They are ordered longest
    /// path first, then longest domain, then longest name, then newest first, and written
    /// <c>name=value</c>, separated by <c>; </c>. Writing stops at the first cookie that would make the
    /// value longer than <see cref="LongestCookieHeader"/> characters.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> is <see langword="null"/>.</exception>
    public string? GetCookieHeader(Uri uri, bool secure, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(uri);

        RemoveExpired(now);
        string host = CookieOrigin.HostOf(uri);
        bool secureContext = secure || CookieOrigin.IsLoopback(host);
        string path = uri.AbsolutePath;
        IEnumerable<Cookie> sent = InSendingOrder(cookies.Where(cookie => IsSentTo(cookie, host, path, secureContext)).Take(MostCookiesSent));
        return WriteHeader(sent);
    }

    /// <summary>curl's tests in <c>Curl_cookie_getlist</c>: secure, domain and path.</summary>
    private static bool IsSentTo(Cookie cookie, string host, string path, bool secureContext) =>
        (secureContext || !cookie.IsSecure) && DomainMatches(cookie, host) && PathMatches(cookie.Path, path);

    /// <summary>curl's <c>cookie_sort</c>: longest path, then longest domain, then longest name, then newest first.</summary>
    private static List<Cookie> InSendingOrder(IEnumerable<Cookie> oldestFirst)
    {
        List<Cookie> newestFirst = [.. oldestFirst];
        newestFirst.Reverse();
        return [.. newestFirst.Order(SendingOrder.Instance)];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Each header is read by <see cref="SetCookieParser"/>; one it refuses is skipped. A cookie with
    /// the name (case-sensitively), domain (in any case), host-only flag and path (case-sensitively) of
    /// a stored one replaces it in its place; any other is added as the newest. A cookie that is not
    /// <c>Secure</c>, from an origin that is not secure, is dropped when a stored <c>Secure</c> cookie
    /// has its name, a domain that is its domain or a parent or child of it, and a path whose first
    /// segment starts the new cookie's path: curl will not let it overlay the secure one.
    /// </para>
    /// <para>
    /// Once <see cref="MostCookiesStoredPerResponse"/> cookies have been stored or replaced, the rest of
    /// the headers are ignored. A cookie that arrived already expired replaces its namesake and is then
    /// removed with every other expired cookie, so it deletes the namesake.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> or <paramref name="setCookieHeaders"/> is <see langword="null"/>.</exception>
    public void StoreFromResponse(Uri uri, IReadOnlyList<string> setCookieHeaders, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(setCookieHeaders);

        bool secureOrigin = CookieOrigin.IsSecure(uri);
        int stored = 0;
        foreach (string header in setCookieHeaders)
        {
            if (stored == MostCookiesStoredPerResponse)
            {
                break;
            }

            Cookie? cookie = SetCookieParser.Parse(header, uri, now);
            if (cookie is not null && (secureOrigin || !OverlaysSecureCookie(cookie)))
            {
                Store(cookie);
                stored++;
            }
        }

        RemoveExpired(now);
    }

    private void Store(Cookie cookie)
    {
        int namesake = cookies.FindIndex(stored => IsNamesake(stored, cookie));
        if (namesake < 0)
        {
            cookies.Add(cookie);
        }
        else
        {
            cookies[namesake] = cookie;
        }
    }

    /// <summary>curl's <c>replace_existing</c>: the same name, domain, host-only flag and path.</summary>
    private static bool IsNamesake(Cookie stored, Cookie cookie) =>
        string.Equals(stored.Name, cookie.Name, StringComparison.Ordinal)
        && string.Equals(stored.Domain, cookie.Domain, StringComparison.OrdinalIgnoreCase)
        && stored.IncludesSubdomains == cookie.IncludesSubdomains
        && string.Equals(stored.Path, cookie.Path, StringComparison.Ordinal);

    /// <summary>
    /// curl's rule that a cookie from an origin that is not secure may not overlay a <c>Secure</c> one:
    /// the same name, domains where one is the other or a parent of it, and the stored path's first
    /// segment (<c>/</c> for the path <c>/</c>) starting the new path.
    /// </summary>
    private bool OverlaysSecureCookie(Cookie cookie) =>
        cookies.Exists(stored =>
            stored.IsSecure
            && string.Equals(stored.Name, cookie.Name, StringComparison.Ordinal)
            && (CookieOrigin.IsDomainOrSubdomain(stored.Domain, cookie.Domain) || CookieOrigin.IsDomainOrSubdomain(cookie.Domain, stored.Domain))
            && cookie.Path.StartsWith(FirstPathSegment(stored.Path), StringComparison.Ordinal));

    private static string FirstPathSegment(string path)
    {
        int secondSlash = path.IndexOf('/', 1);
        return secondSlash < 0 ? path : path[..secondSlash];
    }

    /// <summary>Removes every cookie whose expiry is before <paramref name="now"/>; a session cookie never expires.</summary>
    private void RemoveExpired(DateTimeOffset now)
    {
        long nowUnixSeconds = now.ToUnixTimeSeconds();
        cookies.RemoveAll(cookie => !cookie.IsSessionCookie && cookie.ExpiresUnixSeconds < nowUnixSeconds);
    }

    /// <summary>curl's domain test in <c>Curl_cookie_getlist</c>.</summary>
    private static bool DomainMatches(Cookie cookie, string host) =>
        cookie.IncludesSubdomains
            ? CookieOrigin.IsDomainOrSubdomain(cookie.Domain, host)
            : string.Equals(cookie.Domain, host, StringComparison.OrdinalIgnoreCase);

    /// <summary>curl's <c>pathmatch</c>: <c>/</c>, or the request path itself or up to a <c>/</c>, case-sensitively.</summary>
    private static bool PathMatches(string cookiePath, string requestPath) =>
        cookiePath == "/"
        || (requestPath.StartsWith(cookiePath, StringComparison.Ordinal)
            && (requestPath.Length == cookiePath.Length || requestPath[cookiePath.Length] == '/'));

    private static string? WriteHeader(IEnumerable<Cookie> sent)
    {
        StringBuilder header = new();
        foreach (Cookie cookie in sent)
        {
            string separator = header.Length == 0 ? string.Empty : "; ";
            if (header.Length + separator.Length + cookie.Name.Length + 1 + cookie.Value.Length > LongestCookieHeader)
            {
                break;
            }

            header.Append(separator).Append(cookie.Name).Append('=').Append(cookie.Value);
        }

        return header.Length == 0 ? null : header.ToString();
    }

    /// <summary>Compares two cookies by curl's <c>cookie_sort</c> keys, leaving ties to a stable sort.</summary>
    private sealed class SendingOrder : IComparer<Cookie>
    {
        public static readonly SendingOrder Instance = new();

        public int Compare(Cookie? x, Cookie? y)
        {
            int byPath = y!.Path.Length.CompareTo(x!.Path.Length);
            int byDomain = byPath != 0 ? byPath : y.Domain.Length.CompareTo(x.Domain.Length);
            return byDomain != 0 ? byDomain : y.Name.Length.CompareTo(x.Name.Length);
        }
    }
}
