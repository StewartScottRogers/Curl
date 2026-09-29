namespace Curl.Cryptography;

/// <summary>
/// SHA3-224, SHA3-256, SHA3-384 and SHA3-512 (FIPS 202 section 6.1) on byte strings,
/// hand-built because the BCL's <c>SHA3_256</c>, <c>SHA3_384</c> and <c>SHA3_512</c> are not
/// supported on macOS (ADR-0118) and it has no SHA3-224 at all. ML-KEM's H and G use SHA3-256
/// and SHA3-512, HTTPS <c>tls-server-end-point</c> bindings all four (BL-980); Ed448 and
/// ML-DSA reuse the Keccak sponge beneath them.
/// </summary>
/// <remarks>
/// Constant-time: the running time depends only on the length of the input.
/// </remarks>
public static class Sha3
{
    /// <summary>The length in bytes of a SHA3-224 digest.</summary>
    public const int Sha3_224HashSize = 28;

    /// <summary>The length in bytes of a SHA3-256 digest.</summary>
    public const int Sha3_256HashSize = 32;

    /// <summary>The length in bytes of a SHA3-384 digest.</summary>
    public const int Sha3_384HashSize = 48;

    /// <summary>The length in bytes of a SHA3-512 digest.</summary>
    public const int Sha3_512HashSize = 64;

    private const byte DomainPadding = 0x06;

    /// <summary>Writes the SHA3-224 digest of <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not 28 bytes.</exception>
    public static void HashData224(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(source, destination, Sha3_224HashSize);

    /// <summary>Writes the SHA3-256 digest of <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not 32 bytes.</exception>
    public static void HashData256(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(source, destination, Sha3_256HashSize);

    /// <summary>Writes the SHA3-384 digest of <paramref name="source" /> to <paramref name="destination" />.</summary>
    /// <exception cref="ArgumentException"><paramref name="destination" /> is not 48 bytes.</exception>
    public static void HashData384(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(source, destination, Sha3_384HashSize);

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
