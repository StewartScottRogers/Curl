using System.Numerics;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// <c>des3-cbc-sha1</c> (RFC 3961 section 6.3, <c>des3-cbc-hmac-sha1-kd</c>): RFC 3961's
/// simplified profile over triple DES in outer-CBC mode and HMAC-SHA-1. The confounder is
/// one block, the confounder and plaintext are padded with zeros to a whole number of
/// blocks, and the full 20-byte HMAC is taken over them before encryption, so decryption
/// gives the plaintext with its padding, as MIT's does. Keys come from
/// <c>random-to-key(168-fold(password | salt))</c> then <c>DK(tkey, "kerberos")</c>; per-usage
/// keys from <c>DK</c>, whose random-to-key spreads 21 random bytes over a 24-byte key with
/// odd parity in the low bit of each byte (RFC 3961 section 6.3.1).
/// </summary>
/// <remarks>
/// Like MIT 1.22's DES3 random-to-key, this one does not correct DES weak keys,
/// which a random key hits with probability about 2^-52 (ADR-0237).
/// </remarks>
internal sealed class Des3CbcSha1KerberosEncryption : KerberosEncryption
{
    /// <summary>The number of <c>hmac-sha1-des3-kd</c> in IANA's checksum type registry.</summary>
    private const int HmacSha1Des3ChecksumType = 12;

    private const int BlockSize = 8;

    private const int KeyByteCount = 24;

    private const int RandomByteCount = 21;

    private const int HmacSize = 20;

    public Des3CbcSha1KerberosEncryption(IKerberosRandomSource randomSource)
        : base(KerberosEncryptionType.Des3CbcSha1, HmacSha1Des3ChecksumType, KeyByteCount, HmacSize, randomSource)
    {
    }

    /// <summary>
    /// RFC 3961 section 6.3.1's DES3 random-to-key: each 7 random bytes become 8, the eighth
    /// byte gathering the low bit of each of the seven, then every byte gets odd parity in
    /// its low bit.
    /// </summary>
    internal static byte[] RandomToKey(ReadOnlySpan<byte> randomBytes)
    {
        byte[] key = new byte[KeyByteCount];
        for (int part = 0; part < 3; part++)
        {
            Span<byte> desKey = key.AsSpan(part * BlockSize, BlockSize);
            randomBytes.Slice(part * 7, 7).CopyTo(desKey);
            for (int index = 0; index < 7; index++)
            {
                desKey[7] |= (byte)((desKey[index] & 1) << (index + 1));
            }

            for (int index = 0; index < BlockSize; index++)
            {
                desKey[index] = (byte)((desKey[index] & 0xFE) | (~BitOperations.PopCount((uint)(desKey[index] & 0xFE)) & 1));
            }
        }

        return key;
    }

    /// <summary>
    /// RFC 3961 section 5.1's <c>DR(key, constant)</c> for triple DES: the n-fold of the
    /// constant to one block, encrypted again and again, the blocks concatenated and cut to
    /// the 21 bytes random-to-key takes.
    /// </summary>
    internal static byte[] DeriveRandom(ReadOnlySpan<byte> key, ReadOnlySpan<byte> constant)
    {
        byte[] blocks = new byte[3 * BlockSize];
        Span<byte> block = stackalloc byte[BlockSize];
        KerberosNFold.Fold(constant, block);
        using TripleDES tripleDes = TripleDES.Create();
        tripleDes.SetKey(key);
        for (int offset = 0; offset < blocks.Length; offset += BlockSize)
        {
            tripleDes.EncryptEcb(block, blocks.AsSpan(offset, BlockSize), PaddingMode.None);
            blocks.AsSpan(offset, BlockSize).CopyTo(block);
        }

        CryptographicOperations.ZeroMemory(block);
        byte[] random = blocks[..RandomByteCount];
        CryptographicOperations.ZeroMemory(blocks);
        return random;
    }

