namespace Curl.Tls;

/// <summary>The record MAC of a TLS 1.2 and below suite (RFC 5246 section 6.2.3.1).</summary>
public enum Tls12MacAlgorithm
{
    /// <summary>No MAC: the initial state, and every AEAD suite.</summary>
    None,

    /// <summary>HMAC-MD5, 16 bytes (<c>_MD5</c> suites).</summary>
    HmacMd5,

    /// <summary>HMAC-SHA1, 20 bytes (<c>_SHA</c> suites).</summary>
    HmacSha1,

    /// <summary>HMAC-SHA256, 32 bytes (<c>_SHA256</c> CBC suites).</summary>
    HmacSha256,

    /// <summary>HMAC-SHA384, 48 bytes (<c>_SHA384</c> CBC suites).</summary>
    HmacSha384,
}
