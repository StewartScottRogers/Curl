using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// The SHA-224 compression function (FIPS 180-4 sections 5.3.2 and 6.3): SHA-256's
/// compression from SHA-224's initial hash value, with the digest cut to its first seven
/// words, 28 bytes. Exists because the BCL has no SHA-224 on any platform, and
/// <see cref="DeterministicDsaNonce" /> needs HMAC-SHA-224 for RFC 6979's nonce of a DSA
/// signature over a SHA-224 hash. Constant-time, as <see cref="Sha256" /> is.
/// </summary>
internal readonly struct Sha224 : IBigEndianCompressionFunction
{
    /// <inheritdoc />
    public static int BlockSize => Sha256.BlockSize;

    /// <inheritdoc />
    public static int LengthFieldSize => Sha256.LengthFieldSize;

    /// <inheritdoc />
    public static int StateWords => Sha256.StateWords;

    /// <inheritdoc />
    public static int HashSize => 28;

    /// <inheritdoc />
    public static void Initialize(Span<ulong> state)
    {
        state[0] = 0xc1059ed8;
        state[1] = 0x367cd507;
        state[2] = 0x3070dd17;
        state[3] = 0xf70e5939;
        state[4] = 0xffc00b31;
        state[5] = 0x68581511;
        state[6] = 0x64f98fa7;
        state[7] = 0xbefa4fa4;
    }

    /// <inheritdoc />
    public static void Compress(Span<ulong> state, ReadOnlySpan<byte> block) => Sha256.Compress(state, block);

    /// <inheritdoc />
    public static void WriteDigest(ReadOnlySpan<ulong> state, Span<byte> destination)
    {
        Span<byte> full = stackalloc byte[Sha256.HashSize];
        Sha256.WriteDigest(state, full);
        full[..HashSize].CopyTo(destination);
        CryptographicOperations.ZeroMemory(full);
    }
}
