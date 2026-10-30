using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>
/// The client side of VOPRF(P-384, SHA-384), RFC 9497 mode <c>0x01</c>, the privately verifiable token type
/// <c>0x0001</c> uses (RFC 9578 §5): <see cref="Blind"/> hashes the input to the group and multiplies it by a random
/// blind (§3.3.2), and <see cref="Finalize"/> checks the issuer's DLEQ proof (§2.2.2) and unblinds the evaluated
/// element into the PRF output (§3.3.2).
/// </summary>
/// <param name="randomBytes">Gives cryptographically random octets: the blind.</param>
public sealed class VoprfP384Sha384(Func<int, byte[]> randomBytes)
{
    /// <summary>The length of the PRF output, <c>Nh</c>.</summary>
    public const int OutputLength = 48;

    /// <summary>The length of a proof, two scalars <c>c || s</c>.</summary>
    public const int ProofLength = 2 * P384Group.ScalarLength;

    private static readonly byte[] ContextString = [.. "OPRFV1-"u8, 0x01, .. "-P384-SHA384"u8];

    private BigInteger blind;
    private P384Point blindedElement = P384Point.Identity;

    /// <summary>Gets the domain separation tag of HashToGroup, <c>"HashToGroup-" || contextString</c>.</summary>
    internal static byte[] HashToGroupTag { get; } = [.. "HashToGroup-"u8, .. ContextString];

    /// <summary>Gets the domain separation tag of HashToScalar, <c>"HashToScalar-" || contextString</c>.</summary>
    internal static byte[] HashToScalarTag { get; } = [.. "HashToScalar-"u8, .. ContextString];

    /// <summary>
    /// Blinds <paramref name="input"/> (RFC 9497 §3.3.2 Blind): draws the blind in [1, n) and gives
    /// <c>blind · HashToGroup(input)</c>; keeps both for <see cref="Finalize"/>.
    /// </summary>
    /// <param name="input">The PRF input, the token input.</param>
    /// <returns>The blinded element, <see cref="P384Group.ElementLength"/> bytes.</returns>
    public byte[] Blind(byte[] input)
    {
        blind = RandomScalar();
        blindedElement = P384Group.Multiply(blind, P384HashToCurve.HashToCurve(input, HashToGroupTag));
        return P384Group.Encode(blindedElement);
    }

    /// <summary>
    /// Checks the issuer's proof that <paramref name="evaluatedElement"/> is the blinded element multiplied by the key
    /// behind <paramref name="publicKey"/>, then unblinds it and hashes it with <paramref name="input"/> (RFC 9497 §3.3.2 Finalize).
    /// </summary>
    /// <param name="input">The input <see cref="Blind"/> was given.</param>
    /// <param name="publicKey">The issuer's public key, <c>pkS</c>.</param>
    /// <param name="evaluatedElement">The issuer's evaluated element, <see cref="P384Group.ElementLength"/> bytes.</param>
    /// <param name="proof">The issuer's proof, <see cref="ProofLength"/> bytes.</param>
    /// <returns>The <see cref="OutputLength"/>-byte output, or <see langword="null"/> when the element or the proof is not valid.</returns>
    public byte[]? Finalize(byte[] input, P384Point publicKey, byte[] evaluatedElement, byte[] proof)
    {
        ArgumentNullException.ThrowIfNull(input);
        var evaluated = P384Group.Decode(evaluatedElement);
        if (evaluated is null || !VerifyProof(P384Group.Generator, publicKey, [blindedElement], [evaluated], proof))
        {
            return null;
        }

        var inverse = BigInteger.ModPow(blind, P384Group.Order - 2, P384Group.Order);
        var unblinded = P384Group.Encode(P384Group.Multiply(inverse, evaluated));
        return SHA384.HashData([.. LengthPrefixed(input), .. LengthPrefixed(unblinded), .. "Finalize"u8]);
    }

