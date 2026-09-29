namespace Curl.Cryptography;

/// <summary>
/// The HPKE AEADs <see cref="Hpke" /> supports, valued by their RFC 9180 section 7.3
/// identifier. Each takes a 12-byte nonce and gives a 16-byte tag.
/// </summary>
public enum HpkeAead : ushort
{
    /// <summary>AES-128-GCM, <c>0x0001</c>, from the BCL's <see cref="System.Security.Cryptography.AesGcm" />.</summary>
    Aes128Gcm = 0x0001,

    /// <summary>AES-256-GCM, <c>0x0002</c>, from the BCL's <see cref="System.Security.Cryptography.AesGcm" />.</summary>
    Aes256Gcm = 0x0002,

    /// <summary>ChaCha20-Poly1305, <c>0x0003</c>, the hand-built <see cref="AeadChaCha20Poly1305" />.</summary>
    ChaCha20Poly1305 = 0x0003,
}
