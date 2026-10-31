using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Curl.Extension.Checksum;

/// <summary>
/// The constants of FIPS 180-4's SHA-2 family, computed as the standard defines them (§4.2, §5.3) rather than
/// typed in: fractional parts of the square and cube roots of the first primes.
/// </summary>
internal static class Sha2Constants
{
    private static readonly int[] Primes = FirstPrimes(80);

    /// <summary>Gets the 64 round constants of SHA-256 and SHA-224 (§4.2.2): the first 32 fractional bits of the cube roots of the first 64 primes.</summary>
    public static uint[] RoundConstants256 { get; } = [.. Primes.Take(64).Select(prime => (uint)(FractionalBits(prime, 3) >> 32))];

    /// <summary>Gets the 80 round constants of the SHA-512 family (§4.2.3): the first 64 fractional bits of the cube roots of the first 80 primes.</summary>
    public static ulong[] RoundConstants512 { get; } = [.. Primes.Select(prime => FractionalBits(prime, 3))];

    /// <summary>Gets SHA-256's initial hash value (§5.3.3): the first 32 fractional bits of the square roots of the first 8 primes.</summary>
    public static uint[] InitialHash256 { get; } = [.. Primes.Take(8).Select(prime => (uint)(FractionalBits(prime, 2) >> 32))];

    /// <summary>Gets SHA-224's initial hash value (§5.3.2): the second 32 fractional bits of the square roots of the 9th to 16th primes.</summary>
    public static uint[] InitialHash224 { get; } = [.. Primes.Skip(8).Take(8).Select(prime => (uint)FractionalBits(prime, 2))];

    /// <summary>Gets SHA-512's initial hash value (§5.3.5): the first 64 fractional bits of the square roots of the first 8 primes.</summary>
    public static ulong[] InitialHash512 { get; } = [.. Primes.Take(8).Select(prime => FractionalBits(prime, 2))];

    /// <summary>
    /// Gives the initial hash value of SHA-512/<paramref name="digestBits" /> by the generation function of §5.3.6: the
    /// SHA-512 of the text <c>SHA-512/t</c>, with each word of SHA-512's initial hash value xor <c>a5a5a5a5a5a5a5a5</c>.
    /// </summary>
    /// <param name="digestBits">The digest length in bits, 224 or 256.</param>
    /// <returns>The eight initial hash words.</returns>
    public static ulong[] InitialHash512Truncated(int digestBits)
    {
        var generator = new Sha512Hash([.. InitialHash512.Select(word => word ^ 0xa5a5a5a5a5a5a5a5UL)], 64);
        generator.AppendData(Encoding.ASCII.GetBytes($"SHA-512/{digestBits}"));
        var digest = generator.GetHashAndReset();
        return [.. Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(index * 8)))];
    }

    private static int[] FirstPrimes(int count)
    {
        var primes = new List<int>();
        for (var candidate = 2; primes.Count < count; candidate++)
        {
            if (primes.All(prime => candidate % prime != 0))
            {
                primes.Add(candidate);
            }
        }

        return [.. primes];
    }

    /// <summary>Gives the first 64 bits after the point of the square or cube root of a whole number.</summary>
    private static ulong FractionalBits(int value, int root)
    {
        var scaled = new BigInteger(value) << (64 * root);
        var low = BigInteger.Zero;
        var high = BigInteger.One << 70;
        while (low < high)
        {
            var middle = (low + high + 1) >> 1;
            if (BigInteger.Pow(middle, root) <= scaled)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return (ulong)(low & ulong.MaxValue);
    }
}
