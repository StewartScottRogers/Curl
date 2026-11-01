namespace Curl.Extended.Protocol.Coap.Dtls.TestServer;

/// <summary>The kind of key and certificate <see cref="DtlsTestServer" /> authenticates itself with.</summary>
internal enum DtlsTestServerKeyKind
{
    /// <summary>An ECDSA P-256 key: TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256, signing with ecdsa_secp256r1_sha256.</summary>
    Ecdsa,

    /// <summary>An RSA 2048-bit key: TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256, signing with rsa_pss_rsae_sha256 (the one RSA scheme Curl.Tls signs publicly).</summary>
    Rsa,
}
