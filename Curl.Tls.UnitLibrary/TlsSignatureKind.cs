namespace Curl.Tls;

/// <summary>The signature algorithm behind a TLS signature scheme.</summary>
internal enum TlsSignatureKind
{
    /// <summary>RSASSA-PSS with a salt as long as the hash.</summary>
    RsaPss,

    /// <summary>ECDSA with a DER-encoded signature.</summary>
    Ecdsa,

    /// <summary>Ed25519 (RFC 8032).</summary>
    Ed25519,

    /// <summary>RSASSA-PKCS1-v1_5 with the hash's DigestInfo (TLS 1.2's <c>rsa_pkcs1_*</c>).</summary>
    RsaPkcs1,

    /// <summary>
    /// TLS 1.0 and 1.1's RSA signature (RFC 4346 section 4.7): PKCS #1 block type 1 over
    /// the 36-byte MD5 and SHA-1 hashes of the content, with no DigestInfo.
    /// </summary>
    RsaMd5Sha1,

    /// <summary>DSA (FIPS 186-4) with the DER <c>Dss-Sig-Value</c> signature of RFC 5246 section 4.7.</summary>
    Dsa,
}
