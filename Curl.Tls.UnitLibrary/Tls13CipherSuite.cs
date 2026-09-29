namespace Curl.Tls;

/// <summary>
/// A TLS 1.3 cipher suite (RFC 8446 appendix B.4): its code point, the key schedule of its
/// hash, and the length of its AEAD key, which is all the handshake needs to hand each
/// traffic secret to the record layer.
/// </summary>
/// <param name="Code">The cipher suite code point.</param>
/// <param name="KeySchedule">The key schedule of the suite's hash.</param>
/// <param name="KeyLength">The AEAD key length in bytes.</param>
public sealed record Tls13CipherSuite(ushort Code, Tls13KeySchedule KeySchedule, int KeyLength)
{
    /// <summary>Gets <c>TLS_AES_128_GCM_SHA256</c> (<c>0x1301</c>).</summary>
    public static Tls13CipherSuite Aes128GcmSha256 { get; } = new(0x1301, Tls13KeySchedule.Sha256, 16);

    /// <summary>Gets <c>TLS_AES_256_GCM_SHA384</c> (<c>0x1302</c>).</summary>
    public static Tls13CipherSuite Aes256GcmSha384 { get; } = new(0x1302, Tls13KeySchedule.Sha384, 32);

    /// <summary>Gets <c>TLS_CHACHA20_POLY1305_SHA256</c> (<c>0x1303</c>).</summary>
    public static Tls13CipherSuite ChaCha20Poly1305Sha256 { get; } = new(0x1303, Tls13KeySchedule.Sha256, 32);

    /// <summary>Gets <c>TLS_AES_128_CCM_SHA256</c> (<c>0x1304</c>).</summary>
    public static Tls13CipherSuite Aes128CcmSha256 { get; } = new(0x1304, Tls13KeySchedule.Sha256, 16);

    /// <summary>Gets <c>TLS_AES_128_CCM_8_SHA256</c> (<c>0x1305</c>).</summary>
    public static Tls13CipherSuite Aes128Ccm8Sha256 { get; } = new(0x1305, Tls13KeySchedule.Sha256, 16);

    /// <summary>Returns the suite with code point <paramref name="code" />.</summary>
    /// <param name="code">The cipher suite code point.</param>
    /// <returns>The suite, or <see langword="null" /> when it is not a TLS 1.3 suite.</returns>
    public static Tls13CipherSuite? Find(ushort code) => code switch
    {
        0x1301 => Aes128GcmSha256,
        0x1302 => Aes256GcmSha384,
        0x1303 => ChaCha20Poly1305Sha256,
        0x1304 => Aes128CcmSha256,
        0x1305 => Aes128Ccm8Sha256,
        _ => null,
    };
}
