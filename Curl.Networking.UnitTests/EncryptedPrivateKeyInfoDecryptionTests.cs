using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// Pins <see cref="EncryptedPrivateKeyInfoDecryption" /> (ADR-0354): PBES2 with PBKDF2 and an
/// AES or DES-EDE3 CBC cipher decrypts to the <c>PrivateKeyInfo</c> the BCL encrypted or a
/// hand-written <c>EncryptedPrivateKeyInfo</c> holds, with or without PBKDF2's optional key
/// length and PRF; any other scheme, a malformed structure and a wrong passphrase are a
/// <see cref="CryptographicException" />.
/// </summary>
[TestClass]
public sealed class EncryptedPrivateKeyInfoDecryptionTests
{
    private const string Passphrase = "secret";

    private const string Aes128Oid = "2.16.840.1.101.3.4.1.2";

    private const string Aes192Oid = "2.16.840.1.101.3.4.1.22";

    private const string Aes256Oid = "2.16.840.1.101.3.4.1.42";

    private const string TripleDesOid = "1.2.840.113549.3.7";

    private const string HmacSha1Oid = "1.2.840.113549.2.7";

    private const string HmacSha256Oid = "1.2.840.113549.2.9";

    private const string HmacSha384Oid = "1.2.840.113549.2.10";

    private const string HmacSha512Oid = "1.2.840.113549.2.11";

    private static readonly byte[] PrivateKeyInfo = [.. Enumerable.Range(0, 48).Select(value => (byte)value)];

