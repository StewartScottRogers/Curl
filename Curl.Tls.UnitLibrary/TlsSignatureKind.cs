namespace Curl.Tls;

/// <summary>The signature algorithm behind a TLS 1.3 signature scheme.</summary>
internal enum TlsSignatureKind
{
    /// <summary>RSASSA-PSS with a salt as long as the hash.</summary>
    RsaPss,

    /// <summary>ECDSA with a DER-encoded signature.</summary>
    Ecdsa,

    /// <summary>Ed25519 (RFC 8032).</summary>
    Ed25519,
}
