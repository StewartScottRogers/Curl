using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An <c>HpkeSymmetricCipherSuite</c> (RFC 9849 section 4): the HPKE KDF and AEAD
/// identifiers an ECHConfig offers, kept as sent so a suite the client lacks can be skipped.
/// </summary>
/// <param name="KdfId">The HPKE KDF identifier (RFC 9180 section 7.2).</param>
/// <param name="AeadId">The HPKE AEAD identifier (RFC 9180 section 7.3).</param>
public readonly record struct EchCipherSuite(ushort KdfId, ushort AeadId)
{
    /// <summary>Gets the suite GREASE ECH claims: HKDF-SHA256 with AES-128-GCM, as BoringSSL picks where AES is in hardware.</summary>
    public static EchCipherSuite Grease { get; } = new((ushort)HpkeKdf.HkdfSha256, (ushort)HpkeAead.Aes128Gcm);

    /// <summary>Gets a value indicating whether <see cref="Hpke" /> can seal with this suite: HKDF-SHA256 with AES-128-GCM, AES-256-GCM or ChaCha20-Poly1305.</summary>
    public bool IsSupported =>
        KdfId == (ushort)HpkeKdf.HkdfSha256
            && (HpkeAead)AeadId is HpkeAead.Aes128Gcm or HpkeAead.Aes256Gcm or HpkeAead.ChaCha20Poly1305;
}
