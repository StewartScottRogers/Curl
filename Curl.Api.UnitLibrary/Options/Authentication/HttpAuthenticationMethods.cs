namespace Curl.Api.Options.Authentication;

/// <summary>
/// The <c>CURLAUTH_*</c> bits <c>CURLOPT_HTTPAUTH</c> and <c>CURLOPT_PROXYAUTH</c> accept:
/// the methods an origin or a proxy may be authenticated with. Each member's value is
/// libcurl's bit, in the 32 bits a <c>long</c> carries on every platform.
/// </summary>
[Flags]
public enum HttpAuthenticationMethods : uint
{
    /// <summary><c>CURLAUTH_NONE</c> (0): no authentication.</summary>
    None = 0,

    /// <summary><c>CURLAUTH_BASIC</c> (1 &lt;&lt; 0): HTTP Basic; libcurl's default.</summary>
    Basic = 1u << 0,

    /// <summary><c>CURLAUTH_DIGEST</c> (1 &lt;&lt; 1): HTTP Digest (RFC 7616).</summary>
    Digest = 1u << 1,

    /// <summary>
    /// <c>CURLAUTH_NEGOTIATE</c> (1 &lt;&lt; 2, also <c>CURLAUTH_GSSNEGOTIATE</c> and
    /// <c>CURLAUTH_GSSAPI</c>): SPNEGO (RFC 4559).
    /// </summary>
    Negotiate = 1u << 2,

    /// <summary><c>CURLAUTH_NTLM</c> (1 &lt;&lt; 3): NTLM.</summary>
    Ntlm = 1u << 3,

    /// <summary><c>CURLAUTH_DIGEST_IE</c> (1 &lt;&lt; 4): Digest as Internet Explorer sent it.</summary>
    DigestIe = 1u << 4,

    /// <summary>
    /// <c>CURLAUTH_NTLM_WB</c> (1 &lt;&lt; 5): NTLM through winbind; deprecated, and libcurl
    /// 8.22.0 no longer supports it.
    /// </summary>
    NtlmWb = 1u << 5,

    /// <summary><c>CURLAUTH_BEARER</c> (1 &lt;&lt; 6): an OAuth 2.0 bearer token (RFC 6750).</summary>
    Bearer = 1u << 6,

    /// <summary><c>CURLAUTH_AWS_SIGV4</c> (1 &lt;&lt; 7): AWS Signature Version 4.</summary>
    AwsSigV4 = 1u << 7,

    /// <summary>
    /// <c>CURLAUTH_ONLY</c> (1 &lt;&lt; 31): with one other bit, use that method even when the
    /// server does not ask for it.
    /// </summary>
    Only = 1u << 31,

    /// <summary><c>CURLAUTH_ANY</c> (<c>~CURLAUTH_DIGEST_IE</c>): every method.</summary>
    Any = ~DigestIe,

    /// <summary>
    /// <c>CURLAUTH_ANYSAFE</c> (<c>~(CURLAUTH_BASIC | CURLAUTH_DIGEST_IE)</c>): every method
    /// that does not send the password in the clear.
    /// </summary>
    AnySafe = ~(Basic | DigestIe),
}
