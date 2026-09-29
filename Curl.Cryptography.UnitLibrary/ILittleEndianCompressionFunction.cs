namespace Curl.Cryptography;

/// <summary>
/// The compression function of a hash built as MD4 is: 64-byte blocks read as sixteen
/// little-endian words, a state of 32-bit words written out little-endian.
/// <see cref="LittleEndianMerkleDamgard{TCompression}" /> adds the buffering and padding.
/// </summary>
internal interface ILittleEndianCompressionFunction
{
    /// <summary>The number of 32-bit words in the chaining state, and so in the digest.</summary>
    static abstract int StateWords { get; }

    /// <summary>Writes the specification's initial chaining values into <paramref name="state" />.</summary>
    static abstract void Initialize(Span<uint> state);

    /// <summary>Folds one 64-byte <paramref name="block" /> into <paramref name="state" />.</summary>
    static abstract void Compress(Span<uint> state, ReadOnlySpan<byte> block);
}