    /// <summary>RFC 3961 section 5.1's <c>DK(key, constant) = random-to-key(DR(key, constant))</c>.</summary>
    internal static byte[] DeriveKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> constant)
    {
        byte[] random = DeriveRandom(key, constant);
        try
        {
            return RandomToKey(random);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(random);
        }
    }

    /// <summary>
    /// RFC 3961 section 6.3.1's DES3string-to-key: parameters must be empty, then
    /// <c>DK(random-to-key(168-fold(password | salt)), "kerberos")</c>.
    /// </summary>
    private protected override byte[] StringToKeyWithPassword(string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> parameters)
    {
        if (!parameters.IsEmpty)
        {
            throw new KerberosCryptographyException(KerberosCryptographyError.BadStringToKeyParameters);
        }

        byte[] passwordAndSalt = [.. System.Text.Encoding.UTF8.GetBytes(password), .. salt];
        byte[] folded = new byte[RandomByteCount];
        KerberosNFold.Fold(passwordAndSalt, folded);
        byte[] temporaryKey = RandomToKey(folded);
        try
        {
            return DeriveKey(temporaryKey, "kerberos"u8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordAndSalt);
            CryptographicOperations.ZeroMemory(folded);
            CryptographicOperations.ZeroMemory(temporaryKey);
        }
    }

    private protected override byte[] EncryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> plaintext)
    {
        byte[] encryptionKey = DeriveKey(key, UsageConstant(usage, 0xAA));
        byte[] integrityKey = DeriveKey(key, UsageConstant(usage, 0x55));
        int paddedLength = (BlockSize + plaintext.Length + BlockSize - 1) / BlockSize * BlockSize;
        byte[] confounded = new byte[paddedLength];
        try
        {
            RandomSource.Fill(confounded.AsSpan(0, BlockSize));
            plaintext.CopyTo(confounded.AsSpan(BlockSize));
            byte[] ciphertext = new byte[paddedLength + HmacSize];
            using TripleDES tripleDes = TripleDES.Create();
            tripleDes.SetKey(encryptionKey);
            tripleDes.EncryptCbc(confounded, new byte[BlockSize], ciphertext.AsSpan(0, paddedLength), PaddingMode.None);
            HMACSHA1.HashData(integrityKey, confounded, ciphertext.AsSpan(paddedLength));
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
        if (ciphertext.Length < BlockSize + HmacSize)
        {
            throw new KerberosCryptographyException(KerberosCryptographyError.CiphertextTooShort);
        }

        ReadOnlySpan<byte> encrypted = ciphertext[..^HmacSize];
        if (encrypted.Length % BlockSize != 0)
        {
            throw new KerberosCryptographyException(KerberosCryptographyError.CiphertextNotWholeBlocks);
        }

        byte[] encryptionKey = DeriveKey(key, UsageConstant(usage, 0xAA));
        byte[] integrityKey = DeriveKey(key, UsageConstant(usage, 0x55));
        byte[] confounded = new byte[encrypted.Length];
        Span<byte> expectedHmac = stackalloc byte[HmacSize];
        try
        {
            using TripleDES tripleDes = TripleDES.Create();
            tripleDes.SetKey(encryptionKey);
            tripleDes.DecryptCbc(encrypted, new byte[BlockSize], confounded, PaddingMode.None);
            HMACSHA1.HashData(integrityKey, confounded, expectedHmac);
            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, ciphertext[^HmacSize..]))
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
            CryptographicOperations.ZeroMemory(expectedHmac);
        }
    }

    /// <summary><c>hmac-sha1-des3-kd</c>: the full HMAC-SHA-1 of the data under <c>DK(key, usage | 0x99)</c>.</summary>
    private protected override byte[] ComputeChecksumWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data)
    {
        byte[] checksumKey = DeriveKey(key, UsageConstant(usage, 0x99));
        try
        {
            return HMACSHA1.HashData(checksumKey, data);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(checksumKey);
        }
    }

    /// <summary>
    /// RFC 3961 section 5.3's simplified-profile PRF, as MIT's <c>krb5int_dk_prf</c> does it:
    /// the SHA-1 of the input cut to two blocks, encrypted in CBC mode under <c>DK(key, "prf")</c>.
    /// </summary>
    private protected override byte[] ComputePseudoRandomWithKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        byte[] pseudoRandomKey = DeriveKey(key, "prf"u8);
        byte[] hash = SHA1.HashData(input);
        try
        {
            using TripleDES tripleDes = TripleDES.Create();
            tripleDes.SetKey(pseudoRandomKey);
            return tripleDes.EncryptCbc(hash.AsSpan(0, 2 * BlockSize), new byte[BlockSize], PaddingMode.None);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pseudoRandomKey);
            CryptographicOperations.ZeroMemory(hash);
        }
    }
}
