using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// Decrypts a PKCS #8 <c>EncryptedPrivateKeyInfo</c> (an <c>ENCRYPTED PRIVATE KEY</c> PEM
/// block, RFC 5958 section 3) to the <c>PrivateKeyInfo</c> it holds, for the keys
/// <see cref="HandBuiltPrivateKeyReader" /> reads: the BCL decrypts PKCS #8 only into the RSA,
/// EC and other key types it holds itself (ADR-0354). It takes PBES2 (RFC 8018 section 6.2)
/// with PBKDF2 over HMAC-SHA-1, -256, -384 or -512 and AES-128, -192 or -256 or DES-EDE3 in
/// CBC mode, which covers what <c>openssl pkcs8 -topk8</c> and <c>openssl genpkey</c> write.
/// </summary>
internal static class EncryptedPrivateKeyInfoDecryption
{
    private const string Pbes2Oid = "1.2.840.113549.1.5.13";

    private const string Pbkdf2Oid = "1.2.840.113549.1.5.12";

    // RFC 8018 appendix B.1: the PRF is HMAC-SHA-1 when the parameters name none.
    private static readonly Dictionary<string, HashAlgorithmName> Prfs = new()
    {
        ["1.2.840.113549.2.7"] = HashAlgorithmName.SHA1,
        ["1.2.840.113549.2.9"] = HashAlgorithmName.SHA256,
        ["1.2.840.113549.2.10"] = HashAlgorithmName.SHA384,
        ["1.2.840.113549.2.11"] = HashAlgorithmName.SHA512,
    };

    // Each CBC cipher's key length in bytes and its BCL implementation.
    private static readonly Dictionary<string, (int KeyLength, Func<SymmetricAlgorithm> Create)> Ciphers = new()
    {
        ["2.16.840.1.101.3.4.1.2"] = (16, Aes.Create),
        ["2.16.840.1.101.3.4.1.22"] = (24, Aes.Create),
        ["2.16.840.1.101.3.4.1.42"] = (32, Aes.Create),
        ["1.2.840.113549.3.7"] = (24, TripleDES.Create),
    };

    /// <summary>Decrypts an <c>EncryptedPrivateKeyInfo</c> with the <c>--pass</c> passphrase.</summary>
    /// <param name="encryptedPrivateKeyInfo">The DER (or BER) of the <c>EncryptedPrivateKeyInfo</c>.</param>
    /// <param name="passphrase">The passphrase, whose UTF-8 bytes are PBKDF2's password.</param>
    /// <returns>The <c>PrivateKeyInfo</c> it holds.</returns>
    /// <exception cref="CryptographicException">
    /// The structure is malformed, its scheme is not one of the above, or the passphrase is wrong.
    /// </exception>
    public static byte[] Decrypt(byte[] encryptedPrivateKeyInfo, string passphrase)
    {
        try
        {
            return DecryptPbes2(encryptedPrivateKeyInfo, Encoding.UTF8.GetBytes(passphrase));
        }
        catch (Exception exception) when (exception is AsnContentException or KeyNotFoundException or ArgumentException)
        {
            throw new CryptographicException("The encrypted private key is malformed or of an unsupported scheme.", exception);
        }
    }

    // EncryptedPrivateKeyInfo ::= SEQUENCE { encryptionAlgorithm AlgorithmIdentifier
    // { PBES2, PBES2-params }, encryptedData OCTET STRING }.
    private static byte[] DecryptPbes2(byte[] encryptedPrivateKeyInfo, byte[] password)
    {
        var reader = new AsnReader(encryptedPrivateKeyInfo, AsnEncodingRules.BER);
        var info = reader.ReadSequence();
        reader.ThrowIfNotEmpty();
        var algorithm = info.ReadSequence();
        RequireThat(algorithm.ReadObjectIdentifier() == Pbes2Oid, "The encryption scheme is not PBES2.");
        var parameters = algorithm.ReadSequence();
        algorithm.ThrowIfNotEmpty();
        var encryptedData = info.ReadOctetString();
        info.ThrowIfNotEmpty();

        var (salt, iterations, prf) = ReadPbkdf2(parameters.ReadSequence());
        var encryption = parameters.ReadSequence();
        parameters.ThrowIfNotEmpty();
        var (keyLength, create) = Ciphers[encryption.ReadObjectIdentifier()];
        var iv = encryption.ReadOctetString();
        encryption.ThrowIfNotEmpty();

        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, prf, keyLength);
        CryptographicOperations.ZeroMemory(password);
        using var cipher = create();
        cipher.Key = key;
        CryptographicOperations.ZeroMemory(key);
        return cipher.DecryptCbc(encryptedData, iv);
    }

    // keyDerivationFunc AlgorithmIdentifier { PBKDF2, PBKDF2-params }, where PBKDF2-params ::=
    // SEQUENCE { salt OCTET STRING, iterationCount INTEGER, keyLength INTEGER OPTIONAL,
    // prf AlgorithmIdentifier DEFAULT hmacWithSHA1 }. The cipher fixes the key length.
    private static (byte[] Salt, int Iterations, HashAlgorithmName Prf) ReadPbkdf2(AsnReader derivation)
    {
        RequireThat(derivation.ReadObjectIdentifier() == Pbkdf2Oid, "The PBES2 key derivation is not PBKDF2.");
        var pbkdf2 = derivation.ReadSequence();
        derivation.ThrowIfNotEmpty();
        var salt = pbkdf2.ReadOctetString();
        RequireThat(pbkdf2.TryReadInt32(out var iterations) && iterations > 0, "The iteration count is not a positive 32-bit integer.");
        if (pbkdf2.HasData && pbkdf2.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
        {
            pbkdf2.ReadInteger();
        }

        var prf = pbkdf2.HasData ? ReadPrf(pbkdf2.ReadSequence()) : HashAlgorithmName.SHA1;
        pbkdf2.ThrowIfNotEmpty();
        return (salt, iterations, prf);
    }

    // The PRF's AlgorithmIdentifier carries NULL parameters or none.
    private static HashAlgorithmName ReadPrf(AsnReader prfIdentifier)
    {
        var prf = Prfs[prfIdentifier.ReadObjectIdentifier()];
        if (prfIdentifier.HasData)
        {
            prfIdentifier.ReadNull();
        }

        prfIdentifier.ThrowIfNotEmpty();
        return prf;
    }

    private static void RequireThat(bool condition, string message)
    {
        if (!condition)
        {
            throw new CryptographicException(message);
        }
    }
}
