using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The X25519 function and the Diffie-Hellman key agreement built on it (RFC 7748
/// sections 5 and 6.1): a private key is 32 bytes, clamped as section 5 specifies, and a
/// public key or shared secret is a 32-byte little-endian u-coordinate on Curve25519.
/// </summary>
/// <remarks>
/// Constant-time in the private key: the Montgomery ladder visits all 255 scalar bits
/// with the same operations, the conditional swap is a mask rather than a branch, no
/// array index or loop bound depends on a secret, and the final inversion is a fixed
/// exponentiation. Every secret intermediate is zeroed before returning.
/// </remarks>
public static class X25519
{
    /// <summary>The length in bytes of a private key, a public key and a shared secret.</summary>
    public const int KeySize = 32;

    /// <summary>(A - 2) / 4 for Curve25519's A = 486662, the ladder's a24 (RFC 7748 section 5).</summary>
    private const uint A24 = 121665;

    /// <summary>The field elements one ladder step works in: A, AA, B, BB, E, C, D, DA and CB.</summary>
    private const int LadderScratchElements = 9;

    /// <summary>
    /// Fills <paramref name="privateKey" /> with <see cref="KeySize" /> bytes from
    /// <see cref="RandomNumberGenerator" />; clamping happens when the key is used.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="privateKey" /> is not <see cref="KeySize" /> bytes.</exception>
    public static void GeneratePrivateKey(Span<byte> privateKey)
    {
        RequireKeySize(privateKey.Length, nameof(privateKey));
        RandomNumberGenerator.Fill(privateKey);
    }

    /// <summary>
    /// Computes the public key X25519(<paramref name="privateKey" />, 9) into
    /// <paramref name="publicKey" /> (RFC 7748 section 6.1).
    /// </summary>
    /// <exception cref="ArgumentException">A span is not <see cref="KeySize" /> bytes.</exception>
    public static void ComputePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        RequireKeySize(privateKey.Length, nameof(privateKey));
        RequireKeySize(publicKey.Length, nameof(publicKey));
        Span<byte> basePoint = stackalloc byte[KeySize];
        basePoint.Clear();
        basePoint[0] = 9;
        ScalarMultiply(privateKey, basePoint, publicKey);
    }

    /// <summary>
    /// Computes the shared secret X25519(<paramref name="privateKey" />,
    /// <paramref name="peerPublicKey" />) into <paramref name="sharedSecret" />.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="sharedSecret" /> all zero, when the result is the
    /// all-zero value a low-order peer key produces (RFC 7748 section 6.1, RFC 8731
    /// section 3), which the caller must reject; otherwise <c>true</c>. The check reads
    /// every byte whatever they hold.
    /// </returns>
    /// <exception cref="ArgumentException">A span is not <see cref="KeySize" /> bytes.</exception>
    public static bool TryComputeSharedSecret(
        ReadOnlySpan<byte> privateKey,
        ReadOnlySpan<byte> peerPublicKey,
        Span<byte> sharedSecret)
    {
        RequireKeySize(privateKey.Length, nameof(privateKey));
        RequireKeySize(peerPublicKey.Length, nameof(peerPublicKey));
        RequireKeySize(sharedSecret.Length, nameof(sharedSecret));
        ScalarMultiply(privateKey, peerPublicKey, sharedSecret);
        return !ConstantTime.IsAllZero(sharedSecret);
    }

    private static void RequireKeySize(int length, string parameterName)
    {
        if (length != KeySize)
        {
            throw new ArgumentException($"X25519 keys are {KeySize} bytes; this one is {length}.", parameterName);
        }
    }

    /// <summary>The X25519 function of RFC 7748 section 5: clamp, ladder, encode x2 / z2.</summary>
    private static void ScalarMultiply(ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> uCoordinate, Span<byte> result)
    {
        Span<byte> clamped = stackalloc byte[KeySize];
        Span<long> ladder = stackalloc long[(5 + LadderScratchElements) * Field25519.LimbCount];
        Span<long> x1 = ladder[..Field25519.LimbCount];
        Span<long> x2 = ladder.Slice(Field25519.LimbCount, Field25519.LimbCount);
        Span<long> z2 = ladder.Slice(2 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> x3 = ladder.Slice(3 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> z3 = ladder.Slice(4 * Field25519.LimbCount, Field25519.LimbCount);
        try
        {
            scalar.CopyTo(clamped);
            clamped[0] &= 248;
            clamped[KeySize - 1] &= 127;
            clamped[KeySize - 1] |= 64;
            Field25519.Decode(x1, uCoordinate);
            Field25519.SetSmall(x2, 1);
            Field25519.SetSmall(z2, 0);
            x1.CopyTo(x3);
            Field25519.SetSmall(z3, 1);
            RunLadder(clamped, x1, x2, z2, x3, z3, ladder[(5 * Field25519.LimbCount)..]);
            Field25519.Invert(z2, z2);
            Field25519.Multiply(x2, x2, z2);
            Field25519.Encode(result, x2);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clamped);
            Field25519.Clear(ladder);
        }
    }

    /// <summary>
    /// The Montgomery ladder of RFC 7748 section 5 over bits 254 down to 0 of the clamped
    /// scalar, swapping by mask so the secret bit never picks a branch or an index.
    /// </summary>
    private static void RunLadder(
        ReadOnlySpan<byte> clamped,
        ReadOnlySpan<long> x1,
        Span<long> x2,
        Span<long> z2,
        Span<long> x3,
        Span<long> z3,
        Span<long> scratch)
    {
        uint swap = 0;
        for (int bit = 254; bit >= 0; bit--)
        {
            uint scalarBit = (uint)(clamped[bit >> 3] >> (bit & 7)) & 1u;
            swap ^= scalarBit;
            Field25519.ConditionalSwap(x2, x3, swap);
            Field25519.ConditionalSwap(z2, z3, swap);
            swap = scalarBit;
            LadderStep(x1, x2, z2, x3, z3, scratch);
        }

        Field25519.ConditionalSwap(x2, x3, swap);
        Field25519.ConditionalSwap(z2, z3, swap);
    }

    /// <summary>One differential add-and-double step, as RFC 7748 section 5 writes it.</summary>
    private static void LadderStep(
        ReadOnlySpan<long> x1,
        Span<long> x2,
        Span<long> z2,
        Span<long> x3,
        Span<long> z3,
        Span<long> scratch)
    {
        Span<long> a = scratch[..Field25519.LimbCount];
        Span<long> aa = scratch.Slice(Field25519.LimbCount, Field25519.LimbCount);
        Span<long> b = scratch.Slice(2 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> bb = scratch.Slice(3 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> e = scratch.Slice(4 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> c = scratch.Slice(5 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> d = scratch.Slice(6 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> da = scratch.Slice(7 * Field25519.LimbCount, Field25519.LimbCount);
        Span<long> cb = scratch.Slice(8 * Field25519.LimbCount, Field25519.LimbCount);
        Field25519.Add(a, x2, z2);
        Field25519.Square(aa, a);
        Field25519.Subtract(b, x2, z2);
        Field25519.Square(bb, b);
        Field25519.Subtract(e, aa, bb);
        Field25519.Add(c, x3, z3);
        Field25519.Subtract(d, x3, z3);
        Field25519.Multiply(da, d, a);
        Field25519.Multiply(cb, c, b);
        Field25519.Add(x3, da, cb);
        Field25519.Square(x3, x3);
        Field25519.Subtract(z3, da, cb);
        Field25519.Square(z3, z3);
        Field25519.Multiply(z3, z3, x1);
        Field25519.Multiply(x2, aa, bb);
        Field25519.MultiplySmall(z2, e, A24);
        Field25519.Add(z2, z2, aa);
        Field25519.Multiply(z2, z2, e);
    }
}
