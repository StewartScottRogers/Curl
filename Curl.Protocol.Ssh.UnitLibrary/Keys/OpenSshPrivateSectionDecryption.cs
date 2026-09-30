using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Decrypts the private section of an encrypted <c>openssh-key-v1</c> key (OpenSSH's
/// <c>PROTOCOL.key</c>): KDF <c>bcrypt</c>, whose options are a salt and a round count,
/// derives the cipher's key and IV from <c>--pass</c> with the hand-built
/// <see cref="BcryptPbkdf" />, and the cipher is one both OpenSSH writes and libssh2 1.11.1
/// reads - <c>aes128-ctr</c>, <c>aes192-ctr</c>, <c>aes256-ctr</c> (<c>ssh-keygen</c>'s
/// default), the same three in CBC, <c>3des-cbc</c>, and <c>aes128-gcm@openssh.com</c> and
/// <c>aes256-gcm@openssh.com</c>, whose tag follows the section.
/// </summary>
/// <remarks>
/// A wrong passphrase leaves check integers that differ or a GCM tag that fails; the
/// caller's check integers catch the first, and this throws for the second.
/// </remarks>
internal static class OpenSshPrivateSectionDecryption
{
    private const int GcmTagLength = 16;

    private static readonly Dictionary<string, SectionCipher> Ciphers = new(StringComparer.Ordinal)
    {
        ["aes128-ctr"] = new(SectionCipherMode.AesCtr, 16, 16, 16),
        ["aes192-ctr"] = new(SectionCipherMode.AesCtr, 24, 16, 16),
        ["aes256-ctr"] = new(SectionCipherMode.AesCtr, 32, 16, 16),
        ["aes128-cbc"] = new(SectionCipherMode.AesCbc, 16, 16, 16),
        ["aes192-cbc"] = new(SectionCipherMode.AesCbc, 24, 16, 16),
        ["aes256-cbc"] = new(SectionCipherMode.AesCbc, 32, 16, 16),
        ["3des-cbc"] = new(SectionCipherMode.TripleDesCbc, 24, 8, 8),
        ["aes128-gcm@openssh.com"] = new(SectionCipherMode.AesGcm, 16, 12, 16),
        ["aes256-gcm@openssh.com"] = new(SectionCipherMode.AesGcm, 32, 12, 16),
    };

    private enum SectionCipherMode
    {
        AesCtr,
        AesCbc,
        TripleDesCbc,
        AesGcm,
    }

    /// <summary>
    /// Decrypts the private section.
    /// </summary>
    /// <param name="cipherName">The key's cipher name.</param>
    /// <param name="kdfOptions">The KDF options: the salt as a string, then the rounds.</param>
    /// <param name="encryptedSection">The encrypted private section.</param>
    /// <param name="afterSection">What follows the section: for GCM, its tag.</param>
    /// <param name="passphrase">The <c>--pass</c> bytes.</param>
    /// <returns>The decrypted section, or <see langword="null" /> for a cipher this does not read.</returns>
    /// <exception cref="InvalidDataException">The options, rounds, section length or tag are malformed.</exception>
    /// <exception cref="ArgumentException">The passphrase or salt is empty, which bcrypt-pbkdf refuses.</exception>
    /// <exception cref="CryptographicException">The GCM tag does not verify, as a wrong passphrase leaves it.</exception>
    internal static byte[]? Decrypt(string cipherName, ReadOnlyMemory<byte> kdfOptions, ReadOnlyMemory<byte> encryptedSection, SshWireReader afterSection, byte[] passphrase)
    {
        if (!Ciphers.TryGetValue(cipherName, out SectionCipher? cipher))
        {
            return null;
        }

        if (encryptedSection.Length % cipher.BlockLength != 0)
        {
            throw new InvalidDataException("The encrypted openssh-key-v1 section is not a whole number of blocks.");
        }

        SshWireReader options = new(kdfOptions);
        ReadOnlySpan<byte> salt = options.ReadString().Span;
        uint rounds = options.ReadUInt32();
        if (rounds is 0 or > int.MaxValue)
        {
            throw new InvalidDataException("The openssh-key-v1 bcrypt rounds are zero or too many.");
        }

        byte[] keyAndIv = new byte[cipher.KeyLength + cipher.IvLength];
        BcryptPbkdf.DeriveKey(passphrase, salt, (int)rounds, keyAndIv);
        byte[] key = keyAndIv[..cipher.KeyLength];
        byte[] iv = keyAndIv[cipher.KeyLength..];
        try
        {
            return DecryptWith(cipher.Mode, key, iv, encryptedSection.Span, afterSection);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyAndIv);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] DecryptWith(SectionCipherMode mode, byte[] key, byte[] iv, ReadOnlySpan<byte> data, SshWireReader afterSection)
    {
        byte[] plain = new byte[data.Length];
        switch (mode)
        {
            case SectionCipherMode.AesCtr:
                using (AesCtr ctr = new(key, iv))
                {
                    ctr.ApplyKeyStream(data, plain);
                }

                return plain;
            case SectionCipherMode.AesCbc:
                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    return aes.DecryptCbc(data, iv, PaddingMode.None);
                }

            case SectionCipherMode.TripleDesCbc:
                using (TripleDES tripleDes = TripleDES.Create())
                {
                    tripleDes.Key = key;
                    return tripleDes.DecryptCbc(data, iv, PaddingMode.None);
                }

            default:
                using (AesGcm gcm = new(key, GcmTagLength))
                {
                    gcm.Decrypt(iv, data, afterSection.ReadBytes(GcmTagLength).Span, plain);
                }

                return plain;
        }
    }

    private sealed record SectionCipher(SectionCipherMode Mode, int KeyLength, int IvLength, int BlockLength);
}
