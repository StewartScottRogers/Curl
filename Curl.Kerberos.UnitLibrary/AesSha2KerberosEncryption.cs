using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// <c>aes128-cts-hmac-sha256-128</c> and <c>aes256-cts-hmac-sha384-192</c> (RFC 8009):
/// AES-CBC-CTS with an HMAC-SHA-2 taken over the cipher state and ciphertext, so integrity
/// is checked before decrypting. Keys come from PBKDF2-HMAC-SHA-2 over the salt prefixed
/// with the type's name, and per-usage keys from SP 800-108's counter-mode KDF.
/// </summary>
internal sealed class AesSha2KerberosEncryption : KerberosEncryption
{
    private const int BlockSize = 16;

    private const int DefaultIterationCount = 32768;

    private readonly HashAlgorithmName hashAlgorithm;

    private readonly byte[] encryptionTypeName;

    /// <param name="encryptionType">The encryption type.</param>
    /// <param name="checksumType">Its associated checksum type.</param>
    /// <param name="keySize">The AES key size: 16 or 32.</param>
    /// <param name="hashAlgorithm">SHA-256 or SHA-384.</param>
    /// <param name="hmacSize">The truncated HMAC size: 16 or 24.</param>
    /// <param name="encryptionTypeName">The name prefixed to the salt, e.g. <c>aes128-cts-hmac-sha256-128</c>.</param>
    /// <param name="randomSource">Where the confounders come from.</param>
    public AesSha2KerberosEncryption(KerberosEncryptionType encryptionType, int checksumType, int keySize, HashAlgorithmName hashAlgorithm, int hmacSize, byte[] encryptionTypeName, IKerberosRandomSource randomSource)
        : base(encryptionType, checksumType, keySize, hmacSize, randomSource)
    {
        this.hashAlgorithm = hashAlgorithm;
        this.encryptionTypeName = encryptionTypeName;
    }

    /// <summary>
    /// RFC 8009 section 3's <c>KDF-HMAC-SHA2(key, label, [context,] k)</c>: the HMAC of
    /// <c>0x00000001 | label | 0x00 | context | k</c>, cut to <paramref name="bits" />.
    /// </summary>
    internal byte[] DeriveKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> label, ReadOnlySpan<byte> context, int bits)
    {
        byte[] message = new byte[sizeof(int) + label.Length + 1 + context.Length + sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(message, 1);
        label.CopyTo(message.AsSpan(sizeof(int)));
        context.CopyTo(message.AsSpan(sizeof(int) + label.Length + 1));
        BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(message.Length - sizeof(int)), bits);
        byte[] hmac = CryptographicOperations.HmacData(hashAlgorithm, key, message);
        try
        {
            return hmac[..(bits / 8)];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hmac);
        }
    }

    private protected override byte[] StringToKeyWithPassword(string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> parameters)
    {
        int iterationCount = ReadIterationCount(parameters, DefaultIterationCount);
        byte[] prefixedSalt = [.. encryptionTypeName, 0, .. salt];
        byte[] temporaryKey = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), prefixedSalt, iterationCount, hashAlgorithm, KeySize);
        try
        {
            return DeriveKey(temporaryKey, "kerberos"u8, [], KeySize * 8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(temporaryKey);
        }
    }

    private protected override byte[] EncryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> plaintext)
    {
        byte[] encryptionKey = DeriveKey(key, UsageConstant(usage, 0xAA), [], KeySize * 8);
        byte[] integrityKey = DeriveKey(key, UsageConstant(usage, 0x55), [], ChecksumSize * 8);
        byte[] confounded = new byte[BlockSize + plaintext.Length];
        try
        {
            RandomSource.Fill(confounded.AsSpan(0, BlockSize));
            plaintext.CopyTo(confounded.AsSpan(BlockSize));
            byte[] ciphertext = new byte[confounded.Length + ChecksumSize];
            KerberosAesCts.Encrypt(encryptionKey, confounded, ciphertext.AsSpan(0, confounded.Length));
            WriteIntegrityHmac(integrityKey, ciphertext.AsSpan(0, confounded.Length), ciphertext.AsSpan(confounded.Length));
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
        if (ciphertext.Length < BlockSize + ChecksumSize)
        {
            throw new KerberosCryptographyException(KerberosCryptographyError.CiphertextTooShort);
        }

        ReadOnlySpan<byte> encrypted = ciphertext[..^ChecksumSize];
        byte[] integrityKey = DeriveKey(key, UsageConstant(usage, 0x55), [], ChecksumSize * 8);
        byte[] expectedHmac = new byte[ChecksumSize];
        try
        {
            WriteIntegrityHmac(integrityKey, encrypted, expectedHmac);
            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, ciphertext[^ChecksumSize..]))
            {
                throw new KerberosCryptographyException(KerberosCryptographyError.IntegrityCheckFailed);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(integrityKey);
        }

        return DecryptCheckedCiphertext(key, usage, encrypted);
    }

    private protected override byte[] ComputeChecksumWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data)
    {
        byte[] checksumKey = DeriveKey(key, UsageConstant(usage, 0x99), [], ChecksumSize * 8);
        byte[] hmac = CryptographicOperations.HmacData(hashAlgorithm, checksumKey, data);
        try
        {
            return hmac[..ChecksumSize];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(checksumKey);
            CryptographicOperations.ZeroMemory(hmac);
        }
    }

    /// <summary>RFC 8009 section 5's PRF: <c>KDF-HMAC-SHA2(key, "prf", input, 256 or 384)</c>, the whole hash.</summary>
    private protected override byte[] ComputePseudoRandomWithKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input) =>
        DeriveKey(key, "prf"u8, input, HashSizeInBits());

    private int HashSizeInBits() => hashAlgorithm == HashAlgorithmName.SHA256 ? 256 : 384;

    private byte[] DecryptCheckedCiphertext(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> encrypted)
    {
        byte[] encryptionKey = DeriveKey(key, UsageConstant(usage, 0xAA), [], KeySize * 8);
        byte[] confounded = new byte[encrypted.Length];
        try
        {
            KerberosAesCts.Decrypt(encryptionKey, encrypted, confounded);
            return confounded[BlockSize..];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(confounded);
        }
    }

    /// <summary>Writes <c>HMAC(Ki, IV | C)</c>, cut to the checksum size, the IV being the initial all-zero cipher state.</summary>
    private void WriteIntegrityHmac(ReadOnlySpan<byte> integrityKey, ReadOnlySpan<byte> encrypted, Span<byte> destination)
    {
        byte[] message = new byte[BlockSize + encrypted.Length];
        encrypted.CopyTo(message.AsSpan(BlockSize));
        byte[] hmac = CryptographicOperations.HmacData(hashAlgorithm, integrityKey, message);
        hmac.AsSpan(0, ChecksumSize).CopyTo(destination);
        CryptographicOperations.ZeroMemory(hmac);
    }
}
