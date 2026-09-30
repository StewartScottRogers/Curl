using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// OpenSSL's legacy PEM encryption of a traditional key file (<c>RSA PRIVATE KEY</c>,
/// <c>DSA PRIVATE KEY</c>, <c>EC PRIVATE KEY</c>): a <c>DEK-Info: &lt;cipher&gt;,&lt;iv hex&gt;</c>
/// header, and the key derived from the passphrase and the IV's first eight bytes by
/// <c>EVP_BytesToKey</c> with MD5 and one iteration. <c>ssh-keygen -m PEM</c> writes
/// <c>AES-128-CBC</c>.
/// </summary>
internal static class LegacyPemDecryption
{
    private const int SaltLength = 8;

    /// <summary>
    /// Decrypts the block's body when a <c>DEK-Info</c> header says it is encrypted.
    /// </summary>
    /// <param name="block">The PEM block.</param>
    /// <param name="passphrase">The <c>--pass</c> bytes; empty when it was not given.</param>
    /// <returns>The body, decrypted when it was encrypted.</returns>
    /// <exception cref="CryptographicException">The cipher is unknown, or the passphrase is wrong.</exception>
    /// <exception cref="FormatException">The IV is not hexadecimal.</exception>
    internal static byte[] Decrypt(PemBlock block, byte[] passphrase)
    {
        if (!block.Headers.TryGetValue("DEK-Info", out string? info))
        {
            return block.Body;
        }

        int comma = info.IndexOf(',', StringComparison.Ordinal);
        KeyFileCipher cipher = comma < 0 ? throw new CryptographicException("DEK-Info names no IV.") : Cipher(info[..comma]);
        byte[] iv = Convert.FromHexString(info[(comma + 1)..]);
        if (iv.Length < SaltLength)
        {
            throw new CryptographicException("The DEK-Info IV is shorter than its salt.");
        }

        byte[] key = BytesToKey(passphrase, iv[..SaltLength], KeyFileCbcDecryption.KeyLength(cipher));
        return KeyFileCbcDecryption.Decrypt(cipher, key, iv, block.Body);
    }

    private static KeyFileCipher Cipher(string name) => name switch
    {
        "AES-128-CBC" => KeyFileCipher.Aes128,
        "AES-192-CBC" => KeyFileCipher.Aes192,
        "AES-256-CBC" => KeyFileCipher.Aes256,
        "DES-EDE3-CBC" => KeyFileCipher.TripleDes,
        "DES-CBC" => KeyFileCipher.Des,
        _ => throw new CryptographicException("The DEK-Info cipher is not one OpenSSL writes for a key."),
    };

    // EVP_BytesToKey(MD5, count 1): D_1 = MD5(passphrase || salt), D_i = MD5(D_i-1 || passphrase || salt).
    private static byte[] BytesToKey(byte[] passphrase, byte[] salt, int length)
    {
        byte[] key = [];
        byte[] previous = [];
        while (key.Length < length)
        {
            previous = MD5.HashData([.. previous, .. passphrase, .. salt]);
            key = [.. key, .. previous];
        }

        return key[..length];
    }
}
