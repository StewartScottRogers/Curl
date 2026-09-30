using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// The CBC decryptions an encrypted key file names, each with PKCS #7 padding: AES and
/// three-key triple DES from the BCL, and single DES from the hand-built <see cref="Des" />,
/// which works where the platform's library no longer offers it.
/// </summary>
internal static class KeyFileCbcDecryption
{
    /// <summary>
    /// Gets the key length in bytes of <paramref name="cipher" />.
    /// </summary>
    /// <param name="cipher">The cipher.</param>
    /// <returns>The key length.</returns>
    internal static int KeyLength(KeyFileCipher cipher) => cipher switch
    {
        KeyFileCipher.Aes128 => 16,
        KeyFileCipher.Aes192 => 24,
        KeyFileCipher.Aes256 => 32,
        KeyFileCipher.TripleDes => 24,
        _ => Des.KeySize,
    };

    /// <summary>
    /// Decrypts <paramref name="data" /> and removes its padding.
    /// </summary>
    /// <param name="cipher">The cipher.</param>
    /// <param name="key">The key, <see cref="KeyLength" /> bytes.</param>
    /// <param name="iv">The initialization vector, one block.</param>
    /// <param name="data">The ciphertext.</param>
    /// <returns>The plaintext.</returns>
    /// <exception cref="CryptographicException">The ciphertext, IV or padding is invalid, as a wrong passphrase leaves it.</exception>
    internal static byte[] Decrypt(KeyFileCipher cipher, byte[] key, byte[] iv, byte[] data)
    {
        switch (cipher)
        {
            case KeyFileCipher.Des:
                return DecryptDes(key, iv, data);
            case KeyFileCipher.TripleDes:
                using (TripleDES tripleDes = TripleDES.Create())
                {
                    tripleDes.Key = key;
                    return tripleDes.DecryptCbc(data, iv);
                }

            default:
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    return aes.DecryptCbc(data, iv);
                }
        }
    }

    private static byte[] DecryptDes(byte[] key, byte[] iv, byte[] data)
    {
        if (iv.Length != Des.BlockSize || data.Length == 0 || data.Length % Des.BlockSize != 0)
        {
            throw new CryptographicException("The DES-CBC ciphertext or IV has the wrong length.");
        }

        using Des des = new(key);
        byte[] plain = new byte[data.Length];
        byte[] previous = iv;
        for (int offset = 0; offset < data.Length; offset += Des.BlockSize)
        {
            byte[] block = data[offset..(offset + Des.BlockSize)];
            Span<byte> plainBlock = plain.AsSpan(offset, Des.BlockSize);
            des.DecryptBlock(block, plainBlock);
            XorInPlace(plainBlock, previous);
            previous = block;
        }

        return RemovePadding(plain);
    }

    private static void XorInPlace(Span<byte> destination, byte[] other)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] ^= other[index];
        }
    }

    // PKCS #7: the last byte n, from 1 to a block, and n bytes of n.
    private static byte[] RemovePadding(byte[] plain)
    {
        int padding = plain[^1];
        if (padding is < 1 or > Des.BlockSize || plain.AsSpan(plain.Length - padding).ContainsAnyExcept((byte)padding))
        {
            throw new CryptographicException("The DES-CBC padding is invalid.");
        }

        return plain[..^padding];
    }
}
