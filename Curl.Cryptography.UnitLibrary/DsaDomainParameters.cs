using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// DSA's domain parameters p, q and g (FIPS 186-4 section 4.3) with the arithmetic
/// <see cref="DsaSignature" /> does on them: g raised to a power mod p, inversion mod q by
/// Fermat's little theorem, and a hash's leftmost bits reduced mod q.
/// </summary>
/// <remarks>
/// <see cref="TryCreate" /> accepts what OpenSSL's DSA verification accepts, so a
/// certificate or host key the Linux curl takes is taken here too: q of 160, 224 or 256
/// bits, an odd p longer than q and at most 10,000 bits (<c>OPENSSL_DSA_MAX_MODULUS_BITS</c>),
/// and 1 &lt; g &lt; p. It does not test p and q for primality or g for order q, as neither
/// OpenSSL nor Schannel does when it verifies; a bad group only makes signatures fail.
/// The parameters are public, so nothing here is zeroed.
/// </remarks>
internal sealed class DsaDomainParameters
{
    /// <summary>The longest p in bytes: OpenSSL's <c>OPENSSL_DSA_MAX_MODULUS_BITS</c>, 10,000 bits.</summary>
    private const int MaximumPrimeLength = 1250;

    private readonly uint[] generator;
    private readonly byte[] subprimeMinusTwo;

    private DsaDomainParameters(ReadOnlySpan<byte> prime, ReadOnlySpan<byte> subprime, ReadOnlySpan<byte> generatorValue)
    {
        PrimeLength = prime.Length;
        SubprimeLength = subprime.Length;
        Prime = new MontgomeryModulus(prime);
        Subprime = new MontgomeryModulus(subprime);
        generator = new uint[Prime.LimbCount];
        MontgomeryModulus.ToLimbs(generatorValue, generator);
        subprimeMinusTwo = MontgomeryModulus.MinusTwo(subprime);
    }

    /// <summary>p's arithmetic.</summary>
    public MontgomeryModulus Prime { get; }

    /// <summary>q's arithmetic.</summary>
    public MontgomeryModulus Subprime { get; }

    /// <summary>The length of p in bytes, leading zeros removed: the length of y and of every value mod p.</summary>
    public int PrimeLength { get; }

    /// <summary>The length of q in bytes, N / 8: the length of x, k, r and s.</summary>
    public int SubprimeLength { get; }

    /// <summary>
    /// Returns the domain parameters p, q and g, big-endian with any leading zero bytes, or
    /// <see langword="null" /> when they fall outside the sizes and ranges the remarks list.
    /// </summary>
    public static DsaDomainParameters? TryCreate(ReadOnlySpan<byte> prime, ReadOnlySpan<byte> subprime, ReadOnlySpan<byte> generatorValue)
    {
        prime = TrimLeadingZeros(prime);
        subprime = TrimLeadingZeros(subprime);
        generatorValue = TrimLeadingZeros(generatorValue);
        if (!IsValidSubprime(subprime) || !IsValidPrime(prime, subprime.Length) || !IsAboveOne(generatorValue) || generatorValue.Length > prime.Length)
        {
            return null;
        }

        DsaDomainParameters domain = new(prime, subprime, generatorValue);
        return domain.Prime.IsBelowModulus(domain.generator) ? domain : null;
    }

    /// <summary>Returns <paramref name="value" /> without its leading zero bytes; empty for zero.</summary>
    public static ReadOnlySpan<byte> TrimLeadingZeros(ReadOnlySpan<byte> value)
    {
        int first = value.IndexOfAnyExcept((byte)0);
        return first < 0 ? [] : value[first..];
    }

    /// <summary>Sets <paramref name="result" /> (p's limbs) to g^<paramref name="exponent" /> mod p.</summary>
    /// <param name="exponent">The big-endian exponent; only its length shapes the running time.</param>
    /// <param name="result">Receives the power.</param>
    public void RaiseGenerator(ReadOnlySpan<byte> exponent, Span<uint> result) => Prime.Exponentiate(generator, exponent, result);

    /// <summary>Sets <paramref name="result" /> to <paramref name="value" />^(q - 2) mod q: the inverse of a nonzero value below q.</summary>
    public void InvertModuloSubprime(ReadOnlySpan<uint> value, Span<uint> result) => Subprime.Exponentiate(value, subprimeMinusTwo, result);

    /// <summary>
    /// Sets <paramref name="result" /> (q's limbs) to z mod q, where z is the leftmost
    /// min(N, outlen) bits of <paramref name="hash" /> (FIPS 186-4 section 4.6; RFC 6979's
    /// bits2int). N is a whole number of bytes, so that is the first N / 8 bytes.
    /// </summary>
    public void ReduceHash(ReadOnlySpan<byte> hash, Span<uint> result)
    {
        uint[] leftmost = new uint[Subprime.LimbCount];
        MontgomeryModulus.ToLimbs(hash[..Math.Min(hash.Length, SubprimeLength)], leftmost);
        Subprime.Reduce(leftmost, result);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(leftmost.AsSpan()));
    }

    /// <summary>Whether q is 160, 224 or 256 bits and odd.</summary>
    private static bool IsValidSubprime(ReadOnlySpan<byte> subprime) =>
        subprime.Length is 20 or 28 or 32 && subprime[0] >= 0x80 && (subprime[^1] & 1) == 1;

    /// <summary>Whether p is longer than q, at most <see cref="MaximumPrimeLength" /> bytes, and odd.</summary>
    private static bool IsValidPrime(ReadOnlySpan<byte> prime, int subprimeLength) =>
        prime.Length > subprimeLength && prime.Length <= MaximumPrimeLength && (prime[^1] & 1) == 1;

    /// <summary>Whether the trimmed <paramref name="value" /> is above 1.</summary>
    private static bool IsAboveOne(ReadOnlySpan<byte> value) => value.Length > 1 || (value.Length == 1 && value[0] > 1);
}
