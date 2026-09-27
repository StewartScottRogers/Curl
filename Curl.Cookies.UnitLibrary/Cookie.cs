namespace Curl.Cookies;

/// <summary>
/// One cookie as libcurl 8.21.0 keeps it after reading a <c>Set-Cookie</c> header: the fields a
/// Netscape cookie-jar line carries.
/// </summary>
/// <param name="Name">The cookie's name, as sent, with surrounding spaces removed.</param>
/// <param name="Value">The cookie's value, as sent, quotes included, with surrounding spaces and tabs removed.</param>
/// <param name="Domain">
/// The host the cookie belongs to, without a leading dot: the <c>Domain</c> attribute when one was
/// accepted, otherwise the request's host. Kept in the case it was written in. <see langword="null"/> for a
/// cookie read from a <c>Set-Cookie:</c> line of a cookie file without a <c>Domain</c>: it is sent to every
/// host, sorts as an empty domain, and is never written to the jar.
/// </param>
/// <param name="IncludesSubdomains">
/// <see langword="true"/> when a <c>Domain</c> attribute named a host name, so the cookie is also sent
/// to its subdomains (the jar's <c>TRUE</c> column, curl's <c>tailmatch</c>); <see langword="false"/>
/// for a host-only cookie, including one whose <c>Domain</c> repeated an IP address.
/// </param>
/// <param name="Path">
/// The path the cookie is sent under. Empty for a cookie read from a <c>Set-Cookie:</c> line of a cookie
/// file without a <c>Path</c>: it matches every path, sorts before <c>/</c> and is written to the jar as <c>/</c>.
/// </param>
/// <param name="IsSecure"><see langword="true"/> when the cookie carried the <c>Secure</c> attribute.</param>
/// <param name="IsHttpOnly"><see langword="true"/> when the cookie carried the <c>HttpOnly</c> attribute.</param>
/// <param name="ExpiresUnixSeconds">
/// When the cookie expires, in seconds since the Unix epoch, as curl's jar writes it: <c>0</c> for a
/// session cookie, and <c>1</c> for a cookie that arrived already expired (<c>Max-Age=0</c>, an
/// <c>Expires</c> date at or before the epoch), which a store uses to delete its namesake.
/// </param>
public sealed record Cookie(
    string Name,
    string Value,
    string? Domain,
    bool IncludesSubdomains,
    string Path,
    bool IsSecure,
    bool IsHttpOnly,
    long ExpiresUnixSeconds)
{
    /// <summary><see langword="true"/> for a session cookie, one with no expiry time.</summary>
    public bool IsSessionCookie => ExpiresUnixSeconds == 0;
}
