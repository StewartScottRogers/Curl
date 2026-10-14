namespace Curl.HttpMessageSignatures;

/// <summary>
/// The two RFC 9421 signature algorithms curl 8.22.0's <c>--httpsig-algo</c> offers
/// (upstream <c>lib/http_httpsig.c</c>).
/// </summary>
public enum HttpSignatureAlgorithm
{
    /// <summary><c>ed25519</c> (RFC 9421 section 3.3.6): the key is a 32-byte private seed. curl's default.</summary>
    Ed25519,

    /// <summary><c>hmac-sha256</c> (RFC 9421 section 3.3.3): the key is the shared secret.</summary>
    HmacSha256,
}
