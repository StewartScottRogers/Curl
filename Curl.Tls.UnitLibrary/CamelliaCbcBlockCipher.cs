using Curl.Cryptography;

namespace Curl.Tls;

/// <summary><see cref="Camellia" /> in CBC mode, for the RFC 5932 suites.</summary>
internal sealed class CamelliaCbcBlockCipher(Camellia camellia) : ITls12CbcBlockCipher
{
    public int BlockSize => Camellia.BlockSize;

    public void Encrypt(ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, Span<byte> destination) =>
        camellia.EncryptCbc(iv, source, destination);

    public void Decrypt(ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, Span<byte> destination) =>
        camellia.DecryptCbc(iv, source, destination);

    public void Dispose() => camellia.Dispose();
}
