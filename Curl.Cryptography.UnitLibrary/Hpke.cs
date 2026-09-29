using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Hybrid Public Key Encryption in base mode (RFC 9180 sections 5.1 and 5.1.1) for the
/// suites Encrypted Client Hello uses: DHKEM(X25519, HKDF-SHA256) or DHKEM(P-256,
/// HKDF-SHA256), HKDF-SHA256, and AES-128-GCM, AES-256-GCM or ChaCha20-Poly1305.
/// <c>TrySetupBaseSender</c> encapsulates to a recipient's public key and gives the
/// sender's <see cref="HpkeContext" />; <c>TrySetupBaseRecipient</c> gives the
/// recipient's from the encapsulated key.
/// </summary>
/// <remarks>
/// A suite value outside the three enumerations, a private key that is not 32 bytes, or an
/// encapsulated-key buffer of the wrong size is a caller mistake and throws
/// <see cref="ArgumentException" />. A peer's public key or encapsulated key that cannot be
/// used is ADR-0118's <c>false</c>, with no context.
/// </remarks>
public static class Hpke
{
    /// <summary>Nsk, the length in bytes of a private key for both supported KEMs.</summary>
    public const int PrivateKeySize = HpkeDhkem.PrivateKeySize;

    /// <summary>
    /// Nenc, the length in bytes of <paramref name="kem" />'s encapsulated key, which is
    /// also Npk, the length of its public key: 32 for X25519, 65 for P-256.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="kem" /> is not a supported KEM.</exception>
    public static int GetEncapsulatedKeySize(HpkeKem kem)
    {
        RequireKem(kem);
        return HpkeDhkem.GetPublicKeySize(kem);
    }

