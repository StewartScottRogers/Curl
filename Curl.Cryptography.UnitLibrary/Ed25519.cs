using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Ed25519 signatures (RFC 8032 section 5.1): a private key is a 32-byte seed, a public
/// key is a 32-byte encoded point, and a signature is 64 bytes, R followed by S. Signing
/// is deterministic, so it needs no randomness; SHA-512 comes from the base class
/// library.
/// </summary>
/// <remarks>
/// Key derivation and signing are constant-time in the private key and the secret nonce:
/// scalar multiplication visits all 256 bits with the same operations and swaps by mask,
/// scalar reduction has fixed loop bounds, and every secret intermediate is zeroed before
/// returning. <see cref="Verify" /> works on public data only and is not constant-time.
/// Verification is cofactorless, checking [S]B = R + [k]A by comparing encodings, which
/// RFC 8032 section 5.1.7 permits, and rejects S &gt;= L and a public key that does not
/// decode.
/// </remarks>
public static class Ed25519
{
    /// <summary>The length in bytes of a private key (the seed).</summary>
    public const int PrivateKeySize = 32;

    /// <summary>The length in bytes of a public key.</summary>
    public const int PublicKeySize = 32;

    /// <summary>The length in bytes of a signature.</summary>
    public const int SignatureSize = 64;

    private const int HashSize = 64;

    /// <summary>
    /// Fills <paramref name="privateKey" /> with <see cref="PrivateKeySize" /> bytes from
    /// <see cref="RandomNumberGenerator" />.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="privateKey" /> is not <see cref="PrivateKeySize" /> bytes.</exception>
    public static void GeneratePrivateKey(Span<byte> privateKey)
    {
        RequireSize(privateKey.Length, PrivateKeySize, nameof(privateKey));
        RandomNumberGenerator.Fill(privateKey);
    }

    /// <summary>
    /// Computes the public key of <paramref name="privateKey" /> into
    /// <paramref name="publicKey" /> (RFC 8032 section 5.1.5).
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void ComputePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        RequireSize(privateKey.Length, PrivateKeySize, nameof(privateKey));
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        Span<byte> expanded = stackalloc byte[HashSize];
        try
        {
            ExpandPrivateKey(privateKey, expanded);
            MultiplyBaseAndEncode(publicKey, expanded[..Scalar25519.EncodedLength]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expanded);
        }
    }

    /// <summary>
    /// Signs <paramref name="message" /> with <paramref name="privateKey" /> into
    /// <paramref name="signature" /> (RFC 8032 section 5.1.6). The signature must not
    /// overlap the message.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void Sign(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> message, Span<byte> signature)
    {
        RequireSize(privateKey.Length, PrivateKeySize, nameof(privateKey));
        RequireSize(signature.Length, SignatureSize, nameof(signature));
        Span<byte> secrets = stackalloc byte[(2 * HashSize) + (2 * Scalar25519.EncodedLength)];
        Span<byte> expanded = secrets[..HashSize];
        Span<byte> hash = secrets.Slice(HashSize, HashSize);
        Span<byte> nonce = secrets.Slice(2 * HashSize, Scalar25519.EncodedLength);
        Span<byte> challenge = secrets.Slice((2 * HashSize) + Scalar25519.EncodedLength, Scalar25519.EncodedLength);
        Span<byte> publicKey = stackalloc byte[PublicKeySize];
        try
        {
            ExpandPrivateKey(privateKey, expanded);
            ReadOnlySpan<byte> secretScalar = expanded[..Scalar25519.EncodedLength];
            MultiplyBaseAndEncode(publicKey, secretScalar);
            HashToScalar(nonce, hash, expanded[Scalar25519.EncodedLength..], [], message);
            MultiplyBaseAndEncode(signature[..Edwards25519.EncodedLength], nonce);
            HashToScalar(challenge, hash, signature[..Edwards25519.EncodedLength], publicKey, message);
            Scalar25519.MultiplyAdd(signature[Edwards25519.EncodedLength..], challenge, secretScalar, nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secrets);
        }
    }

    /// <summary>
    /// Verifies <paramref name="signature" /> over <paramref name="message" /> against
    /// <paramref name="publicKey" /> (RFC 8032 section 5.1.7).
    /// </summary>
    /// <returns>
    /// <c>true</c> when the signature is valid; <c>false</c> when S is not below the group
    /// order L, when the public key does not decode to a point, or when
    /// [S]B differs from R + [k]A.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        RequireSize(signature.Length, SignatureSize, nameof(signature));
        ReadOnlySpan<byte> encodedR = signature[..Edwards25519.EncodedLength];
        ReadOnlySpan<byte> s = signature[Edwards25519.EncodedLength..];
        Span<long> negatedKey = stackalloc long[Edwards25519.PointLength];
        if (!Scalar25519.IsBelowOrder(s) || !Edwards25519.TryDecode(negatedKey, publicKey))
        {
            return false;
        }

        Span<byte> hash = stackalloc byte[HashSize];
        Span<byte> challenge = stackalloc byte[Scalar25519.EncodedLength];
        HashToScalar(challenge, hash, encodedR, publicKey, message);
        Edwards25519.Negate(negatedKey);
        Span<long> sum = stackalloc long[Edwards25519.PointLength];
        Span<long> product = stackalloc long[Edwards25519.PointLength];
        Edwards25519.ScalarMultiplyBase(sum, s);
        Edwards25519.ScalarMultiply(product, negatedKey, challenge);
        Edwards25519.Add(sum, product);
        Span<byte> expectedR = stackalloc byte[Edwards25519.EncodedLength];
        Edwards25519.Encode(expectedR, sum);
        return CryptographicOperations.FixedTimeEquals(expectedR, encodedR);
    }

    private static void RequireSize(int length, int size, string parameterName)
    {
        if (length != size)
        {
            throw new ArgumentException($"This Ed25519 value is {size} bytes; the one given is {length}.", parameterName);
        }
    }

    /// <summary>
    /// Hashes the seed with SHA-512 and prunes the lower half into the secret scalar
    /// (RFC 8032 section 5.1.5, steps 1 and 2); the upper half is the nonce prefix.
    /// </summary>
    private static void ExpandPrivateKey(ReadOnlySpan<byte> privateKey, Span<byte> expanded)
    {
        SHA512.HashData(privateKey, expanded);
        expanded[0] &= 248;
        expanded[Scalar25519.EncodedLength - 1] &= 127;
        expanded[Scalar25519.EncodedLength - 1] |= 64;
    }

    /// <summary>Encodes [<paramref name="scalar" />]B into <paramref name="encoded" />.</summary>
    private static void MultiplyBaseAndEncode(Span<byte> encoded, ReadOnlySpan<byte> scalar)
    {
        Span<long> point = stackalloc long[Edwards25519.PointLength];
        try
        {
            Edwards25519.ScalarMultiplyBase(point, scalar);
            Edwards25519.Encode(encoded, point);
        }
        finally
        {
            Field25519.Clear(point);
        }
    }

    /// <summary>
    /// Sets <paramref name="scalar" /> to SHA-512(<paramref name="first" /> ||
    /// <paramref name="second" /> || <paramref name="message" />) modulo L, using
    /// <paramref name="hash" /> for the digest.
    /// </summary>
    private static void HashToScalar(
        Span<byte> scalar,
        Span<byte> hash,
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> message)
    {
        using IncrementalHash sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        sha512.AppendData(first);
        sha512.AppendData(second);
        sha512.AppendData(message);
        sha512.GetHashAndReset(hash);
        Scalar25519.Reduce(scalar, hash);
    }
}
