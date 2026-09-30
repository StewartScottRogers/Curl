using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

/// <summary>
/// A block cipher in cipher block chaining mode (RFC 4253 section 6.3): the chain starts at
/// the derived IV and runs on across packets, each call's last ciphertext block chaining
/// into the next call. <c>aes256-cbc</c> (and <c>rijndael-cbc@lysator.liu.se</c>),
/// <c>aes192-cbc</c> and <c>aes128-cbc</c> use the BCL's <see cref="Aes" />,
/// <c>3des-cbc</c> the BCL's <see cref="TripleDES" />, and <c>blowfish-cbc</c> and
/// <c>cast128-cbc</c> <see cref="Blowfish" /> and <see cref="Cast128" /> from
/// <c>Curl.Cryptography</c> (ADR-0118).
/// </summary>
internal sealed class CbcSshCipher : ISshCipher
{
    private readonly CbcTransform encrypt;

    private readonly CbcTransform decrypt;

    private readonly IDisposable blockCipher;

    private readonly byte[] chain;

    private CbcSshCipher(byte[] initializationVector, CbcTransform encrypt, CbcTransform decrypt, IDisposable blockCipher)
    {
        chain = [.. initializationVector];
        this.encrypt = encrypt;
        this.decrypt = decrypt;
        this.blockCipher = blockCipher;
    }

    /// <summary>
    /// One call of the block cipher's chaining mode: <paramref name="source" />, a whole
    /// number of blocks, from <paramref name="initializationVector" /> into
    /// <paramref name="destination" />, which may be <paramref name="source" /> itself.
    /// </summary>
    /// <param name="initializationVector">The block the chain starts from.</param>
    /// <param name="source">The bytes to transform.</param>
    /// <param name="destination">Where the result goes.</param>
    private delegate void CbcTransform(ReadOnlySpan<byte> initializationVector, ReadOnlySpan<byte> source, Span<byte> destination);

    /// <inheritdoc />
    public int BlockSize => chain.Length;

    /// <summary>Creates AES in CBC mode.</summary>
    /// <param name="key">The derived encryption key: 16, 24 or 32 bytes.</param>
    /// <param name="initializationVector">The derived IV: 16 bytes.</param>
    /// <returns>The cipher.</returns>
    internal static CbcSshCipher ForAes(byte[] key, byte[] initializationVector) =>
        ForSymmetricAlgorithm(Aes.Create(), key, initializationVector);

    /// <summary>Creates three-key triple DES (EDE) in CBC mode.</summary>
    /// <param name="key">The derived encryption key: 24 bytes.</param>
    /// <param name="initializationVector">The derived IV: 8 bytes.</param>
    /// <returns>The cipher.</returns>
    internal static CbcSshCipher ForTripleDes(byte[] key, byte[] initializationVector) =>
        ForSymmetricAlgorithm(TripleDES.Create(), key, initializationVector);

    /// <summary>Creates Blowfish in CBC mode.</summary>
    /// <param name="key">The derived encryption key: 16 bytes.</param>
    /// <param name="initializationVector">The derived IV: 8 bytes.</param>
    /// <returns>The cipher.</returns>
    internal static CbcSshCipher ForBlowfish(byte[] key, byte[] initializationVector)
    {
        Blowfish blowfish = new(key);
        return new(initializationVector, blowfish.EncryptCbc, blowfish.DecryptCbc, blowfish);
    }

    /// <summary>Creates CAST-128 in CBC mode.</summary>
    /// <param name="key">The derived encryption key: 16 bytes.</param>
    /// <param name="initializationVector">The derived IV: 8 bytes.</param>
    /// <returns>The cipher.</returns>
    internal static CbcSshCipher ForCast128(byte[] key, byte[] initializationVector)
    {
        Cast128 cast128 = new(key);
        return new(initializationVector, cast128.EncryptCbc, cast128.DecryptCbc, cast128);
    }

    /// <inheritdoc />
    public void Encrypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.IsEmpty)
        {
            return;
        }

        encrypt(chain, source, destination);
        destination[^chain.Length..].CopyTo(chain);
    }

    /// <inheritdoc />
    public void Decrypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.IsEmpty)
        {
            return;
        }

        byte[] nextChain = source[^chain.Length..].ToArray();
        decrypt(chain, source, destination);
        nextChain.CopyTo(chain, 0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(chain);
        blockCipher.Dispose();
    }

    private static CbcSshCipher ForSymmetricAlgorithm(SymmetricAlgorithm algorithm, byte[] key, byte[] initializationVector)
    {
        algorithm.Key = key;
        return new(
            initializationVector,
            (iv, source, destination) => algorithm.EncryptCbc(source, iv, destination, PaddingMode.None),
            (iv, source, destination) => algorithm.DecryptCbc(source, iv, destination, PaddingMode.None),
            algorithm);
    }
}
