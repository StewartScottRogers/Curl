using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// RFC 9180 section 4.1's DHKEM over X25519 (the hand-built <see cref="X25519" />) and
/// over P-256 (the BCL's <see cref="ECDiffieHellman" />), both with HKDF-SHA256:
/// <c>Encap</c> with a caller's ephemeral private key, and <c>Decap</c>.
/// </summary>
/// <remarks>
/// A peer's public key or encapsulated key of the wrong length, not on the curve, or
/// giving X25519's all-zero result is the <c>false</c> of ADR-0118, with the shared secret
/// zeroed. The Diffie-Hellman output is zeroed in a <c>finally</c> block. X25519 is
/// constant-time; P-256 is as constant-time as the platform's library.
/// </remarks>
internal static class HpkeDhkem
{
    /// <summary>Nsecret, the length in bytes of a KEM shared secret.</summary>
    internal const int SharedSecretSize = 32;

    /// <summary>Nsk, the length in bytes of a private key for both KEMs.</summary>
    internal const int PrivateKeySize = 32;

    private const int CoordinateSize = 32;

    private const byte UncompressedPointPrefix = 0x04;

    // SP 800-186 section 3.2.1.3, P-256's field prime p and coefficient b.
    private static readonly BigInteger P256Prime = BigInteger.Parse(
        "0FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF",
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);

    private static readonly BigInteger P256CoefficientB = BigInteger.Parse(
        "05AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B",
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);

    /// <summary>Npk, the length in bytes of <paramref name="kem" />'s serialized public key, which is also Nenc.</summary>
    internal static int GetPublicKeySize(HpkeKem kem) =>
        kem == HpkeKem.DhkemX25519HkdfSha256 ? X25519.KeySize : 1 + (2 * CoordinateSize);

    /// <summary>
    /// <c>Encap(pkR)</c> with <paramref name="ephemeralPrivateKey" /> as skE: writes enc,
    /// the serialized pkE, into <paramref name="encapsulatedKey" /> and the KEM shared
    /// secret into <paramref name="sharedSecret" />.
    /// </summary>
    /// <returns><c>false</c>, with <paramref name="sharedSecret" /> all zero, when <paramref name="recipientPublicKey" /> is unusable.</returns>
    internal static bool TryEncapsulate(
        HpkeKem kem,
        ReadOnlySpan<byte> ephemeralPrivateKey,
        ReadOnlySpan<byte> recipientPublicKey,
        Span<byte> encapsulatedKey,
        Span<byte> sharedSecret) =>
        TryAgreeAndDerive(kem, ephemeralPrivateKey, recipientPublicKey, encapsulatedKey, encapsulatedKey, recipientPublicKey, sharedSecret);

    /// <summary>
    /// <c>Decap(enc, skR)</c>: writes the KEM shared secret for
    /// <paramref name="encapsulatedKey" /> into <paramref name="sharedSecret" />.
    /// </summary>
    /// <returns><c>false</c>, with <paramref name="sharedSecret" /> all zero, when <paramref name="encapsulatedKey" /> is unusable.</returns>
    internal static bool TryDecapsulate(
        HpkeKem kem,
        ReadOnlySpan<byte> encapsulatedKey,
        ReadOnlySpan<byte> recipientPrivateKey,
        Span<byte> sharedSecret)
    {
        Span<byte> recipientPublicKey = stackalloc byte[GetPublicKeySize(kem)];
        return TryAgreeAndDerive(kem, recipientPrivateKey, encapsulatedKey, recipientPublicKey, encapsulatedKey, recipientPublicKey, sharedSecret);
    }

    /// <summary>
    /// Computes DH(<paramref name="privateKey" />, <paramref name="peerPublicKey" />), writes
    /// the private key's own public key into <paramref name="ownPublicKey" />, then
    /// <c>ExtractAndExpand(dh, enc || pkRm)</c> into <paramref name="sharedSecret" />.
    /// <paramref name="encapsulatedKey" /> and <paramref name="recipientPublicKey" /> are
    /// read only after the agreement has filled whichever of them is
    /// <paramref name="ownPublicKey" />.
    /// </summary>
    private static bool TryAgreeAndDerive(
        HpkeKem kem,
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> ownPublicKey,
        ReadOnlySpan<byte> encapsulatedKey,
        ReadOnlySpan<byte> recipientPublicKey,
        Span<byte> sharedSecret)
    {
        Span<byte> diffieHellman = stackalloc byte[CoordinateSize];
        try
        {
            bool agreed = kem == HpkeKem.DhkemX25519HkdfSha256
                ? TryAgreeX25519(privateKey, peerPublicKey, diffieHellman, ownPublicKey)
                : TryAgreeP256(privateKey, peerPublicKey, diffieHellman, ownPublicKey);
            if (!agreed)
            {
                CryptographicOperations.ZeroMemory(sharedSecret);
                return false;
            }

            ExtractAndExpand(kem, diffieHellman, encapsulatedKey, recipientPublicKey, sharedSecret);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(diffieHellman);
        }
    }

