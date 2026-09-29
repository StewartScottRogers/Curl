namespace Curl.Cryptography;

/// <summary>
/// SHA3-256 and SHA3-512 (FIPS 202 section 6.1) on byte strings, hand-built because the
/// BCL's <c>SHA3_256</c> and <c>SHA3_512</c> are not supported on macOS (ADR-0118).
/// ML-KEM's H and G use them; Ed448 and ML-DSA reuse the Keccak sponge beneath them.
/// </summary>
/// <remarks>
/// Constant-time: the running time depends only on the length of the input.
/// </remarks>
public static class Sha3
{
    /// <summary>The length in bytes of a SHA3-256 digest.</summary>
    public const int Sha3_256HashSize = 32;

    /// <summary>The length in bytes of a SHA3-512 digest.</summary>
    public const int Sha3_512HashSize = 64;

    private const byte DomainPadding = 0x06;

    /// <summary>Writes the SHA3-256 digest of <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not 32 bytes.</exception>
    public static void HashData256(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(source, destination, Sha3_256HashSize);

    /// <summary>Writes the SHA3-512 digest of <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not 64 bytes.</exception>
    public static void HashData512(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(source, destination, Sha3_512HashSize);

    private static void HashData(ReadOnlySpan<byte> source, Span<byte> destination, int hashSize)
    {
        if (destination.Length != hashSize)
        {
            throw new ArgumentException($"The destination must be {hashSize} bytes.", nameof(destination));
        }

        using KeccakSponge sponge = new(200 - (2 * hashSize), DomainPadding);
        sponge.Absorb(source);
        sponge.Squeeze(destination);
    }
}
