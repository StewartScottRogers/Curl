using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// An RSA private key held in its Chinese Remainder Theorem form, applying the raw private
/// operation m^d mod n: PKCS #1's RSASP1 (RFC 8017 section 5.2.1), which is RSADP too.
/// It exists for the signature the BCL cannot make - TLS 1.0 and 1.1's PKCS #1 block over
/// MD5 and SHA-1 with no DigestInfo - since <see cref="RSA.SignHash(byte[], HashAlgorithmName, RSASignaturePadding)" />
/// always adds one. The caller pads the message representative; this type only exponentiates.
/// </summary>
/// <remarks>
/// <para>
/// Each operation is blinded, as OpenSSL blinds it: with a random r, the key exponentiates
/// m * r^e instead of m and multiplies the result by r^-1, which it computes by Fermat's
/// little theorem through the same CRT exponentiation (r^(p-2) mod p, r^(q-2) mod q). The
/// result is checked by re-applying the public exponent before it is written, so a fault
/// during the computation can never release a signature that factors n.
/// </para>
/// <para>
/// Constant-time in the key and r: the exponentiations are <see cref="MontgomeryModulus" />'s
/// fixed-window ladder over fixed-width limbs, reductions and the CRT recombination
/// (Garner's formula) are masked limb arithmetic, and only the key's byte lengths, which
/// are public, shape the running time. The key is zeroed on <see cref="Dispose" />, and
/// every intermediate before each call returns.
/// </para>
/// </remarks>
public sealed class RsaCrtPrivateKey : IDisposable
{
    /// <summary>
    /// The bytes of blinding randomness beyond the modulus length: r is drawn from
    /// <see cref="BlindingRandomLength" /> bytes reduced mod n, so its bias is below 2^-64.
    /// </summary>
    private const int BlindingMargin = 8;

    private readonly MontgomeryModulus modulusN;
    private readonly MontgomeryModulus primeP;
    private readonly MontgomeryModulus primeQ;
    private readonly uint[] q;
    private readonly uint[] qInverse;
    private readonly byte[] publicExponent;
    private readonly byte[] exponentP;
    private readonly byte[] exponentQ;
    private readonly byte[] inverseExponentP;
    private readonly byte[] inverseExponentQ;
    private bool disposed;

