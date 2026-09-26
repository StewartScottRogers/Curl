namespace Curl.Cookies;

/// <summary>
/// What <see cref="SetCookieParser"/> and <see cref="CookieStore"/> both need to know about the host a
/// cookie came from or goes to, as libcurl 8.21.0 decides it.
/// </summary>
internal static class CookieOrigin
{
    private static readonly string[] SecureSchemes = ["https", "wss"];

    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>The URL's host as curl compares it: an IPv6 address without its brackets.</summary>
    public static string HostOf(Uri uri) => uri.HostNameType == UriHostNameType.IPv6 ? uri.Host.Trim('[', ']') : uri.Host;

    /// <summary>curl's <c>Curl_secure_context</c> for a URL: a TLS scheme, or a loopback host.</summary>
    public static bool IsSecure(Uri uri) => SecureSchemes.Contains(uri.Scheme, StringComparer.Ordinal) || IsLoopback(HostOf(uri));

    /// <summary><see langword="true"/> for <c>localhost</c> (any case), <c>127.0.0.1</c> and <c>::1</c>, which curl treats as secure.</summary>
    public static bool IsLoopback(string host) => LoopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    /// <summary>curl's <c>cookie_tailmatch</c>: <paramref name="host"/> is <paramref name="domain"/> or ends with it at a dot, in any case.</summary>
    public static bool IsDomainOrSubdomain(string domain, string host) =>
        host.EndsWith(domain, StringComparison.OrdinalIgnoreCase)
        && (host.Length == domain.Length || host[host.Length - domain.Length - 1] == '.');
}
