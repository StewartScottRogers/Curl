namespace Curl.Api.Options.Proxy;

/// <summary>
/// The <c>CURLAUTH_*</c> bits <c>CURLOPT_SOCKS5_AUTH</c> accepts: the methods a SOCKS5
/// proxy may be authenticated with. Each member's value is libcurl's bit.
/// </summary>
[Flags]
public enum Socks5Authentication
{
    /// <summary><c>CURLAUTH_NONE</c> (0): no authentication method is offered.</summary>
    None = 0,

    /// <summary><c>CURLAUTH_BASIC</c> (1): user name and password (RFC 1929).</summary>
    Basic = 1 << 0,

    /// <summary>
    /// <c>CURLAUTH_GSSAPI</c> (1 &lt;&lt; 2, the same bit as <c>CURLAUTH_NEGOTIATE</c>):
    /// GSS-API (RFC 1961).
    /// </summary>
    GssApi = 1 << 2,
}