    /// <summary>
    /// <c>SetupBaseS(pkR, info)</c> with a fresh ephemeral key from the platform's random
    /// number generator: writes enc into <paramref name="encapsulatedKey" /> and gives the
    /// sender's context.
    /// </summary>
    /// <returns><c>false</c>, with <paramref name="context" /> <c>null</c>, when <paramref name="recipientPublicKey" /> is unusable.</returns>
    /// <exception cref="ArgumentException">A suite value is unsupported or <paramref name="encapsulatedKey" /> is not Nenc bytes.</exception>
    public static bool TrySetupBaseSender(
        HpkeKem kem,
        HpkeKdf kdf,
        HpkeAead aead,
        ReadOnlySpan<byte> recipientPublicKey,
        ReadOnlySpan<byte> info,
        Span<byte> encapsulatedKey,
        [NotNullWhen(true)] out HpkeContext? context)
    {
        RequireKem(kem);
        Span<byte> ephemeralPrivateKey = stackalloc byte[PrivateKeySize];
        try
        {
            GenerateEphemeralPrivateKey(kem, ephemeralPrivateKey);
            return TrySetupBaseSender(kem, kdf, aead, recipientPublicKey, ephemeralPrivateKey, info, encapsulatedKey, out context);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ephemeralPrivateKey);
        }
    }

    /// <summary>
    /// <c>SetupBaseS(pkR, info)</c> with <paramref name="ephemeralPrivateKey" /> as skE, so
    /// published vectors can be reproduced: writes enc into
    /// <paramref name="encapsulatedKey" /> and gives the sender's context.
    /// </summary>
    /// <returns><c>false</c>, with <paramref name="context" /> <c>null</c>, when <paramref name="recipientPublicKey" /> is unusable.</returns>
    /// <exception cref="ArgumentException">
    /// A suite value is unsupported, <paramref name="ephemeralPrivateKey" /> is not
    /// <see cref="PrivateKeySize" /> bytes, or <paramref name="encapsulatedKey" /> is not Nenc bytes.
    /// </exception>
    public static bool TrySetupBaseSender(
        HpkeKem kem,
        HpkeKdf kdf,
        HpkeAead aead,
        ReadOnlySpan<byte> recipientPublicKey,
        ReadOnlySpan<byte> ephemeralPrivateKey,
        ReadOnlySpan<byte> info,
        Span<byte> encapsulatedKey,
        [NotNullWhen(true)] out HpkeContext? context)
    {
        RequireSuite(kem, kdf, aead);
        RequireLength(ephemeralPrivateKey.Length, PrivateKeySize, nameof(ephemeralPrivateKey));
        RequireLength(encapsulatedKey.Length, HpkeDhkem.GetPublicKeySize(kem), nameof(encapsulatedKey));
        Span<byte> sharedSecret = stackalloc byte[HpkeDhkem.SharedSecretSize];
        try
        {
            context = HpkeDhkem.TryEncapsulate(kem, ephemeralPrivateKey, recipientPublicKey, encapsulatedKey, sharedSecret)
                ? new HpkeContext(kem, kdf, aead, sharedSecret, info)
                : null;
            return context is not null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
        }
    }

    /// <summary>
    /// <c>SetupBaseR(enc, skR, info)</c>: decapsulates <paramref name="encapsulatedKey" />
    /// with <paramref name="recipientPrivateKey" /> and gives the recipient's context.
    /// </summary>
    /// <returns><c>false</c>, with <paramref name="context" /> <c>null</c>, when <paramref name="encapsulatedKey" /> is unusable.</returns>
    /// <exception cref="ArgumentException">A suite value is unsupported or <paramref name="recipientPrivateKey" /> is not <see cref="PrivateKeySize" /> bytes.</exception>
    public static bool TrySetupBaseRecipient(
        HpkeKem kem,
        HpkeKdf kdf,
        HpkeAead aead,
        ReadOnlySpan<byte> encapsulatedKey,
        ReadOnlySpan<byte> recipientPrivateKey,
        ReadOnlySpan<byte> info,
        [NotNullWhen(true)] out HpkeContext? context)
    {
        RequireSuite(kem, kdf, aead);
        RequireLength(recipientPrivateKey.Length, PrivateKeySize, nameof(recipientPrivateKey));
        Span<byte> sharedSecret = stackalloc byte[HpkeDhkem.SharedSecretSize];
        try
        {
            context = HpkeDhkem.TryDecapsulate(kem, encapsulatedKey, recipientPrivateKey, sharedSecret)
                ? new HpkeContext(kem, kdf, aead, sharedSecret, info)
                : null;
            return context is not null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
        }
    }

    private static void GenerateEphemeralPrivateKey(HpkeKem kem, Span<byte> privateKey)
    {
        if (kem == HpkeKem.DhkemX25519HkdfSha256)
        {
            X25519.GeneratePrivateKey(privateKey);
            return;
        }

        using ECDiffieHellman ephemeralKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        byte[] privateScalar = ephemeralKey.ExportParameters(true).D!;
        privateScalar.CopyTo(privateKey);
        CryptographicOperations.ZeroMemory(privateScalar);
    }

    private static void RequireSuite(HpkeKem kem, HpkeKdf kdf, HpkeAead aead)
    {
        RequireKem(kem);
        if (kdf != HpkeKdf.HkdfSha256)
        {
            throw new ArgumentException($"HPKE KDF 0x{(ushort)kdf:x4} is not supported.", nameof(kdf));
        }

        if (aead is not (HpkeAead.Aes128Gcm or HpkeAead.Aes256Gcm or HpkeAead.ChaCha20Poly1305))
        {
            throw new ArgumentException($"HPKE AEAD 0x{(ushort)aead:x4} is not supported.", nameof(aead));
        }
    }

    private static void RequireKem(HpkeKem kem)
    {
        if (kem is not (HpkeKem.DhkemP256HkdfSha256 or HpkeKem.DhkemX25519HkdfSha256))
        {
            throw new ArgumentException($"HPKE KEM 0x{(ushort)kem:x4} is not supported.", nameof(kem));
        }
    }

    private static void RequireLength(int length, int expected, string parameterName)
    {
        if (length != expected)
        {
            throw new ArgumentException($"HPKE needs {expected} bytes here; this is {length}.", parameterName);
        }
    }
}
