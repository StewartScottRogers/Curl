namespace Curl.Cryptography;

/// <summary>
/// The HPKE key encapsulation mechanisms <see cref="Hpke" /> supports, valued by their
/// RFC 9180 section 7.1 identifier: the two Encrypted Client Hello uses.
/// </summary>
public enum HpkeKem : ushort
{
    /// <summary>DHKEM(P-256, HKDF-SHA256), <c>0x0010</c>: 65-byte uncompressed public keys, 32-byte private keys.</summary>
    DhkemP256HkdfSha256 = 0x0010,

    /// <summary>DHKEM(X25519, HKDF-SHA256), <c>0x0020</c>: 32-byte public and private keys.</summary>
    DhkemX25519HkdfSha256 = 0x0020,
}
