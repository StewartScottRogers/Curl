namespace Curl.Cryptography;

/// <summary>
/// The compression function of a hash of the SHA family (FIPS 180-4): blocks read as
/// big-endian words, <c>0x80</c> padding and a big-endian bit length at the end of the last
/// block. <see cref="FixedBlockMerkleDamgard{TCompression}" /> adds the padding and runs it
/// over a fixed number of blocks.
/// </summary>
/// <remarks>
/// The state is held in 64-bit words whatever the hash's word size, so one construction
/// serves SHA-1 and SHA-256 (32-bit words, kept in the low half) and SHA-384 (64-bit words).
/// </remarks>
internal interface IBigEndianCompressionFunction
{
    /// <summary>The length in bytes of a block: 64 or 128.</summary>
    static abstract int BlockSize { get; }

    /// <summary>The length in bytes of the bit-length field that ends the last block: 8 or 16.</summary>
    static abstract int LengthFieldSize { get; }

    /// <summary>The number of words in the chaining state.</summary>
    static abstract int StateWords { get; }

    /// <summary>The length in bytes of the digest.</summary>
    static abstract int HashSize { get; }

    /// <summary>Writes the specification's initial hash value into <paramref name="state" />.</summary>
    static abstract void Initialize(Span<ulong> state);

    /// <summary>Folds one <see cref="BlockSize" />-byte <paramref name="block" /> into <paramref name="state" />.</summary>
    static abstract void Compress(Span<ulong> state, ReadOnlySpan<byte> block);

    /// <summary>Writes the first <see cref="HashSize" /> bytes of <paramref name="state" />, big-endian, to <paramref name="destination" />.</summary>
    static abstract void WriteDigest(ReadOnlySpan<ulong> state, Span<byte> destination);
}