    /// <summary>
    /// Checks a DLEQ proof that <c>B = k·A</c> and <c>D[i] = k·C[i]</c> for one <c>k</c> (RFC 9497 §2.2.2 VerifyProof).
    /// </summary>
    /// <param name="a">The base, the generator.</param>
    /// <param name="b">The public key.</param>
    /// <param name="c">The blinded elements.</param>
    /// <param name="d">The evaluated elements.</param>
    /// <param name="proof">The proof, <c>c || s</c>.</param>
    /// <returns>Whether the proof verifies.</returns>
    internal static bool VerifyProof(P384Point a, P384Point b, P384Point[] c, P384Point[] d, byte[] proof)
    {
        var challenge = P384Group.DecodeScalar(proof.AsSpan(0, Math.Min(proof.Length, P384Group.ScalarLength)));
        var response = P384Group.DecodeScalar(proof.AsSpan(Math.Min(proof.Length, P384Group.ScalarLength)));
        if (challenge is not { } cs || response is not { } s)
        {
            return false;
        }

        var (m, z) = ComputeComposites(b, c, d);
        var t2 = P384Group.Add(P384Group.Multiply(s, a), P384Group.Multiply(cs, b));
        var t3 = P384Group.Add(P384Group.Multiply(s, m), P384Group.Multiply(cs, z));
        try
        {
            return ChallengeScalar(b, m, z, t2, t3) == cs;
        }
        catch (CryptographicException)
        {
            // A crafted proof can make t2 or t3 the identity, which has no encoding: it does not verify.
            return false;
        }
    }

    /// <summary>Combines the blinded and evaluated elements into one pair, weighted by scalars hashed from all of them (RFC 9497 §2.2.1 ComputeComposites).</summary>
    /// <param name="b">The public key.</param>
    /// <param name="c">The blinded elements.</param>
    /// <param name="d">The evaluated elements.</param>
    /// <returns>The composites <c>M</c> and <c>Z</c>.</returns>
    internal static (P384Point M, P384Point Z) ComputeComposites(P384Point b, P384Point[] c, P384Point[] d)
    {
        var seed = SHA384.HashData([.. LengthPrefixed(P384Group.Encode(b)), .. LengthPrefixed([.. "Seed-"u8, .. ContextString])]);
        var (m, z) = (P384Point.Identity, P384Point.Identity);
        for (var index = 0; index < c.Length; index++)
        {
            byte[] transcript =
            [
                .. LengthPrefixed(seed), (byte)(index >> 8), (byte)index,
                .. LengthPrefixed(P384Group.Encode(c[index])), .. LengthPrefixed(P384Group.Encode(d[index])), .. "Composite"u8,
            ];
            var weight = P384HashToCurve.HashToScalar(transcript, HashToScalarTag);
            m = P384Group.Add(P384Group.Multiply(weight, c[index]), m);
            z = P384Group.Add(P384Group.Multiply(weight, d[index]), z);
        }

        return (m, z);
    }

    /// <summary>Hashes the proof's transcript to its challenge scalar (RFC 9497 §2.2.1 GenerateProof, §2.2.2 VerifyProof).</summary>
    /// <param name="b">The public key.</param>
    /// <param name="m">The composite of the blinded elements.</param>
    /// <param name="z">The composite of the evaluated elements.</param>
    /// <param name="t2">The first commitment.</param>
    /// <param name="t3">The second commitment.</param>
    /// <returns>The challenge scalar.</returns>
    /// <exception cref="CryptographicException">One of the points is the identity.</exception>
    internal static BigInteger ChallengeScalar(P384Point b, P384Point m, P384Point z, P384Point t2, P384Point t3)
    {
        byte[] transcript =
        [
            .. LengthPrefixed(P384Group.Encode(b)), .. LengthPrefixed(P384Group.Encode(m)), .. LengthPrefixed(P384Group.Encode(z)),
            .. LengthPrefixed(P384Group.Encode(t2)), .. LengthPrefixed(P384Group.Encode(t3)), .. "Challenge"u8,
        ];
        return P384HashToCurve.HashToScalar(transcript, HashToScalarTag);
    }

    private static byte[] LengthPrefixed(byte[] value) => [(byte)(value.Length >> 8), (byte)value.Length, .. value];

    // RFC 9497 §4.7 RandomScalar: draws Ns octets until they are a scalar in [1, n).
    private BigInteger RandomScalar()
    {
        BigInteger? scalar;
        do
        {
            scalar = P384Group.DecodeScalar(randomBytes(P384Group.ScalarLength));
        }
        while (scalar is not { IsZero: false });

        return scalar.Value;
    }
}
