using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// <c>camellia128-cts-cmac</c> and <c>camellia256-cts-cmac</c> (RFC 6803): Camellia-CBC-CTS
/// with a CMAC-Camellia taken over the confounder and plaintext, before encryption. Keys
/// come from PBKDF2-HMAC-SHA-1 over the salt prefixed with the type's name, then
/// <c>KDF-FEEDBACK-CMAC(tkey, "kerberos")</c>; per-usage keys from that same KDF, SP 800-108's
/// feedback mode with CMAC as its PRF.
/// </summary>
internal sealed class CamelliaCmacKerberosEncryption : KerberosEncryption
{
    private const int BlockSize = 16;

    private const int DefaultIterationCount = 32768;

    private readonly byte[] encryptionTypeName;

    /// <param name="encryptionType">The encryption type.</param>
    /// <param name="checksumType">Its associated checksum type: 17 <c>cmac-camellia128</c> or 18 <c>cmac-camellia256</c>.</param>
    /// <param name="keySize">The Camellia key size: 16 or 32.</param>
    /// <param name="encryptionTypeName">The name prefixed to the salt, e.g. <c>camellia128-cts-cmac</c>.</param>
    /// <param name="randomSource">Where the confounders come from.</param>
    public CamelliaCmacKerberosEncryption(KerberosEncryptionType encryptionType, int checksumType, int keySize, byte[] encryptionTypeName, IKerberosRandomSource randomSource)
        : base(encryptionType, checksumType, keySize, BlockSize, randomSource)
    {
        this.encryptionTypeName = encryptionTypeName;
    }

    /// <summary>Gives <c>camellia128-cts-cmac</c> for <see cref="KerberosEncryptionType.Camellia128CtsCmac" />, <c>camellia256-cts-cmac</c> otherwise.</summary>
    /// <param name="encryptionType">One of the two Camellia encryption types.</param>
    /// <param name="randomSource">Where the confounders come from.</param>
    public static CamelliaCmacKerberosEncryption ForType(KerberosEncryptionType encryptionType, IKerberosRandomSource randomSource) =>
        encryptionType == KerberosEncryptionType.Camellia128CtsCmac
            ? new(encryptionType, 17, 16, "camellia128-cts-cmac"u8.ToArray(), randomSource)
            : new(encryptionType, 18, 32, "camellia256-cts-cmac"u8.ToArray(), randomSource);

    /// <summary>
    /// RFC 6803 section 3's <c>KDF-FEEDBACK-CMAC(key, constant)</c>: <c>K(i) = CMAC(key,
    /// K(i-1) | i | constant | 0x00 | k)</c> from an all-zero <c>K(0)</c>, the blocks
    /// concatenated and cut to the key size.
    /// </summary>
    internal byte[] DeriveKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> constant)
    {
        byte[] derived = new byte[KeySize];
        byte[] message = new byte[BlockSize + sizeof(int) + constant.Length + 1 + sizeof(int)];
        constant.CopyTo(message.AsSpan(BlockSize + sizeof(int)));
        BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(message.Length - sizeof(int)), KeySize * 8);
        try
        {
            for (int offset = 0; offset < KeySize; offset += BlockSize)
            {
                BinaryPrimitives.WriteInt32BigEndian(message.AsSpan(BlockSize), (offset / BlockSize) + 1);
                byte[] block = KerberosCamelliaCmac.Compute(key, message);
                block.CopyTo(message, 0);
                block.CopyTo(derived, offset);
                CryptographicOperations.ZeroMemory(block);
            }

            return derived;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
        }
    }

    private protected override byte[] StringToKeyWithPassword(string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> parameters)
    {
        int iterationCount = ReadIterationCount(parameters, DefaultIterationCount);
        byte[] prefixedSalt = [.. encryptionTypeName, 0, .. salt];
        byte[] temporaryKey = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), prefixedSalt, iterationCount, HashAlgorithmName.SHA1, KeySize);
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
        byte[] confounded = new byte[BlockSize + plaintext.Length];
        try
        {
            RandomSource.Fill(confounded.AsSpan(0, BlockSize));
            plaintext.CopyTo(confounded.AsSpan(BlockSize));
            byte[] ciphertext = new byte[confounded.Length + ChecksumSize];
            KerberosCamelliaCts.Encrypt(encryptionKey, confounded, ciphertext.AsSpan(0, confounded.Length));
            byte[] mac = KerberosCamelliaCmac.Compute(integrityKey, confounded);
            mac.CopyTo(ciphertext, confounded.Length);
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
        byte[] encryptionKey = DeriveKey(key, UsageConstant(usage, 0xAA));
        byte[] integrityKey = DeriveKey(key, UsageConstant(usage, 0x55));
        byte[] confounded = new byte[encrypted.Length];
        try
        {
            KerberosCamelliaCts.Decrypt(encryptionKey, encrypted, confounded);
            byte[] expectedMac = KerberosCamelliaCmac.Compute(integrityKey, confounded);
            if (!CryptographicOperations.FixedTimeEquals(expectedMac, ciphertext[^ChecksumSize..]))
            {
                throw new KerberosCryptographyException(KerberosCryptographyError.IntegrityCheckFailed);
            }

            return confounded[BlockSize..];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(integrityKey);
            CryptographicOperations.ZeroMemory(confounded);
        }
    }

    /// <summary>RFC 6803 section 7's <c>get_mic</c>: <c>CMAC(Kc, message)</c>.</summary>
    private protected override byte[] ComputeChecksumWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data)
    {
        byte[] checksumKey = DeriveKey(key, UsageConstant(usage, 0x99));
        try
        {
            return KerberosCamelliaCmac.Compute(checksumKey, data);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(checksumKey);
        }
    }

    /// <summary>RFC 6803 section 6's PRF: <c>CMAC(KDF-FEEDBACK-CMAC(key, "prf"), input)</c>.</summary>
    private protected override byte[] ComputePseudoRandomWithKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        byte[] pseudoRandomKey = DeriveKey(key, "prf"u8);
        try
        {
            return KerberosCamelliaCmac.Compute(pseudoRandomKey, input);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pseudoRandomKey);
        }
    }
}