    [TestMethod]
    public void Decrypt_OfAKeyTheBclEncrypted_GivesItsPrivateKeyInfo()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var encrypted = key.ExportEncryptedPkcs8PrivateKey(Passphrase, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000));

        var decrypted = EncryptedPrivateKeyInfoDecryption.Decrypt(encrypted, Passphrase);

        using var imported = ECDsa.Create();
        imported.ImportPkcs8PrivateKey(decrypted, out var bytesRead);
        Assert.AreEqual(decrypted.Length, bytesRead);
        CollectionAssert.AreEqual(key.ExportParameters(true).D, imported.ExportParameters(true).D);
    }

    [TestMethod]
    [DataRow(Aes128Oid, 16, HmacSha256Oid, true, false)]
    [DataRow(Aes192Oid, 24, HmacSha384Oid, false, false)]
    [DataRow(Aes256Oid, 32, HmacSha512Oid, true, true)]
    [DataRow(TripleDesOid, 24, HmacSha1Oid, false, true)]
    [DataRow(Aes256Oid, 32, null, false, false)]
    [DataRow(Aes128Oid, 16, null, false, true)]
    public void Decrypt_OfEachCipherAndPrf_GivesThePrivateKeyInfo(string cipherOid, int keyLength, string? prfOid, bool prfWithNull, bool writeKeyLength)
    {
        var encrypted = Encrypt(new Encryption(cipherOid, keyLength, prfOid, prfWithNull, writeKeyLength));

        var decrypted = EncryptedPrivateKeyInfoDecryption.Decrypt(encrypted, Passphrase);

        CollectionAssert.AreEqual(PrivateKeyInfo, decrypted);
    }

    [TestMethod]
    public void Decrypt_WithAWrongPassphrase_Throws()
    {
        var encrypted = Encrypt(new Encryption(Aes256Oid, 32, HmacSha256Oid));

        Assert.ThrowsExactly<CryptographicException>(() => EncryptedPrivateKeyInfoDecryption.Decrypt(encrypted, "wrong"));
    }

    [TestMethod]
    [DataRow("not PBES2")]
    [DataRow("not PBKDF2")]
    [DataRow("an unknown PRF")]
    [DataRow("an unknown cipher")]
    [DataRow("an IV of the wrong length")]
    [DataRow("an iteration count of zero")]
    [DataRow("an iteration count past 32 bits")]
    [DataRow("not DER")]
    [DataRow("a trailing field")]
    public void Decrypt_OfAnUnsupportedOrMalformedStructure_Throws(string malformation)
    {
        var encrypted = malformation switch
        {
            "not PBES2" => Encrypt(new Encryption(Aes256Oid, 32, null) { SchemeOid = "1.2.840.113549.1.5.3" }),
            "not PBKDF2" => Encrypt(new Encryption(Aes256Oid, 32, null) { DerivationOid = "1.2.840.113549.1.5.14" }),
            "an unknown PRF" => Encrypt(new Encryption(Aes256Oid, 32, "1.2.840.113549.2.8")),
            "an unknown cipher" => Encrypt(new Encryption("2.16.840.1.101.3.4.1.6", 16, null)),
            "an IV of the wrong length" => Encrypt(new Encryption(Aes256Oid, 32, null) { IvLength = 8 }),
            "an iteration count of zero" => Encrypt(new Encryption(Aes256Oid, 32, null) { Iterations = 0 }),
            "an iteration count past 32 bits" => Encrypt(new Encryption(Aes256Oid, 32, null) { Iterations = 1L << 32 }),
            "not DER" => [1, 2, 3],
            _ => [.. Encrypt(new Encryption(Aes256Oid, 32, null)), 2, 1, 0],
        };

        Assert.ThrowsExactly<CryptographicException>(() => EncryptedPrivateKeyInfoDecryption.Decrypt(encrypted, Passphrase));
    }

    /// <summary>Writes <see cref="PrivateKeyInfo" /> encrypted with PBES2 as <paramref name="encryption" /> says.</summary>
    /// <param name="encryption">The scheme and how its parameters are written.</param>
    /// <returns>The DER <c>EncryptedPrivateKeyInfo</c>.</returns>
    internal static byte[] Encrypt(Encryption encryption) => Encrypt(PrivateKeyInfo, Passphrase, encryption);

    /// <summary>Writes a <c>PrivateKeyInfo</c> encrypted with PBES2 as <paramref name="encryption" /> says.</summary>
    /// <param name="privateKeyInfo">The <c>PrivateKeyInfo</c> to encrypt.</param>
    /// <param name="passphrase">The passphrase.</param>
    /// <param name="encryption">The scheme and how its parameters are written.</param>
    /// <returns>The DER <c>EncryptedPrivateKeyInfo</c>.</returns>
    internal static byte[] Encrypt(byte[] privateKeyInfo, string passphrase, Encryption encryption)
    {
        byte[] salt = [9, 8, 7, 6, 5, 4, 3, 2];
        var prf = encryption.PrfOid switch
        {
            HmacSha256Oid => HashAlgorithmName.SHA256,
            HmacSha384Oid => HashAlgorithmName.SHA384,
            HmacSha512Oid => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA1,
        };
        var iterations = (int)Math.Clamp(encryption.Iterations, 1, 2048);
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), salt, iterations, prf, encryption.KeyLength);
        using SymmetricAlgorithm cipher = encryption.CipherOid == TripleDesOid ? TripleDES.Create() : Aes.Create();
        cipher.Key = key;
        var iv = new byte[cipher.BlockSize / 8];
        var encryptedData = cipher.EncryptCbc(privateKeyInfo, iv);

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(encryption.SchemeOid);
                using (writer.PushSequence())
                {
                    WriteDerivation(writer, encryption, salt);
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(encryption.CipherOid);
                        writer.WriteOctetString(encryption.IvLength is { } ivLength ? new byte[ivLength] : iv);
                    }
                }
            }

            writer.WriteOctetString(encryptedData);
        }

        return writer.Encode();
    }

    private static void WriteDerivation(AsnWriter writer, Encryption encryption, byte[] salt)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(encryption.DerivationOid);
            using (writer.PushSequence())
            {
                writer.WriteOctetString(salt);
                writer.WriteInteger(encryption.Iterations);
                if (encryption.WriteKeyLength)
                {
                    writer.WriteInteger(encryption.KeyLength);
                }

                if (encryption.PrfOid is not null)
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(encryption.PrfOid);
                        if (encryption.PrfWithNull)
                        {
                            writer.WriteNull();
                        }
                    }
                }
            }
        }
    }

    /// <summary>How <see cref="Encrypt(Encryption)" /> encrypts and writes its parameters.</summary>
    /// <param name="CipherOid">The CBC cipher's identifier.</param>
    /// <param name="KeyLength">The cipher's key length in bytes.</param>
    /// <param name="PrfOid">The PBKDF2 PRF's identifier, or <see langword="null" /> to leave it out.</param>
    /// <param name="PrfWithNull">Whether the PRF's identifier carries NULL parameters.</param>
    /// <param name="WriteKeyLength">Whether PBKDF2's optional key length is written.</param>
    internal sealed record Encryption(string CipherOid, int KeyLength, string? PrfOid, bool PrfWithNull = true, bool WriteKeyLength = false)
    {
        /// <summary>Gets the encryption scheme's identifier.</summary>
        public string SchemeOid { get; init; } = "1.2.840.113549.1.5.13";

        /// <summary>Gets the key derivation's identifier.</summary>
        public string DerivationOid { get; init; } = "1.2.840.113549.1.5.12";

        /// <summary>Gets the iteration count written.</summary>
        public long Iterations { get; init; } = 2048;

        /// <summary>Gets the length of a zero IV written in place of the right one, or <see langword="null" />.</summary>
        public int? IvLength { get; init; }
    }
}
