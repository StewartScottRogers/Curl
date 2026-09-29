namespace Curl.Tls;

/// <summary>
/// The bulk ciphers that protect TLS 1.2, 1.1 and 1.0 records, as a suite names them
/// (ADR-0140, "What the client supports"). The CBC ciphers pair with a
/// <see cref="Tls12MacAlgorithm" />; the AEAD ciphers carry their own tag and need TLS 1.2.
/// </summary>
public enum Tls12BulkCipher
{
    /// <summary>No encryption: the initial state, and the <c>_WITH_NULL_</c> suites with a MAC.</summary>
    Null,

    /// <summary>Triple DES (EDE) in CBC mode, 24-byte key, 8-byte blocks (<c>_WITH_3DES_EDE_CBC_</c>).</summary>
    TripleDesEdeCbc,

    /// <summary>AES-128 in CBC mode (<c>_WITH_AES_128_CBC_</c>).</summary>
    Aes128Cbc,

    /// <summary>AES-256 in CBC mode (<c>_WITH_AES_256_CBC_</c>).</summary>
    Aes256Cbc,

    /// <summary>Camellia-128 in CBC mode (<c>_WITH_CAMELLIA_128_CBC_</c>, RFC 5932).</summary>
    Camellia128Cbc,

    /// <summary>Camellia-256 in CBC mode (<c>_WITH_CAMELLIA_256_CBC_</c>, RFC 5932).</summary>
    Camellia256Cbc,

    /// <summary>AES-128-GCM (<c>_WITH_AES_128_GCM_</c>, RFC 5288): a 4-byte salt and an 8-byte explicit nonce.</summary>
    Aes128Gcm,

    /// <summary>AES-256-GCM (<c>_WITH_AES_256_GCM_</c>, RFC 5288): a 4-byte salt and an 8-byte explicit nonce.</summary>
    Aes256Gcm,

    /// <summary>ARIA-128-GCM (<c>_WITH_ARIA_128_GCM_</c>, RFC 6209): nonces as AES-GCM's.</summary>
    Aria128Gcm,

    /// <summary>ARIA-256-GCM (<c>_WITH_ARIA_256_GCM_</c>, RFC 6209): nonces as AES-GCM's.</summary>
    Aria256Gcm,

    /// <summary>ChaCha20-Poly1305 (<c>_WITH_CHACHA20_POLY1305_</c>, RFC 7905): a 12-byte IV XORed with the sequence number.</summary>
    ChaCha20Poly1305,
}
