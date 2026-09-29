using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// <c>rc4-hmac</c> (RFC 4757), Windows' NT-hash encryption type: the key is the MD4 of the
/// UTF-16LE password, a message is an HMAC-MD5 checksum followed by the RC4 encryption of
/// an 8-byte confounder and the plaintext, and the checksum type is <c>hmac-md5</c> (-138).
/// </summary>
/// <remarks>
/// RFC 4757 keys each message with a "message type" T; Kerberos key usages map to it as
/// MIT Kerberos' <c>krb5int_arcfour_translate_usage</c> maps them: 3 and 9 become 8, 23
/// becomes 13, and every other usage is its own T.
/// </remarks>
internal sealed class Rc4HmacKerberosEncryption : KerberosEncryption
{
    private const int KeyLength = 16;

    private const int ConfounderSize = 8;

    private const int HmacMd5ChecksumType = -138;

    public Rc4HmacKerberosEncryption(IKerberosRandomSource randomSource)
        : base(KerberosEncryptionType.Rc4Hmac, HmacMd5ChecksumType, KeyLength, HMACMD5.HashSizeInBytes, randomSource)
    {
    }

    /// <summary>The RFC 4757 message type T for a Kerberos key usage.</summary>
    internal static int MessageType(int usage) => usage switch
    {
        3 or 9 => 8,
        23 => 13,
        _ => usage,
    };

    /// <summary>RFC 4757 section 2: <c>MD4(UNICODE(password))</c>; the salt and parameters play no part.</summary>
    private protected override byte[] StringToKeyWithPassword(string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> parameters)
    {
        byte[] unicodePassword = Encoding.Unicode.GetBytes(password);
        byte[] key = new byte[Md4.HashSize];
        Md4.HashData(unicodePassword, key);
        CryptographicOperations.ZeroMemory(unicodePassword);
        return key;
    }

    private protected override byte[] EncryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> plaintext)
    {
        byte[] usageKey = UsageKey(key, usage);
        byte[] ciphertext = new byte[ChecksumSize + ConfounderSize + plaintext.Length];
        Span<byte> body = ciphertext.AsSpan(ChecksumSize);
        try
        {
            RandomSource.Fill(body[..ConfounderSize]);
            plaintext.CopyTo(body[ConfounderSize..]);
            HMACMD5.HashData(usageKey, body, ciphertext.AsSpan(0, ChecksumSize));
            ApplyKeyStream(usageKey, ciphertext.AsSpan(0, ChecksumSize), body, body);
            return ciphertext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(usageKey);
        }
    }

    private protected override byte[] DecryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> ciphertext)
    {
        if (ciphertext.Length < ChecksumSize + ConfounderSize)
        {
            throw new KerberosCryptographyException(KerberosCryptographyError.CiphertextTooShort);
        }

        ReadOnlySpan<byte> checksum = ciphertext[..ChecksumSize];
        byte[] usageKey = UsageKey(key, usage);
        byte[] body = new byte[ciphertext.Length - ChecksumSize];
        Span<byte> expectedChecksum = stackalloc byte[ChecksumSize];
        try
        {
            ApplyKeyStream(usageKey, checksum, ciphertext[ChecksumSize..], body);
            HMACMD5.HashData(usageKey, body, expectedChecksum);
            if (!CryptographicOperations.FixedTimeEquals(expectedChecksum, checksum))
            {
                throw new KerberosCryptographyException(KerberosCryptographyError.IntegrityCheckFailed);
            }

            return body[ConfounderSize..];
        }
        finally
        {
            CryptographicOperations.ZeroMemory(usageKey);
            CryptographicOperations.ZeroMemory(body);
        }
    }

    /// <summary>
    /// RFC 4757 section 4's <c>hmac-md5</c>: <c>HMAC(HMAC(K, "signaturekey\0"), MD5(T | data))</c>.
    /// </summary>
    private protected override byte[] ComputeChecksumWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data)
    {
        byte[] signingKey = HMACMD5.HashData(key, "signaturekey\0"u8);
        byte[] typedData = new byte[sizeof(int) + data.Length];
        BinaryPrimitives.WriteInt32LittleEndian(typedData, MessageType(usage));
        data.CopyTo(typedData.AsSpan(sizeof(int)));
        try
        {
            return HMACMD5.HashData(signingKey, MD5.HashData(typedData));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signingKey);
            CryptographicOperations.ZeroMemory(typedData);
        }
    }

    /// <summary>RFC 4757 section 5: <c>HMAC-SHA1(K, S)</c>.</summary>
    private protected override byte[] ComputePseudoRandomWithKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input) =>
        HMACSHA1.HashData(key, input);

    /// <summary>K1 = <c>HMAC(K, T)</c>, T the message type as four little-endian bytes.</summary>
    private static byte[] UsageKey(ReadOnlySpan<byte> key, int usage)
    {
        Span<byte> messageType = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(messageType, MessageType(usage));
        return HMACMD5.HashData(key, messageType);
    }

    /// <summary>RC4s <paramref name="source" /> into <paramref name="destination" /> under K3 = <c>HMAC(K1, checksum)</c>.</summary>
    private static void ApplyKeyStream(ReadOnlySpan<byte> usageKey, ReadOnlySpan<byte> checksum, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        byte[] messageKey = HMACMD5.HashData(usageKey, checksum);
        try
        {
            using Rc4 rc4 = new(messageKey);
            rc4.ApplyKeyStream(source, destination);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(messageKey);
        }
    }
}
