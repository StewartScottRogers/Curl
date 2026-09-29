using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The X448 function and the Diffie-Hellman key agreement built on it (RFC 7748
/// sections 5 and 6.2): a private key is 56 bytes, clamped as section 5 specifies, and a
/// public key or shared secret is a 56-byte little-endian u-coordinate on Curve448.
/// </summary>
/// <remarks>
/// Constant-time in the private key: the Montgomery ladder visits all 448 scalar bits
/// with the same operations, the conditional swap is a mask rather than a branch, no
/// array index or loop bound depends on a secret, and the final inversion is a fixed
/// exponentiation. Every secret intermediate is zeroed before returning.
/// </remarks>
public static class X448
{
    /// <summary>The length in bytes of a private key, a public key and a shared secret.</summary>
    public const int KeySize = 56;

    /// <summary>(A - 2) / 4 for Curve448's A = 156326, the ladder's a24 (RFC 7748 section 5).</summary>
    private const ushort A24 = 39081;

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
    /// Computes the public key X448(<paramref name="privateKey" />, 5) into
    /// <paramref name="publicKey" /> (RFC 7748 section 6.2).
    /// </summary>
    /// <exception cref="ArgumentException">A span is not <see cref="KeySize" /> bytes.</exception>
    public static void ComputePublicKey(ReadOnlySpan<byte> privateKey, Span<byte> publicKey)
    {
        RequireKeySize(privateKey.Length, nameof(privateKey));
        RequireKeySize(publicKey.Length, nameof(publicKey));
        Span<byte> basePoint = stackalloc byte[KeySize];
        basePoint.Clear();
        basePoint[0] = 5;
        ScalarMultiply(privateKey, basePoint, publicKey);
    }

    /// <summary>
    /// Computes the shared secret X448(<paramref name="privateKey" />,
    /// <paramref name="peerPublicKey" />) into <paramref name="sharedSecret" />.
    /// </summary>
    /// <returns>
    /// <c>false</c>, with <paramref name="sharedSecret" /> all zero, when the result is the
    /// all-zero value a low-order peer key produces (RFC 7748 section 6.2), which the caller
    /// must reject; otherwise <c>true</c>. The check reads every byte whatever they hold.
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
            throw new ArgumentException($"X448 keys are {KeySize} bytes; this one is {length}.", parameterName);
        }
    }

    /// <summary>The X448 function of RFC 7748 section 5: clamp, ladder, encode x2 / z2.</summary>
    private static void ScalarMultiply(ReadOnlySpan<byte> scalar, ReadOnlySpan<byte> uCoordinate, Span<byte> result)
    {
        Span<byte> clamped = stackalloc byte[KeySize];
        Span<long> ladder = stackalloc long[5 * Field448.LimbCount];
        Span<long> x1 = ladder[..Field448.LimbCount];
        Span<long> x2 = ladder.Slice(Field448.LimbCount, Field448.LimbCount);
        Span<long> z2 = ladder.Slice(2 * Field448.LimbCount, Field448.LimbCount);
        Span<long> x3 = ladder.Slice(3 * Field448.LimbCount, Field448.LimbCount);
        Span<long> z3 = ladder.Slice(4 * Field448.LimbCount, Field448.LimbCount);
        try
        {
            scalar.CopyTo(clamped);
            clamped[0] &= 252;
            clamped[KeySize - 1] |= 128;
            Field448.Decode(x1, uCoordinate);
            Field448.SetSmall(x2, 1);
            Field448.SetSmall(z2, 0);
            x1.CopyTo(x3);
            Field448.SetSmall(z3, 1);
            RunLadder(clamped, x1, x2, z2, x3, z3);
            Field448.Invert(z2, z2);
            Field448.Multiply(x2, x2, z2);
            Field448.Encode(result, x2);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clamped);
            Field448.Clear(ladder);
        }
    }

    /// <summary>
    /// The Montgomery ladder of RFC 7748 section 5 over bits 447 down to 0 of the clamped
    /// scalar, swapping by mask so the secret bit never picks a branch or an index.
    /// </summary>
    private static void RunLadder(
        ReadOnlySpan<byte> clamped,
        ReadOnlySpan<long> x1,
        Span<long> x2,
        Span<long> z2,
        Span<long> x3,
        Span<long> z3)
    {
        uint swap = 0;
        for (int bit = (8 * KeySize) - 1; bit >= 0; bit--)
        {
            uint scalarBit = (uint)(clamped[bit >> 3] >> (bit & 7)) & 1u;
            swap ^= scalarBit;
            Field448.ConditionalSwap(x2, x3, swap);
            Field448.ConditionalSwap(z2, z3, swap);
            swap = scalarBit;
            LadderStep(x1, x2, z2, x3, z3);
        }

        Field448.ConditionalSwap(x2, x3, swap);
        Field448.ConditionalSwap(z2, z3, swap);
    }

    /// <summary>One differential add-and-double step, as RFC 7748 section 5 writes it.</summary>
    private static void LadderStep(
        ReadOnlySpan<long> x1,
        Span<long> x2,
        Span<long> z2,
        Span<long> x3,
        Span<long> z3)
    {
        Span<long> scratch = stackalloc long[9 * Field448.LimbCount];
        Span<long> a = scratch[..Field448.LimbCount];
        Span<long> aa = scratch.Slice(Field448.LimbCount, Field448.LimbCount);
        Span<long> b = scratch.Slice(2 * Field448.LimbCount, Field448.LimbCount);
        Span<long> bb = scratch.Slice(3 * Field448.LimbCount, Field448.LimbCount);
        Span<long> e = scratch.Slice(4 * Field448.LimbCount, Field448.LimbCount);
        Span<long> c = scratch.Slice(5 * Field448.LimbCount, Field448.LimbCount);
        Span<long> d = scratch.Slice(6 * Field448.LimbCount, Field448.LimbCount);
        Span<long> da = scratch.Slice(7 * Field448.LimbCount, Field448.LimbCount);
        Span<long> cb = scratch.Slice(8 * Field448.LimbCount, Field448.LimbCount);
        try
        {
            Field448.Add(a, x2, z2);
            Field448.Square(aa, a);
            Field448.Subtract(b, x2, z2);
            Field448.Square(bb, b);
            Field448.Subtract(e, aa, bb);
            Field448.Add(c, x3, z3);
            Field448.Subtract(d, x3, z3);
            Field448.Multiply(da, d, a);
            Field448.Multiply(cb, c, b);
            Field448.Add(x3, da, cb);
            Field448.Square(x3, x3);
            Field448.Subtract(z3, da, cb);
            Field448.Square(z3, z3);
            Field448.Multiply(z3, z3, x1);
            Field448.Multiply(x2, aa, bb);
            Field448.SetSmall(a, A24);
            Field448.Multiply(z2, e, a);
            Field448.Add(z2, z2, aa);
            Field448.Multiply(z2, z2, e);
        }
        finally
        {
            Field448.Clear(scratch);
        }
    }
}
