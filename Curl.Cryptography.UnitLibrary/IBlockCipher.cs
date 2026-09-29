namespace Curl.Cryptography;

/// <summary>
/// The forward direction of a block cipher with 16-byte blocks, the one operation
/// <see cref="GaloisCounterMode" /> needs from it.
/// </summary>
internal interface IBlockCipher
{
    /// <summary>Encrypts the one 16-byte block <paramref name="source" /> into <paramref name="destination" />.</summary>
    void EncryptBlock(ReadOnlySpan<byte> source, Span<byte> destination);
}
