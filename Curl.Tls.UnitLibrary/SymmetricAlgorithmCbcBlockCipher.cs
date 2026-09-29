using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>A BCL <see cref="SymmetricAlgorithm" /> (<see cref="Aes" /> or <see cref="TripleDES" />) in CBC mode without padding.</summary>
internal sealed class SymmetricAlgorithmCbcBlockCipher(SymmetricAlgorithm algorithm) : ITls12CbcBlockCipher
{
    public int BlockSize => algorithm.BlockSize / 8;

    public void Encrypt(ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, Span<byte> destination) =>
        algorithm.EncryptCbc(source, iv, destination, PaddingMode.None);

    public void Decrypt(ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, Span<byte> destination) =>
        algorithm.DecryptCbc(source, iv, destination, PaddingMode.None);

    public void Dispose() => algorithm.Dispose();
}
