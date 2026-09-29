namespace Curl.Tls;

/// <summary>A block cipher in CBC mode without padding, as TLS CBC records use it.</summary>
internal interface ITls12CbcBlockCipher : IDisposable
{
    /// <summary>Gets the block length in bytes.</summary>
    int BlockSize { get; }

    /// <summary>Encrypts whole blocks <paramref name="source" /> into <paramref name="destination" /> from <paramref name="iv" />.</summary>
    void Encrypt(ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, Span<byte> destination);

    /// <summary>Decrypts whole blocks <paramref name="source" /> into <paramref name="destination" /> from <paramref name="iv" />.</summary>
    void Decrypt(ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, Span<byte> destination);
}