    /// <summary>Copies the key from <paramref name="parameters" />, which must carry its CRT values.</summary>
    /// <param name="parameters">
    /// An RSA private key as <see cref="RSA.ExportParameters(bool)" /> gives it: Modulus,
    /// Exponent, P, Q, DP, DQ and InverseQ. Their consistency is not checked here; an
    /// inconsistent key fails every <see cref="ApplyPrivateExponent(ReadOnlySpan{byte}, Span{byte})" />
    /// at the fault check.
    /// </param>
    /// <exception cref="ArgumentException">A CRT value or the public part is missing.</exception>
    public RsaCrtPrivateKey(RSAParameters parameters)
    {
        byte[] modulus = Require(parameters.Modulus, nameof(parameters.Modulus));
        byte[] p = Require(parameters.P, nameof(parameters.P));
        byte[] qBytes = Require(parameters.Q, nameof(parameters.Q));
        publicExponent = Require(parameters.Exponent, nameof(parameters.Exponent)).ToArray();
        exponentP = Require(parameters.DP, nameof(parameters.DP)).ToArray();
        exponentQ = Require(parameters.DQ, nameof(parameters.DQ)).ToArray();
        byte[] inverse = Require(parameters.InverseQ, nameof(parameters.InverseQ));
        ModulusLength = modulus.Length;
        modulusN = new MontgomeryModulus(modulus);
        primeP = new MontgomeryModulus(p);
        primeQ = new MontgomeryModulus(qBytes);
        q = Limbs(qBytes, primeQ.LimbCount);
        qInverse = new uint[primeP.LimbCount];
        uint[] inverseLimbs = Limbs(inverse, (inverse.Length + 3) / 4);
        primeP.Reduce(inverseLimbs, qInverse);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(inverseLimbs.AsSpan()));
        inverseExponentP = MontgomeryModulus.MinusTwo(p);
        inverseExponentQ = MontgomeryModulus.MinusTwo(qBytes);
    }

    /// <summary>The length in bytes of n, and so of every message representative and result.</summary>
    public int ModulusLength { get; }

    /// <summary>The length in bytes of the blinding randomness the overload that takes it needs.</summary>
    public int BlindingRandomLength => ModulusLength + BlindingMargin;

    /// <summary>
    /// Writes s = m^d mod n for the message representative <paramref name="message" />,
    /// blinded with fresh random bytes.
    /// </summary>
    /// <param name="message">m, big-endian, exactly <see cref="ModulusLength" /> bytes, below n.</param>
    /// <param name="destination">Receives s, big-endian, exactly <see cref="ModulusLength" /> bytes.</param>
    /// <exception cref="ArgumentException">A length is wrong, or m is not below n.</exception>
    /// <exception cref="CryptographicException">The result failed the fault check: the key is inconsistent.</exception>
    /// <exception cref="ObjectDisposedException">The key was disposed.</exception>
    public void ApplyPrivateExponent(ReadOnlySpan<byte> message, Span<byte> destination)
    {
        byte[] blindingRandom = RandomNumberGenerator.GetBytes(BlindingRandomLength);
        try
        {
            ApplyPrivateExponent(message, blindingRandom, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blindingRandom);
        }
    }

    /// <summary>
    /// Writes s = m^d mod n for the message representative <paramref name="message" />,
    /// blinded with r taken from <paramref name="blindingRandom" />. s does not depend on r.
    /// </summary>
    /// <param name="message">m, big-endian, exactly <see cref="ModulusLength" /> bytes, below n.</param>
    /// <param name="blindingRandom">
    /// <see cref="BlindingRandomLength" /> random bytes, reduced mod n to give r. An r that
    /// shares a factor with n (probability about 2^-(bits of p)) fails the fault check.
    /// </param>
    /// <param name="destination">Receives s, big-endian, exactly <see cref="ModulusLength" /> bytes.</param>
    /// <exception cref="ArgumentException">A length is wrong, or m is not below n.</exception>
    /// <exception cref="CryptographicException">The result failed the fault check: the key is inconsistent or r shares a factor with n.</exception>
    /// <exception cref="ObjectDisposedException">The key was disposed.</exception>
    public void ApplyPrivateExponent(ReadOnlySpan<byte> message, ReadOnlySpan<byte> blindingRandom, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        RequireLength(message, ModulusLength, nameof(message));
        RequireLength(blindingRandom, BlindingRandomLength, nameof(blindingRandom));
        RequireLength(destination, ModulusLength, nameof(destination));
        int n = modulusN.LimbCount;
        int wide = primeP.LimbCount + primeQ.LimbCount + 1;
        uint[] work = new uint[(4 * n) + (2 * wide) + ((BlindingRandomLength + 3) / 4)];
        Span<uint> m = work.AsSpan(0, n);
        Span<uint> r = work.AsSpan(n, n);
        Span<uint> blinded = work.AsSpan(2 * n, n);
        Span<uint> check = work.AsSpan(3 * n, n);
        Span<uint> signature = work.AsSpan(4 * n, wide);
        Span<uint> rInverse = work.AsSpan((4 * n) + wide, wide);
        Span<uint> randomLimbs = work.AsSpan((4 * n) + (2 * wide));
        try
        {
            MontgomeryModulus.ToLimbs(message, m);
            RequireBelowModulus(m);
            MontgomeryModulus.ToLimbs(blindingRandom, randomLimbs);
            modulusN.Reduce(randomLimbs, r);
            ExponentiateByCrt(r, inverseExponentP, inverseExponentQ, rInverse);
            modulusN.Exponentiate(r, publicExponent, blinded);
            modulusN.MultiplyModulo(blinded, m, blinded);
            ExponentiateByCrt(blinded, exponentP, exponentQ, signature);
            modulusN.MultiplyModulo(signature[..n], signature[..n], rInverse[..n]);
            modulusN.Exponentiate(signature[..n], publicExponent, check);
            if (!CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(check), MemoryMarshal.AsBytes(m)))
            {
                throw new CryptographicException("The RSA private-key operation failed its check against the public exponent; the key is inconsistent.");
            }

            MontgomeryModulus.FromLimbs(signature, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(work.AsSpan()));
        }
    }

    /// <summary>Zeroes the key; any later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        primeP.Clear();
        primeQ.Clear();
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(q.AsSpan()));
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(qInverse.AsSpan()));
        CryptographicOperations.ZeroMemory(exponentP);
        CryptographicOperations.ZeroMemory(exponentQ);
        CryptographicOperations.ZeroMemory(inverseExponentP);
        CryptographicOperations.ZeroMemory(inverseExponentQ);
        disposed = true;
    }

    private static byte[] Require(byte[]? value, string name) =>
        value is { Length: > 0 } ? value : throw new ArgumentException($"The RSA parameters lack {name}.", nameof(RSAParameters));

    private static void RequireLength(ReadOnlySpan<byte> value, int length, string name)
    {
        if (value.Length != length)
        {
            throw new ArgumentException($"{name} must be {length} bytes; it is {value.Length}.", name);
        }
    }

    private static uint[] Limbs(ReadOnlySpan<byte> bigEndian, int limbCount)
    {
        uint[] limbs = new uint[limbCount];
        MontgomeryModulus.ToLimbs(bigEndian, limbs);
        return limbs;
    }

    /// <summary>
    /// Sets <paramref name="result" /> (|p| + |q| + 1 limbs) to <paramref name="value" />^d mod n
    /// for the d whose CRT exponents are <paramref name="exponentModP" /> and
    /// <paramref name="exponentModQ" />: m1 = v^dP mod p, m2 = v^dQ mod q, then Garner's
    /// h = qInv * (m1 - m2) mod p and m2 + h * q.
    /// </summary>
    private void ExponentiateByCrt(ReadOnlySpan<uint> value, byte[] exponentModP, byte[] exponentModQ, Span<uint> result)
    {
        int pLimbs = primeP.LimbCount;
        int qLimbs = primeQ.LimbCount;
        uint[] work = new uint[(3 * pLimbs) + qLimbs];
        Span<uint> m1 = work.AsSpan(0, pLimbs);
        Span<uint> m2 = work.AsSpan(pLimbs, qLimbs);
        Span<uint> m2ModP = work.AsSpan(pLimbs + qLimbs, pLimbs);
        Span<uint> h = work.AsSpan((2 * pLimbs) + qLimbs, pLimbs);
        try
        {
            primeP.Reduce(value, m1);
            primeP.Exponentiate(m1, exponentModP, m1);
            primeQ.Reduce(value, m2);
            primeQ.Exponentiate(m2, exponentModQ, m2);
            primeP.Reduce(m2, m2ModP);
            primeP.Subtract(h, m1, m2ModP);
            primeP.MultiplyModulo(h, h, qInverse);
            AddProductOfHAndQ(m2, h, result);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(work.AsSpan()));
        }
    }

    /// <summary>Sets <paramref name="result" /> to <paramref name="m2" /> + <paramref name="h" /> * q by schoolbook multiplication over every limb.</summary>
    private void AddProductOfHAndQ(ReadOnlySpan<uint> m2, ReadOnlySpan<uint> h, Span<uint> result)
    {
        result.Clear();
        m2.CopyTo(result);
        for (int i = 0; i < h.Length; i++)
        {
            ulong carry = 0;
            for (int j = 0; j < q.Length; j++)
            {
                ulong sum = result[i + j] + ((ulong)h[i] * q[j]) + carry;
                result[i + j] = (uint)sum;
                carry = sum >> 32;
            }

            for (int k = i + q.Length; k < result.Length; k++)
            {
                ulong sum = result[k] + carry;
                result[k] = (uint)sum;
                carry = sum >> 32;
            }
        }
    }

    /// <summary>Throws when the public message representative <paramref name="m" /> is not below n.</summary>
    private void RequireBelowModulus(ReadOnlySpan<uint> m)
    {
        if (!modulusN.IsBelowModulus(m))
        {
            throw new ArgumentException("The message representative is not below the modulus (RFC 8017 section 5.2.1).", "message");
        }
    }
}
