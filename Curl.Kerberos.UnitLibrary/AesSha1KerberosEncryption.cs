using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// <c>aes128-cts-hmac-sha1-96</c> and <c>aes256-cts-hmac-sha1-96</c> (RFC 3962): RFC 3961's
/// simplified profile over AES-CBC-CTS and HMAC-SHA-1 truncated to 96 bits. Keys come from
/// PBKDF2-HMAC-SHA-1 then <c>DK(tkey, "kerberos")</c>; per-usage keys from <c>DK</c>; the
/// checksum is taken over the confounder and plaintext, before encryption.
/// </summary>
internal sealed class AesSha1KerberosEncryption : KerberosEncryption
{
    private const int ConfounderSize = 16;

    private const int HmacSize = 12;

    private const int DefaultIterationCount = 4096;

    public AesSha1KerberosEncryption(KerberosEncryptionType encryptionType, int checksumType, int keySize, IKerberosRandomSource randomSource)
        : base(encryptionType, checksumType, keySize, HmacSize, randomSource)
    {
    }

    /// <summary>
    /// RFC 3961 section 5.1's <c>DK(key, constant)</c> for AES, whose random-to-key is the
    /// identity: the n-fold of the constant to one block, encrypted again and again, the
    /// blocks concatenated to the key size.
    /// </summary>
    internal byte[] DeriveKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> constant)
    {
        byte[] derived = new byte[KeySize];
        Span<byte> block = stackalloc byte[16];
        KerberosNFold.Fold(constant, block);
        using Aes aes = Aes.Create();
        aes.SetKey(key);
        for (int offset = 0; offset < KeySize; offset += block.Length)
        {
            aes.EncryptEcb(block, derived.AsSpan(offset, block.Length), PaddingMode.None);
            derived.AsSpan(offset, block.Length).CopyTo(block);
        }

        CryptographicOperations.ZeroMemory(block);
        return derived;
    }

    private protected override byte[] StringToKeyWithPassword(string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> parameters)
    {
        int iterationCount = ReadIterationCount(parameters, DefaultIterationCount);
        byte[] temporaryKey = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), salt, iterationCount, HashAlgorithmName.SHA1, KeySize);
        try
        {
            return DeriveKey(temporaryKey, "kerberos"u8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(temporaryKey);
        }
    }

    private protected override byte[] EncryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> plaintext)
    {
        byte[] encryptionKey = DeriveKey(key, UsageConstant(usage, 0xAA));
        byte[] integrityKey = DeriveKey(key, UsageConstant(usage, 0x55));
        byte[] confounded = new byte[ConfounderSize + plaintext.Length];
        try
        {
            RandomSource.Fill(confounded.AsSpan(0, ConfounderSize));
            plaintext.CopyTo(confounded.AsSpan(ConfounderSize));
            byte[] ciphertext = new byte[confounded.Length + HmacSize];
            KerberosAesCts.Encrypt(encryptionKey, confounded, ciphertext.AsSpan(0, confounded.Length));
            WriteHmac(integrityKey, confounded, ciphertext.AsSpan(confounded.Length));
            return ciphertext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(integrityKey);
            CryptographicOperations.ZeroMemory(confounded);
        }
    }

    private protected override byte[] DecryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length < ConfounderSize + HmacSize)
        {
            throw new KerberosCryptographyException(KerberosCryptographyError.CiphertextTooShort);
        }

        ReadOnlySpan<byte> encrypted = ciphertext[..^HmacSize];
        byte[] encryptionKey = DeriveKey(key, UsageConstant(usage, 0xAA));
        byte[] integrityKey = DeriveKey(key, UsageConstant(usage, 0x55));
        byte[] confounded = new byte[encrypted.Length];
        Span<byte> expectedHmac = stackalloc byte[HmacSize];
        try
        {
            KerberosAesCts.Decrypt(encryptionKey, encrypted, confounded);
            WriteHmac(integrityKey, confounded, expectedHmac);
            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, ciphertext[^HmacSize..]))
            {
                throw new KerberosCryptographyException(KerberosCryptographyError.IntegrityCheckFailed);
            }

            return confounded[ConfounderSize..];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(integrityKey);
            CryptographicOperations.ZeroMemory(confounded);
        }
    }

    private protected override byte[] ComputeChecksumWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data)
    {
        byte[] checksumKey = DeriveKey(key, UsageConstant(usage, 0x99));
        try
        {
            byte[] checksum = new byte[HmacSize];
            WriteHmac(checksumKey, data, checksum);
            return checksum;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(checksumKey);
        }
    }

    /// <summary>
    /// RFC 3961 section 5.3's simplified-profile PRF: the SHA-1 of the input cut to one
    /// block, encrypted under <c>DK(key, "prf")</c>.
    /// </summary>
    private protected override byte[] ComputePseudoRandomWithKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        byte[] pseudoRandomKey = DeriveKey(key, "prf"u8);
        byte[] hash = SHA1.HashData(input);
        try
        {
            byte[] output = new byte[16];
            KerberosAesCts.Encrypt(pseudoRandomKey, hash.AsSpan(0, output.Length), output);
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pseudoRandomKey);
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    /// <summary>Writes HMAC-SHA-1 of <paramref name="data" />, cut to <see cref="HmacSize" /> bytes, to <paramref name="destination" />.</summary>
    private static void WriteHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> destination)
    {
        Span<byte> hmac = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(key, data, hmac);
        hmac[..HmacSize].CopyTo(destination);
        CryptographicOperations.ZeroMemory(hmac);
    }
}
