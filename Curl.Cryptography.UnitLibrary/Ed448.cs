using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Ed448 signatures, PureEdDSA with an optional context (RFC 8032 section 5.2): a private
/// key is a 57-byte seed, a public key is a 57-byte encoded point, and a signature is 114
/// bytes, R followed by S. Signing is deterministic, so it needs no randomness; SHAKE256
/// is the hand-built <see cref="Shake" />, because the BCL's is missing on macOS
/// (ADR-0118).
/// </summary>
/// <remarks>
/// Key derivation and signing are constant-time in the private key and the secret nonce:
/// scalar multiplication visits all 456 bits with the same operations and swaps by mask,
/// scalar arithmetic runs on <see cref="MontgomeryModulus" />, and every secret
/// intermediate is zeroed before returning. <see cref="Verify(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})" />
/// works on public data only and is not constant-time. Verification is cofactorless,
/// checking [S]B = R + [k]A by comparing encodings, which RFC 8032 section 5.2.7 permits,
/// and rejects S &gt;= L and a public key that does not decode; a non-canonical R never
/// equals the canonical encoding it is compared with.
/// </remarks>
public static class Ed448
{
    /// <summary>The length in bytes of a private key (the seed).</summary>
    public const int PrivateKeySize = 57;

    /// <summary>The length in bytes of a public key.</summary>
    public const int PublicKeySize = 57;

    /// <summary>The length in bytes of a signature.</summary>
    public const int SignatureSize = 114;

    /// <summary>The longest context RFC 8032 allows, in bytes.</summary>
    public const int MaximumContextSize = 255;

    private const int HashSize = 114;