    private static bool TryAgreeX25519(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> diffieHellman,
        Span<byte> ownPublicKey)
    {
        if (peerPublicKey.Length != X25519.KeySize)
        {
            return false;
        }

        X25519.ComputePublicKey(privateKey, ownPublicKey);
        return X25519.TryComputeSharedSecret(privateKey, peerPublicKey, diffieHellman);
    }

    private static bool TryAgreeP256(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> diffieHellman,
        Span<byte> ownPublicKey)
    {
        using ECDiffieHellman? peerKey = TryImportP256PublicKey(peerPublicKey);
        if (peerKey is null)
        {
            return false;
        }

        byte[] privateScalar = privateKey.ToArray();
        byte[]? agreement = null;
        try
        {
            using ECDiffieHellman ownKey = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = privateScalar });
            ECPoint ownPoint = ownKey.ExportParameters(false).Q;
            ownPublicKey[0] = UncompressedPointPrefix;
            ownPoint.X.CopyTo(ownPublicKey[1..]);
            ownPoint.Y.CopyTo(ownPublicKey[(1 + CoordinateSize)..]);
            agreement = ownKey.DeriveRawSecretAgreement(peerKey.PublicKey);
            agreement.CopyTo(diffieHellman);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateScalar);
            CryptographicOperations.ZeroMemory(agreement);
        }
    }

    /// <summary>
    /// Imports an uncompressed P-256 point, or gives <c>null</c> for one of the wrong length
    /// or form, or not on the curve. The curve check is done here rather than left to the
    /// platform, whose libraries report an invalid point with different exception types.
    /// </summary>
    private static ECDiffieHellman? TryImportP256PublicKey(ReadOnlySpan<byte> publicKey)
    {
        if (publicKey.Length != 1 + (2 * CoordinateSize) || publicKey[0] != UncompressedPointPrefix)
        {
            return null;
        }

        ReadOnlySpan<byte> x = publicKey.Slice(1, CoordinateSize);
        ReadOnlySpan<byte> y = publicKey.Slice(1 + CoordinateSize, CoordinateSize);
        if (!IsOnP256(new BigInteger(x, isUnsigned: true, isBigEndian: true), new BigInteger(y, isUnsigned: true, isBigEndian: true)))
        {
            return null;
        }

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x.ToArray(), Y = y.ToArray() },
        });
    }

    /// <summary>
    /// SEC 1 section 3.2.2.1's public key validation for P-256 (FIPS 186-5 / SP 800-186):
    /// both coordinates below p and y^2 = x^3 - 3x + b mod p. The cofactor is 1, so a point
    /// on the curve other than infinity, which has no uncompressed encoding, is in the
    /// group. Public data only, so <see cref="BigInteger" /> is used.
    /// </summary>
    private static bool IsOnP256(BigInteger x, BigInteger y)
    {
        if (x >= P256Prime || y >= P256Prime)
        {
            return false;
        }

        BigInteger left = BigInteger.ModPow(y, 2, P256Prime);
        BigInteger right = ((BigInteger.ModPow(x, 3, P256Prime) - (3 * x) + P256CoefficientB) % P256Prime + P256Prime) % P256Prime;
        return left == right;
    }

    /// <summary>
    /// RFC 9180 section 4.1's <c>ExtractAndExpand</c>: <c>eae_prk</c> from the
    /// Diffie-Hellman output, then <c>shared_secret</c> over <c>enc || pkRm</c>, both
    /// under the KEM's suite identifier <c>"KEM" || I2OSP(kem_id, 2)</c>.
    /// </summary>
    private static void ExtractAndExpand(
        HpkeKem kem,
        ReadOnlySpan<byte> diffieHellman,
        ReadOnlySpan<byte> encapsulatedKey,
        ReadOnlySpan<byte> recipientPublicKey,
        Span<byte> sharedSecret)
    {
        Span<byte> suiteId = stackalloc byte[5];
        "KEM"u8.CopyTo(suiteId);
        BinaryPrimitives.WriteUInt16BigEndian(suiteId[3..], (ushort)kem);
        Span<byte> kemContext = stackalloc byte[encapsulatedKey.Length + recipientPublicKey.Length];
        encapsulatedKey.CopyTo(kemContext);
        recipientPublicKey.CopyTo(kemContext[encapsulatedKey.Length..]);
        Span<byte> extractAndExpandKey = stackalloc byte[HpkeLabeledHkdf.HashSize];
        try
        {
            HpkeLabeledHkdf.Extract(suiteId, default, "eae_prk"u8, diffieHellman, extractAndExpandKey);
            HpkeLabeledHkdf.Expand(suiteId, extractAndExpandKey, "shared_secret"u8, kemContext, sharedSecret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(extractAndExpandKey);
        }
    }
}
