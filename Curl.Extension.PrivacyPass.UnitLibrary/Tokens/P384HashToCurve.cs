using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// Hashing to the P-384 group (RFC 9380): <c>expand_message_xmd</c> with SHA-384 (§5.3.1), <c>hash_to_field</c> with
/// L = 72 (§5.2), the simplified SWU map with Z = -12 (§6.6.2) and the random-oracle suite
/// <c>P384_XMD:SHA-384_SSWU_RO_</c> (§8.3), whose cofactor is 1.
/// </summary>
public static class P384HashToCurve
{
    /// <summary>The length of the uniform bytes each field element is drawn from, <c>L</c>.</summary>
    public const int FieldElementLength = 72;

    // SHA-384's block size, s_in_bytes.
    private const int BlockLength = 128;

    /// <summary>Gives <paramref name="length"/> uniform bytes for <paramref name="message"/> (RFC 9380 §5.3.1).</summary>
    /// <param name="message">The message.</param>
    /// <param name="domainSeparationTag">The domain separation tag, at most 255 bytes.</param>
    /// <param name="length">The number of bytes, at most 255 · 48.</param>
    /// <returns>The bytes.</returns>
    public static byte[] ExpandMessageXmd(ReadOnlySpan<byte> message, ReadOnlySpan<byte> domainSeparationTag, int length)
    {
        byte[] dstPrime = [.. domainSeparationTag, (byte)domainSeparationTag.Length];
        var b0 = SHA384.HashData([.. new byte[BlockLength], .. message, (byte)(length >> 8), (byte)length, 0x00, .. dstPrime]);
        var output = new byte[length];
        var previous = new byte[SHA384.HashSizeInBytes];
        for (var index = 1; (index - 1) * SHA384.HashSizeInBytes < length; index++)
        {
            var mixed = new byte[SHA384.HashSizeInBytes];
            for (var octet = 0; octet < mixed.Length; octet++)
            {
                mixed[octet] = (byte)(b0[octet] ^ previous[octet]);
            }

            previous = SHA384.HashData([.. mixed, (byte)index, .. dstPrime]);
            var offset = (index - 1) * SHA384.HashSizeInBytes;
            previous.AsSpan(0, Math.Min(previous.Length, length - offset)).CopyTo(output.AsSpan(offset));
        }

        return output;
    }

    /// <summary>Hashes <paramref name="message"/> to <paramref name="count"/> elements of the integers modulo <paramref name="modulus"/> (RFC 9380 §5.2).</summary>
    /// <param name="message">The message.</param>
    /// <param name="domainSeparationTag">The domain separation tag.</param>
    /// <param name="count">The number of elements.</param>
    /// <param name="modulus">The modulus: the field prime, or the group order for a scalar.</param>
    /// <returns>The elements.</returns>
    public static BigInteger[] HashToField(ReadOnlySpan<byte> message, ReadOnlySpan<byte> domainSeparationTag, int count, BigInteger modulus)
    {
        var uniform = ExpandMessageXmd(message, domainSeparationTag, count * FieldElementLength);
        var elements = new BigInteger[count];
        for (var index = 0; index < count; index++)
        {
            var slice = uniform.AsSpan(index * FieldElementLength, FieldElementLength);
            elements[index] = new BigInteger(slice, isUnsigned: true, isBigEndian: true) % modulus;
        }

        return elements;
    }

    /// <summary>Hashes <paramref name="message"/> to a point of P-384 (RFC 9380 §3 hash_to_curve, suite <c>P384_XMD:SHA-384_SSWU_RO_</c>).</summary>
    /// <param name="message">The message.</param>
    /// <param name="domainSeparationTag">The domain separation tag.</param>
    /// <returns>The point.</returns>
    public static P384Point HashToCurve(ReadOnlySpan<byte> message, ReadOnlySpan<byte> domainSeparationTag)
    {
        var u = HashToField(message, domainSeparationTag, 2, P384Group.Prime);
        return P384Group.Add(MapToCurve(u[0]), MapToCurve(u[1]));
    }

    /// <summary>Hashes <paramref name="message"/> to a scalar modulo the group order (RFC 9497 §4.4 HashToScalar).</summary>
    /// <param name="message">The message.</param>
    /// <param name="domainSeparationTag">The domain separation tag.</param>
    /// <returns>The scalar.</returns>
    public static BigInteger HashToScalar(ReadOnlySpan<byte> message, ReadOnlySpan<byte> domainSeparationTag) =>
        HashToField(message, domainSeparationTag, 1, P384Group.Order)[0];

    /// <summary>Maps a field element to a point of P-384 by the simplified SWU method with Z = -12 (RFC 9380 §6.6.2).</summary>
    /// <param name="u">The field element, in [0, p).</param>
    /// <returns>The point.</returns>
    public static P384Point MapToCurve(BigInteger u)
    {
        var p = P384Group.Prime;
        var z = p - 12;
        var zu2 = z * u % p * u % p;
        var tv1 = P384Group.Inverse(((zu2 * zu2) + zu2) % p);
        var x1 = tv1.IsZero
            ? P384Group.B * P384Group.Inverse(z * P384Group.A % p) % p
            : (p - P384Group.B) * P384Group.Inverse(P384Group.A) % p * (1 + tv1) % p;
        var (x, y) = P384Group.SquareRoot(P384Group.CurveRightHandSide(x1)) is { } root
            ? (x1, root)
            : SecondCandidate(zu2 * x1 % p);
        return new(x, u.IsEven == y.IsEven ? y : (p - y) % p);
    }

    // When g(x1) is not a square, g(x2) for x2 = Z·u^2·x1 is (RFC 9380 §6.6.2).
    private static (BigInteger X, BigInteger Y) SecondCandidate(BigInteger x2) =>
        (x2, P384Group.SquareRoot(P384Group.CurveRightHandSide(x2))!.Value);
}
