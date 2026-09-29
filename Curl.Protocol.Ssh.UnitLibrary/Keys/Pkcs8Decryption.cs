using System.Formats.Asn1;
using System.Security.Cryptography;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Decrypts a PKCS #8 <c>EncryptedPrivateKeyInfo</c> (<c>ENCRYPTED PRIVATE KEY</c>, RFC
/// 5958 section 3) by hand, since the BCL's own import takes RSA and EC keys only: PBES2
/// (RFC 8018 section 6.2) with PBKDF2 over HMAC-SHA-1, -256, -384 or -512 and AES-CBC,
/// triple DES or DES, and PBES1 (section 6.1) with MD5 or SHA-1 and DES.
/// </summary>
internal static class Pkcs8Decryption
{
    private const string Pbes2 = "1.2.840.113549.1.5.13";

    private const string Pbkdf2 = "1.2.840.113549.1.5.12";

    private const string PbeWithMd5AndDes = "1.2.840.113549.1.5.3";

    private const string PbeWithSha1AndDes = "1.2.840.113549.1.5.10";

    private const int Pbes1KeyLength = 8;

    /// <summary>
    /// Decrypts <paramref name="der" /> to the <c>PrivateKeyInfo</c> it holds.
    /// </summary>
    /// <param name="der">The <c>EncryptedPrivateKeyInfo</c>.</param>
    /// <param name="passphrase">The <c>--pass</c> bytes; empty when it was not given.</param>
    /// <returns>The DER <c>PrivateKeyInfo</c>.</returns>
    /// <exception cref="AsnContentException">The structure is malformed.</exception>
    /// <exception cref="CryptographicException">A scheme is not one of the above, or the passphrase is wrong.</exception>
    internal static byte[] Decrypt(byte[] der, byte[] passphrase)
    {
        AsnReader info = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        AsnReader algorithm = info.ReadSequence();
        string scheme = algorithm.ReadObjectIdentifier();
        byte[] data = info.ReadOctetString();
        return scheme switch
        {
            Pbes2 => DecryptPbes2(algorithm.ReadSequence(), passphrase, data),
            PbeWithMd5AndDes => DecryptPbes1(algorithm.ReadSequence(), HashAlgorithmName.MD5, passphrase, data),
            PbeWithSha1AndDes => DecryptPbes1(algorithm.ReadSequence(), HashAlgorithmName.SHA1, passphrase, data),
            _ => throw new CryptographicException("The PKCS #8 encryption scheme is not PBES2 or a DES PBES1."),
        };
    }

    // PBES2-params: keyDerivationFunc (PBKDF2 with salt, iterations, optional key length,
    // optional PRF), then encryptionScheme (cipher OID and its IV).
    private static byte[] DecryptPbes2(AsnReader parameters, byte[] passphrase, byte[] data)
    {
        AsnReader derivation = parameters.ReadSequence();
        if (derivation.ReadObjectIdentifier() != Pbkdf2)
        {
            throw new CryptographicException("The PBES2 key derivation is not PBKDF2.");
        }

        AsnReader derivationParameters = derivation.ReadSequence();
        byte[] salt = derivationParameters.ReadOctetString();
        int iterations = ReadIterations(derivationParameters);
        if (derivationParameters.HasData && derivationParameters.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
        {
            derivationParameters.ReadInteger();
        }

        HashAlgorithmName prf = derivationParameters.HasData ? Prf(derivationParameters.ReadSequence().ReadObjectIdentifier()) : HashAlgorithmName.SHA1;
        AsnReader encryption = parameters.ReadSequence();
        KeyFileCipher cipher = Cipher(encryption.ReadObjectIdentifier());
        byte[] iv = encryption.ReadOctetString();
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, iterations, prf, KeyFileCbcDecryption.KeyLength(cipher));
        return KeyFileCbcDecryption.Decrypt(cipher, key, iv, data);
    }

    // PBKDF1: T = Hash(P || S), hashed again iterations - 1 times; DES key and IV are its
    // first and second eight bytes.
    private static byte[] DecryptPbes1(AsnReader parameters, HashAlgorithmName hash, byte[] passphrase, byte[] data)
    {
        byte[] salt = parameters.ReadOctetString();
        int iterations = ReadIterations(parameters);
        byte[] derived = CryptographicOperations.HashData(hash, [.. passphrase, .. salt]);
        for (int round = 1; round < iterations; round++)
        {
            derived = CryptographicOperations.HashData(hash, derived);
        }

        return KeyFileCbcDecryption.Decrypt(KeyFileCipher.Des, derived[..Pbes1KeyLength], derived[Pbes1KeyLength..(2 * Pbes1KeyLength)], data);
    }

    private static int ReadIterations(AsnReader reader) =>
        reader.TryReadInt32(out int iterations) && iterations > 0 ? iterations : throw new CryptographicException("The iteration count is not a positive 32-bit integer.");

    private static HashAlgorithmName Prf(string oid) => oid switch
    {
        "1.2.840.113549.2.7" => HashAlgorithmName.SHA1,
        "1.2.840.113549.2.9" => HashAlgorithmName.SHA256,
        "1.2.840.113549.2.10" => HashAlgorithmName.SHA384,
        "1.2.840.113549.2.11" => HashAlgorithmName.SHA512,
        _ => throw new CryptographicException("The PBKDF2 PRF is not HMAC with SHA-1 or SHA-2."),
    };

    private static KeyFileCipher Cipher(string oid) => oid switch
    {
        "2.16.840.1.101.3.4.1.2" => KeyFileCipher.Aes128,
        "2.16.840.1.101.3.4.1.22" => KeyFileCipher.Aes192,
        "2.16.840.1.101.3.4.1.42" => KeyFileCipher.Aes256,
        "1.2.840.113549.3.7" => KeyFileCipher.TripleDes,
        "1.3.14.3.2.7" => KeyFileCipher.Des,
        _ => throw new CryptographicException("The PBES2 cipher is not AES-CBC, DES-EDE3-CBC or DES-CBC."),
    };
}