    /// <summary>"SigEd448", the prefix of dom4 (RFC 8032 section 5.2).</summary>
    private static ReadOnlySpan<byte> DomainPrefix => "SigEd448"u8;

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
    /// <paramref name="publicKey" /> (RFC 8032 section 5.2.5).
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
            MultiplyBaseAndEncode(publicKey, expanded[..Scalar448.EncodedLength]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expanded);
        }
    }

    /// <summary>
    /// Signs <paramref name="message" /> with <paramref name="privateKey" /> and an empty
    /// context into <paramref name="signature" /> (RFC 8032 section 5.2.6). The signature
    /// must not overlap the message.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static void Sign(ReadOnlySpan<byte> privateKey, ReadOnlySpan<byte> message, Span<byte> signature) =>
        Sign(privateKey, message, [], signature);

    /// <summary>
    /// Signs <paramref name="message" /> with <paramref name="privateKey" /> under
    /// <paramref name="context" /> into <paramref name="signature" /> (RFC 8032 section
    /// 5.2.6). The signature must not overlap the message or the context.
    /// </summary>
    /// <exception cref="ArgumentException">A span has the wrong length, or the context is longer than <see cref="MaximumContextSize" />.</exception>
    public static void Sign(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> context,
        Span<byte> signature)
    {
        RequireSize(privateKey.Length, PrivateKeySize, nameof(privateKey));
        RequireSize(signature.Length, SignatureSize, nameof(signature));
        RequireContext(context);
        const int ScalarSize = Scalar448.EncodedLength;
        Span<byte> secrets = stackalloc byte[(2 * HashSize) + (2 * ScalarSize)];
        Span<byte> expanded = secrets[..HashSize];
        Span<byte> hash = secrets.Slice(HashSize, HashSize);
        Span<byte> nonce = secrets.Slice(2 * HashSize, ScalarSize);
        Span<byte> challenge = secrets.Slice((2 * HashSize) + ScalarSize, ScalarSize);
        Span<byte> publicKey = stackalloc byte[PublicKeySize];
        Span<byte> encodedR = signature[..Edwards448.EncodedLength];
        try
        {
            ExpandPrivateKey(privateKey, expanded);
            ReadOnlySpan<byte> secretScalar = expanded[..ScalarSize];
            MultiplyBaseAndEncode(publicKey, secretScalar);
            HashToScalar(nonce, hash, context, expanded[ScalarSize..], [], message);
            MultiplyBaseAndEncode(encodedR, nonce);
            HashToScalar(challenge, hash, context, encodedR, publicKey, message);
            Scalar448.MultiplyAdd(signature[Edwards448.EncodedLength..], secretScalar, challenge, nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secrets);
        }
    }

    /// <summary>
    /// Verifies <paramref name="signature" /> over <paramref name="message" /> with an empty
    /// context against <paramref name="publicKey" /> (RFC 8032 section 5.2.7).
    /// </summary>
    /// <returns>
    /// <c>true</c> when the signature is valid; <c>false</c> when S is not below the group
    /// order L, when the public key does not decode to a point, or when [S]B differs from
    /// R + [k]A.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length.</exception>
    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature) =>
        Verify(publicKey, message, [], signature);

    /// <summary>
    /// Verifies <paramref name="signature" /> over <paramref name="message" /> under
    /// <paramref name="context" /> against <paramref name="publicKey" /> (RFC 8032 section
    /// 5.2.7).
    /// </summary>
    /// <returns>
    /// <c>true</c> when the signature is valid; <c>false</c> when S is not below the group
    /// order L, when the public key does not decode to a point, or when [S]B differs from
    /// R + [k]A.
    /// </returns>
    /// <exception cref="ArgumentException">A span has the wrong length, or the context is longer than <see cref="MaximumContextSize" />.</exception>
    public static bool Verify(
        ReadOnlySpan<byte> publicKey,
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> context,
        ReadOnlySpan<byte> signature)
    {
        RequireSize(publicKey.Length, PublicKeySize, nameof(publicKey));
        RequireSize(signature.Length, SignatureSize, nameof(signature));
        RequireContext(context);
        ReadOnlySpan<byte> encodedR = signature[..Edwards448.EncodedLength];
        ReadOnlySpan<byte> s = signature[Edwards448.EncodedLength..];
        Span<long> negatedKey = stackalloc long[Edwards448.PointLength];
        if (!Scalar448.IsBelowOrder(s) || !Edwards448.TryDecode(negatedKey, publicKey))
        {
            return false;
        }

        Span<byte> hash = stackalloc byte[HashSize];
        Span<byte> challenge = stackalloc byte[Scalar448.EncodedLength];
        HashToScalar(challenge, hash, context, encodedR, publicKey, message);
        Edwards448.Negate(negatedKey);
        Span<long> sum = stackalloc long[Edwards448.PointLength];
        Span<long> product = stackalloc long[Edwards448.PointLength];
        Edwards448.ScalarMultiplyBase(sum, s);
        Edwards448.ScalarMultiply(product, negatedKey, challenge);
        Edwards448.Add(sum, product);
        Span<byte> expectedR = stackalloc byte[Edwards448.EncodedLength];
        Edwards448.Encode(expectedR, sum);
        return CryptographicOperations.FixedTimeEquals(expectedR, encodedR);
    }

    private static void RequireSize(int length, int size, string parameterName)
    {
        if (length != size)
        {
            throw new ArgumentException($"This Ed448 value is {size} bytes; the one given is {length}.", parameterName);
        }
    }

    private static void RequireContext(ReadOnlySpan<byte> context)
    {
        if (context.Length > MaximumContextSize)
        {
            throw new ArgumentException(
                $"An Ed448 context is at most {MaximumContextSize} bytes; the one given is {context.Length}.",
                nameof(context));
        }
    }

    /// <summary>
    /// Hashes the seed with SHAKE256 to 114 bytes and prunes the lower 57 into the secret
    /// scalar (RFC 8032 section 5.2.5, steps 1 and 2); the upper 57 are the nonce prefix.
    /// </summary>
    private static void ExpandPrivateKey(ReadOnlySpan<byte> privateKey, Span<byte> expanded)
    {
        Shake.HashData256(privateKey, expanded);
        expanded[0] &= 0xFC;
        expanded[Scalar448.EncodedLength - 2] |= 0x80;
        expanded[Scalar448.EncodedLength - 1] = 0;
    }

    /// <summary>Encodes [<paramref name="scalar" />]B into <paramref name="encoded" />.</summary>
    private static void MultiplyBaseAndEncode(Span<byte> encoded, ReadOnlySpan<byte> scalar)
    {
        Span<long> point = stackalloc long[Edwards448.PointLength];
        try
        {
            Edwards448.ScalarMultiplyBase(point, scalar);
            Edwards448.Encode(encoded, point);
        }
        finally
        {
            Field448.Clear(point);
        }
    }

    /// <summary>
    /// Sets <paramref name="scalar" /> to SHAKE256(dom4(0, <paramref name="context" />) ||
    /// <paramref name="first" /> || <paramref name="second" /> || <paramref name="message" />,
    /// 114) modulo L, using <paramref name="hash" /> for the digest.
    /// </summary>
    private static void HashToScalar(
        Span<byte> scalar,
        Span<byte> hash,
        ReadOnlySpan<byte> context,
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> message)
    {
        using Shake shake256 = Shake.Create256();
        shake256.AppendData(DomainPrefix);
        shake256.AppendData([0, (byte)context.Length]);
        shake256.AppendData(context);
        shake256.AppendData(first);
        shake256.AppendData(second);
        shake256.AppendData(message);
        shake256.Read(hash);
        Scalar448.Reduce(scalar, hash);
    }
}
