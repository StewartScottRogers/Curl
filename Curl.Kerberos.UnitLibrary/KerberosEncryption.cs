using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Curl.Kerberos;

/// <summary>
/// One Kerberos encryption type (RFC 3961's "encryption and checksum profile"): making a
/// key from a password, encrypting and decrypting with a key and key usage, the type's
/// keyed checksum, and its pseudorandom function. <see cref="Create" /> gives the one for
/// an encryption type number.
/// </summary>
/// <remarks>
/// Every operation takes the base key, never a derived one; the type derives what it needs
/// per key usage and zeroes it afterwards. Encryption uses the initial (all-zero) cipher
/// state, as every Kerberos message and RFC 4121 token does. Decryption checks integrity
/// and throws <see cref="KerberosCryptographyException" /> with
/// <see cref="KerberosCryptographyError.IntegrityCheckFailed" /> on a mismatch.
/// </remarks>
public abstract class KerberosEncryption
{
    private protected KerberosEncryption(KerberosEncryptionType encryptionType, int checksumType, int keySize, int checksumSize, IKerberosRandomSource randomSource)
    {
        EncryptionType = encryptionType;
        ChecksumType = checksumType;
        KeySize = keySize;
        ChecksumSize = checksumSize;
        RandomSource = randomSource;
    }

    /// <summary>Gets the encryption type.</summary>
    public KerberosEncryptionType EncryptionType { get; }

    /// <summary>
    /// Gets the number of the type's associated keyed checksum in IANA's "Kerberos Checksum
    /// Type Numbers" registry, e.g. 16 for <c>hmac-sha1-96-aes256</c>.
    /// </summary>
    public int ChecksumType { get; }

    /// <summary>Gets the length in bytes of a key.</summary>
    public int KeySize { get; }

    /// <summary>Gets the length in bytes of a checksum from <see cref="ComputeChecksum" />.</summary>
    public int ChecksumSize { get; }

    /// <summary>Gets where the confounders come from.</summary>
    private protected IKerberosRandomSource RandomSource { get; }

    /// <summary>Gives the encryption type numbered <paramref name="encryptionType" />.</summary>
    /// <param name="encryptionType">The encryption type.</param>
    /// <param name="randomSource">Where its confounders come from.</param>
    /// <exception cref="KerberosCryptographyException">
    /// <see cref="KerberosCryptographyError.UnsupportedEncryptionType" />: the type is not one of <see cref="KerberosEncryptionType" />'s.
    /// </exception>
    public static KerberosEncryption Create(KerberosEncryptionType encryptionType, IKerberosRandomSource randomSource) => encryptionType switch
    {
        KerberosEncryptionType.Des3CbcSha1 => new Des3CbcSha1KerberosEncryption(randomSource),
        KerberosEncryptionType.Aes128CtsHmacSha196 or KerberosEncryptionType.Aes256CtsHmacSha196 => AesSha1KerberosEncryption.ForType(encryptionType, randomSource),
        KerberosEncryptionType.Aes128CtsHmacSha256128 or KerberosEncryptionType.Aes256CtsHmacSha384192 => AesSha2KerberosEncryption.ForType(encryptionType, randomSource),
        KerberosEncryptionType.Rc4Hmac => new Rc4HmacKerberosEncryption(randomSource),
        KerberosEncryptionType.Camellia128CtsCmac or KerberosEncryptionType.Camellia256CtsCmac => CamelliaCmacKerberosEncryption.ForType(encryptionType, randomSource),
        _ => throw new KerberosCryptographyException(KerberosCryptographyError.UnsupportedEncryptionType),
    };

    /// <summary>
    /// Makes the key for <paramref name="password" /> (RFC 3961's <c>string-to-key</c>).
    /// </summary>
    /// <param name="password">The password.</param>
    /// <param name="salt">The salt, usually the realm followed by the principal's name components.</param>
    /// <param name="parameters">The type's string-to-key parameters, or empty for its default.</param>
    /// <returns>A key of <see cref="KeySize" /> bytes; the caller zeroes it.</returns>
    /// <exception cref="KerberosCryptographyException"><see cref="KerberosCryptographyError.BadStringToKeyParameters" />.</exception>
    public byte[] StringToKey(string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> parameters)
    {
        ArgumentNullException.ThrowIfNull(password);
        return StringToKeyWithPassword(password, salt, parameters);
    }

    /// <summary>Encrypts <paramref name="plaintext" /> under <paramref name="key" /> for <paramref name="usage" />, with a fresh confounder.</summary>
    /// <param name="key">The base key, <see cref="KeySize" /> bytes.</param>
    /// <param name="usage">The key usage number (RFC 4120 section 7.5.1).</param>
    /// <param name="plaintext">The bytes to encrypt.</param>
    /// <returns>The ciphertext, confounder and checksum included.</returns>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not <see cref="KeySize" /> bytes.</exception>
    public byte[] Encrypt(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> plaintext)
    {
        RequireKeySize(key);
        return EncryptWithKey(key, usage, plaintext);
    }

