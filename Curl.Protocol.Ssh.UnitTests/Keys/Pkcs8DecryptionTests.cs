using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="Pkcs8Decryption" />'s reading of PBES2's parameters on structures written
/// here, around plaintext encrypted with the BCL; the schemes OpenSSL writes are pinned by
/// <see cref="SshPrivateKeyReaderTests" />.
/// </summary>
[TestClass]
public sealed class Pkcs8DecryptionTests
{
    private const string Aes128Cbc = "2.16.840.1.101.3.4.1.2";

    private const string HmacWithSha256 = "1.2.840.113549.2.9";

    private static readonly byte[] Secret = Encoding.UTF8.GetBytes("secret");

    private static readonly byte[] Salt = [1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly byte[] Iv = new byte[16];

    private static readonly byte[] Plain = Encoding.ASCII.GetBytes("a PrivateKeyInfo");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Decrypt_Pbes2WithAKeyLength_SkipsItAndDecrypts()
    {
        byte[] der = Pbes2(iterations: 1000, keyLength: 16, prfOid: HmacWithSha256, cipherOid: Aes128Cbc, Encrypt(HashAlgorithmName.SHA256, 1000));
        Diagnostics.Arrange("PBES2", "PBKDF2 HMAC-SHA-256, 1000 iterations, key length 16, AES-128-CBC");
        Diagnostics.Bytes("EncryptedPrivateKeyInfo", der);

        byte[] decrypted = Pkcs8Decryption.Decrypt(der, Secret);

        Diagnostics.ActBytes("decrypted", decrypted);
        Diagnostics.AssertBytes("decrypted", Plain, decrypted);
        CollectionAssert.AreEqual(Plain, decrypted);
    }

    [TestMethod]
    public void Decrypt_Pbes2NamingHmacSha1Explicitly_Decrypts()
    {
        byte[] der = Pbes2(iterations: 1000, keyLength: null, prfOid: "1.2.840.113549.2.7", cipherOid: Aes128Cbc, Encrypt(HashAlgorithmName.SHA1, 1000));
        Diagnostics.Arrange("PBES2", "PBKDF2 HMAC-SHA-1 named, 1000 iterations, AES-128-CBC");
        Diagnostics.Bytes("EncryptedPrivateKeyInfo", der);

        byte[] decrypted = Pkcs8Decryption.Decrypt(der, Secret);

        Diagnostics.ActBytes("decrypted", decrypted);
        Diagnostics.AssertBytes("decrypted", Plain, decrypted);
        CollectionAssert.AreEqual(Plain, decrypted);
    }

    [TestMethod]
    public void Decrypt_UnknownScheme_Throws()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.12.1.3");
            }

            writer.WriteOctetString(new byte[16]);
        }

        Diagnostics.Arrange("scheme OID", "1.2.840.113549.1.12.1.3 (PKCS #12 SHA-1 3DES)");
        Diagnostics.Bytes("EncryptedPrivateKeyInfo", writer.Encode());

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Pkcs8Decryption.Decrypt(writer.Encode(), Secret));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    public void Decrypt_KeyDerivationNotPbkdf2_Throws()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.5.13");
                using (writer.PushSequence())
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier("1.3.6.1.4.1.11591.4.11");
                    }
                }
            }

            writer.WriteOctetString(new byte[16]);
        }

        Diagnostics.Arrange("key derivation OID", "1.3.6.1.4.1.11591.4.11 (scrypt)");
        Diagnostics.Bytes("EncryptedPrivateKeyInfo", writer.Encode());

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Pkcs8Decryption.Decrypt(writer.Encode(), Secret));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    [TestMethod]
    [DataRow("1.2.840.113549.2.8", Aes128Cbc, 1000L, DisplayName = "PRF HMAC-SHA-224")]
    [DataRow(HmacWithSha256, "1.2.840.113549.3.2", 1000L, DisplayName = "cipher RC2-CBC")]
    [DataRow(HmacWithSha256, Aes128Cbc, 0L, DisplayName = "no iterations")]
    [DataRow(HmacWithSha256, Aes128Cbc, 4294967296L, DisplayName = "iterations past 32 bits")]
    public void Decrypt_ParametersNotRead_Throws(string prfOid, string cipherOid, long iterations)
    {
        byte[] der = Pbes2(iterations, keyLength: null, prfOid, cipherOid, new byte[16]);
        Diagnostics.Arrange("PRF OID", prfOid);
        Diagnostics.Arrange("cipher OID", cipherOid);
        Diagnostics.Arrange("iterations", iterations);

        var failure = Assert.ThrowsExactly<CryptographicException>(() => Pkcs8Decryption.Decrypt(der, Secret));

        Diagnostics.ActAndAssertThrown(nameof(CryptographicException), failure);
    }

    private static byte[] Encrypt(HashAlgorithmName prf, int iterations)
    {
        using Aes aes = Aes.Create();
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(Secret, Salt, iterations, prf, 16);
        return aes.EncryptCbc(Plain, Iv);
    }

    private static byte[] Pbes2(long iterations, int? keyLength, string prfOid, string cipherOid, byte[] data)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier("1.2.840.113549.1.5.13");
                using (writer.PushSequence())
                {
                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier("1.2.840.113549.1.5.12");
                        using (writer.PushSequence())
                        {
                            writer.WriteOctetString(Salt);
                            writer.WriteInteger(new BigInteger(iterations));
                            if (keyLength is { } length)
                            {
                                writer.WriteInteger(length);
                            }

                            using (writer.PushSequence())
                            {
                                writer.WriteObjectIdentifier(prfOid);
                                writer.WriteNull();
                            }
                        }
                    }

                    using (writer.PushSequence())
                    {
                        writer.WriteObjectIdentifier(cipherOid);
                        writer.WriteOctetString(Iv);
                    }
                }
            }

            writer.WriteOctetString(data);
        }

        return writer.Encode();
    }
}