    /// <summary>Decrypts <paramref name="ciphertext" /> under <paramref name="key" /> for <paramref name="usage" /> and checks its integrity.</summary>
    /// <param name="key">The base key, <see cref="KeySize" /> bytes.</param>
    /// <param name="usage">The key usage number it was encrypted for.</param>
    /// <param name="ciphertext">The ciphertext from <see cref="Encrypt" /> or a peer.</param>
    /// <returns>The plaintext, confounder removed.</returns>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not <see cref="KeySize" /> bytes.</exception>
    /// <exception cref="KerberosCryptographyException">
    /// <see cref="KerberosCryptographyError.CiphertextTooShort" />,
    /// <see cref="KerberosCryptographyError.CiphertextNotWholeBlocks" /> or
    /// <see cref="KerberosCryptographyError.IntegrityCheckFailed" />.
    /// </exception>
    public byte[] Decrypt(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> ciphertext)
    {
        RequireKeySize(key);
        return DecryptWithKey(key, usage, ciphertext);
    }

    /// <summary>Computes the type's keyed checksum of <paramref name="data" /> for <paramref name="usage" />.</summary>
    /// <param name="key">The base key, <see cref="KeySize" /> bytes.</param>
    /// <param name="usage">The key usage number.</param>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>A checksum of <see cref="ChecksumSize" /> bytes.</returns>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not <see cref="KeySize" /> bytes.</exception>
    public byte[] ComputeChecksum(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data)
    {
        RequireKeySize(key);
        return ComputeChecksumWithKey(key, usage, data);
    }

    /// <summary>Returns whether <paramref name="checksum" /> is the keyed checksum of <paramref name="data" />, comparing in constant time.</summary>
    /// <param name="key">The base key, <see cref="KeySize" /> bytes.</param>
    /// <param name="usage">The key usage number.</param>
    /// <param name="data">The checksummed bytes.</param>
    /// <param name="checksum">The checksum to check.</param>
    /// <returns><see langword="true" /> when it matches.</returns>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not <see cref="KeySize" /> bytes.</exception>
    public bool VerifyChecksum(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data, ReadOnlySpan<byte> checksum)
    {
        byte[] expected = ComputeChecksum(key, usage, data);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expected, checksum);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
        }
    }

    /// <summary>Computes the type's pseudorandom function of <paramref name="input" /> (RFC 3961 section 3's <c>pseudo-random</c>).</summary>
    /// <param name="key">The base key, <see cref="KeySize" /> bytes.</param>
    /// <param name="input">The octet string to feed it.</param>
    /// <returns>The type's pseudorandom output.</returns>
    /// <exception cref="ArgumentException"><paramref name="key" /> is not <see cref="KeySize" /> bytes.</exception>
    public byte[] ComputePseudoRandom(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        RequireKeySize(key);
        return ComputePseudoRandomWithKey(key, input);
    }

    /// <summary>
    /// Reads the RFC 3962 string-to-key parameters: an iteration count as four big-endian
    /// bytes, zero meaning 2^32, or <paramref name="defaultIterationCount" /> when empty.
    /// </summary>
    /// <exception cref="KerberosCryptographyException">
    /// <see cref="KerberosCryptographyError.BadStringToKeyParameters" />: not zero or four
    /// bytes, or a count of 2^24 or more, MIT Kerberos' limit.
    /// </exception>
    internal static int ReadIterationCount(ReadOnlySpan<byte> parameters, int defaultIterationCount)
    {
        const long MaximumIterationCount = 0xffffff;
        if (parameters.IsEmpty)
        {
            return defaultIterationCount;
        }

        long count = parameters.Length == sizeof(uint) ? BinaryPrimitives.ReadUInt32BigEndian(parameters) : 0;
        if (count == 0 || count > MaximumIterationCount)
        {
            throw new KerberosCryptographyException(KerberosCryptographyError.BadStringToKeyParameters);
        }

        return (int)count;
    }

    /// <summary>The five bytes of a key usage constant: the usage, big-endian, then <paramref name="purpose" /> (0x99 checksum, 0xAA encryption, 0x55 integrity).</summary>
    private protected static byte[] UsageConstant(int usage, byte purpose)
    {
        byte[] constant = new byte[5];
        BinaryPrimitives.WriteInt32BigEndian(constant, usage);
        constant[4] = purpose;
        return constant;
    }

    /// <summary>Makes the key; <paramref name="password" /> is not null.</summary>
    private protected abstract byte[] StringToKeyWithPassword(string password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> parameters);

    /// <summary>Encrypts; <paramref name="key" /> is <see cref="KeySize" /> bytes.</summary>
    private protected abstract byte[] EncryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> plaintext);

    /// <summary>Decrypts and checks integrity; <paramref name="key" /> is <see cref="KeySize" /> bytes.</summary>
    private protected abstract byte[] DecryptWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> ciphertext);

    /// <summary>Computes the checksum; <paramref name="key" /> is <see cref="KeySize" /> bytes.</summary>
    private protected abstract byte[] ComputeChecksumWithKey(ReadOnlySpan<byte> key, int usage, ReadOnlySpan<byte> data);

    /// <summary>Computes the pseudorandom function; <paramref name="key" /> is <see cref="KeySize" /> bytes.</summary>
    private protected abstract byte[] ComputePseudoRandomWithKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input);

    private void RequireKeySize(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"{EncryptionType} needs a key of {KeySize} bytes; this is {key.Length}.", nameof(key));
        }
    }
}
