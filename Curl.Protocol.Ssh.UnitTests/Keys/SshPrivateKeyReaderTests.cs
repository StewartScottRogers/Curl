using System.Formats.Asn1;
using System.Text;
using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="SshPrivateKeyReader" /> against key files written by <c>ssh-keygen</c> and
/// OpenSSL: every format of one key reads to the same public blob as its <c>.pub</c> file,
/// and every file libssh2 fails to use reads as none (ADR-0230).
/// </summary>
[TestClass]
public sealed class SshPrivateKeyReaderTests
{
    private static readonly byte[] Secret = Encoding.UTF8.GetBytes(TestUserKeys.Passphrase);

    [TestMethod]
    [DataRow(TestUserKeys.RsaPkcs1, DisplayName = "PKCS #1")]
    [DataRow(TestUserKeys.RsaOpenSsh, DisplayName = "openssh-key-v1")]
    [DataRow(TestUserKeys.RsaPkcs8, DisplayName = "PKCS #8")]
    [DataRow(TestUserKeys.RsaPkcs8Aes256Sha256, DisplayName = "PKCS #8, PBES2 AES-256 SHA-256")]
    [DataRow(TestUserKeys.RsaPkcs8Aes128Sha1, DisplayName = "PKCS #8, PBES2 AES-128 default SHA-1")]
    [DataRow(TestUserKeys.RsaPkcs8Aes192Sha384, DisplayName = "PKCS #8, PBES2 AES-192 SHA-384")]
    [DataRow(TestUserKeys.RsaPkcs8TripleDesSha512, DisplayName = "PKCS #8, PBES2 3DES SHA-512")]
    [DataRow(TestUserKeys.RsaPkcs8DesSha256, DisplayName = "PKCS #8, PBES2 DES SHA-256")]
    [DataRow(TestUserKeys.RsaPkcs8Md5Des, DisplayName = "PKCS #8, PBES1 MD5 DES")]
    [DataRow(TestUserKeys.RsaPkcs8Sha1Des, DisplayName = "PKCS #8, PBES1 SHA-1 DES")]
    [DataRow(TestUserKeys.RsaPkcs1Aes128, DisplayName = "PKCS #1, PEM AES-128, as measured")]
    [DataRow(TestUserKeys.RsaPkcs1Aes192, DisplayName = "PKCS #1, PEM AES-192")]
    [DataRow(TestUserKeys.RsaPkcs1Aes256, DisplayName = "PKCS #1, PEM AES-256")]
    [DataRow(TestUserKeys.RsaPkcs1Des, DisplayName = "PKCS #1, PEM DES")]
    [DataRow(TestUserKeys.RsaPkcs1TripleDes, DisplayName = "PKCS #1, PEM 3DES")]
    public void Read_RsaKeyInEachFormat_ReadsTheKeyOfItsPublicKeyFile(string text)
    {
        SshPrivateKey? key = SshPrivateKeyReader.Read(text, Secret);

        Assert.IsInstanceOfType<RsaSshPrivateKey>(key);
        Assert.AreEqual("ssh-rsa", key.KeyType);
        CollectionAssert.AreEqual(SshPublicKeyFile.Parse(TestUserKeys.RsaPublicKeyFile)!.Blob, key.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow(TestUserKeys.EcdsaP256Sec1, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "P-256 SEC 1")]
    [DataRow(TestUserKeys.EcdsaP256OpenSsh, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "P-256 openssh-key-v1")]
    [DataRow(TestUserKeys.EcdsaP256Pkcs8, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "P-256 PKCS #8")]
    [DataRow(TestUserKeys.EcdsaP256Sec1WithoutPublicKey, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "P-256 SEC 1 without its point")]
    [DataRow(TestUserKeys.EcdsaP256Sec1Aes128, TestUserKeys.EcdsaP256PublicKeyFile, DisplayName = "P-256 SEC 1, PEM AES-128")]
    [DataRow(TestUserKeys.EcdsaP384Sec1, TestUserKeys.EcdsaP384PublicKeyFile, DisplayName = "P-384 SEC 1")]
    [DataRow(TestUserKeys.EcdsaP521OpenSsh, TestUserKeys.EcdsaP521PublicKeyFile, DisplayName = "P-521 openssh-key-v1")]
    public void Read_EcdsaKeyInEachFormat_ReadsTheKeyOfItsPublicKeyFile(string text, string publicKeyFile)
    {
        SshPublicKey expected = SshPublicKeyFile.Parse(publicKeyFile)!;

        SshPrivateKey? key = SshPrivateKeyReader.Read(text, Secret);

        Assert.IsInstanceOfType<EcdsaSshPrivateKey>(key);
        Assert.AreEqual(expected.KeyType, key.KeyType);
        CollectionAssert.AreEqual(expected.Blob, key.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow("traditional", DisplayName = "DSA PRIVATE KEY")]
    [DataRow("pkcs8", DisplayName = "PKCS #8, y computed")]
    [DataRow("openssh", DisplayName = "openssh-key-v1")]
    public void Read_DsaKeyInEachFormat_ReadsTheRfc6979Key(string format)
    {
        string text = format switch
        {
            "traditional" => Pem("DSA PRIVATE KEY", TestDsaKey.Traditional()),
            "pkcs8" => Pem("PRIVATE KEY", TestDsaKey.Pkcs8()),
            _ => Pem("OPENSSH PRIVATE KEY", TestDsaKey.OpenSsh()),
        };

        SshPrivateKey? key = SshPrivateKeyReader.Read(text, []);

        Assert.IsInstanceOfType<DsaSshPrivateKey>(key);
        CollectionAssert.AreEqual(TestDsaKey.PublicKeyBlob, key.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow(TestUserKeys.RsaPkcs1Aes128, DisplayName = "PEM AES-128")]
    [DataRow(TestUserKeys.RsaPkcs1Des, DisplayName = "PEM DES")]
    [DataRow(TestUserKeys.RsaPkcs8Aes256Sha256, DisplayName = "PKCS #8 PBES2")]
    [DataRow(TestUserKeys.RsaPkcs8Md5Des, DisplayName = "PKCS #8 PBES1")]
    [DataRow(TestUserKeys.EcdsaP256Sec1Aes128, DisplayName = "EC PEM AES-128")]
    public void Read_EncryptedKeyWithoutOrWithAWrongPassphrase_ReadsNoneAsMeasured(string text)
    {
        Assert.IsNull(SshPrivateKeyReader.Read(text, []), "no --pass");
        Assert.IsNull(SshPrivateKeyReader.Read(text, Encoding.UTF8.GetBytes("nope")), "wrong --pass");
    }

    [TestMethod]
    [DataRow(TestUserKeys.EcdsaSecp256k1Sec1, DisplayName = "secp256k1")]
    [DataRow("-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n", DisplayName = "another label")]
    [DataRow("PuTTY-User-Key-File-3: ssh-rsa\n", DisplayName = "PuTTY, no PEM block")]
    [DataRow("-----BEGIN RSA PRIVATE KEY-----\nAAAA\n", DisplayName = "never ends")]
    [DataRow("-----BEGIN RSA PRIVATE KEY-----\n!!!!\n-----END RSA PRIVATE KEY-----\n", DisplayName = "not base64")]
    [DataRow("-----BEGIN RSA PRIVATE KEY-----\nMAA=\n-----END RSA PRIVATE KEY-----\n", DisplayName = "not an RSA key")]
    [DataRow("-----BEGIN OPENSSH PRIVATE KEY-----\nAAAA\n-----END OPENSSH PRIVATE KEY-----\n", DisplayName = "no openssh-key-v1 magic")]
    [DataRow("-----BEGIN PRIVATE KEY-----\nAAAA\n-----END PRIVATE KEY-----\n", DisplayName = "not DER")]
    [DataRow("", DisplayName = "empty file")]
    public void Read_KeyThisDoesNotRead_ReadsNone(string text)
    {
        Assert.IsNull(SshPrivateKeyReader.Read(text, Secret));
    }

    [TestMethod]
    [DataRow("aes128-ctr")]
    [DataRow("aes192-ctr")]
    [DataRow("aes256-ctr", DisplayName = "aes256-ctr, ssh-keygen's default")]
    [DataRow("aes128-cbc")]
    [DataRow("aes192-cbc")]
    [DataRow("aes256-cbc")]
    [DataRow("3des-cbc")]
    [DataRow("aes128-gcm@openssh.com")]
    [DataRow("aes256-gcm@openssh.com")]
    public void Read_Ed25519KeyEncryptedWithEachCipher_ReadsTheKeyOfItsPublicKeyFile(string cipher)
    {
        SshPrivateKey? key = SshPrivateKeyReader.Read(TestUserKeys.Ed25519OpenSshEncrypted[cipher], Secret);

        Assert.IsInstanceOfType<Ed25519SshPrivateKey>(key);
        Assert.AreEqual("ssh-ed25519", key.KeyType);
        CollectionAssert.AreEqual(SshPublicKeyFile.Parse(TestUserKeys.Ed25519PublicKeyFile)!.Blob, key.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow("aes256-ctr")]
    [DataRow("aes256-gcm@openssh.com")]
    public void Read_EncryptedOpenSshKeyWithoutOrWithAWrongPassphrase_ReadsNone(string cipher)
    {
        string text = TestUserKeys.Ed25519OpenSshEncrypted[cipher];

        Assert.IsNull(SshPrivateKeyReader.Read(text, []), "no --pass");
        Assert.IsNull(SshPrivateKeyReader.Read(text, Encoding.UTF8.GetBytes("nope")), "wrong --pass");
    }

    [TestMethod]
    public void Read_Ed25519KeyEncryptedWithChaCha20Poly1305_ReadsNoneAsLibssh2Does()
    {
        Assert.IsNull(SshPrivateKeyReader.Read(TestUserKeys.Ed25519OpenSshEncrypted["chacha20-poly1305@openssh.com"], Secret));
    }

    [TestMethod]
    public void Read_Ed25519OpenSsh_ReadsTheKeyOfItsPublicKeyFile()
    {
        SshPrivateKey? key = SshPrivateKeyReader.Read(TestUserKeys.Ed25519OpenSsh, []);

        CollectionAssert.AreEqual(SshPublicKeyFile.Parse(TestUserKeys.Ed25519PublicKeyFile)!.Blob, key!.PublicKeyBlob);
    }

    [TestMethod]
    public void Read_Ed25519Pkcs8_ReadsTheKeyOpenSslDerives()
    {
        SshPrivateKey? key = SshPrivateKeyReader.Read(TestUserKeys.Ed25519Pkcs8, []);

        Assert.IsInstanceOfType<Ed25519SshPrivateKey>(key);
        CollectionAssert.AreEqual(SshPublicKeyFile.Parse("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIGX8BTc94jNCFKn//daYyhhor97hE+LfZyYTJxcsfXpY")!.Blob, key.PublicKeyBlob);
    }

    [TestMethod]
    public void Read_RsaOpenSshEncrypted_ReadsAnRsaKey()
    {
        SshPrivateKey? key = SshPrivateKeyReader.Read(TestUserKeys.RsaOpenSshEncrypted, Encoding.UTF8.GetBytes("enc"));

        Assert.IsInstanceOfType<RsaSshPrivateKey>(key);
        Assert.IsNull(SshPrivateKeyReader.Read(TestUserKeys.RsaOpenSshEncrypted, Secret), "wrong --pass");
    }

    [TestMethod]
    public void Read_EcdsaOpenSshEncryptedWithAes256Gcm_ReadsTheKeyOfItsPublicKeyFile()
    {
        SshPrivateKey? key = SshPrivateKeyReader.Read(TestUserKeys.EcdsaP256OpenSshAes256Gcm, Secret);

        CollectionAssert.AreEqual(SshPublicKeyFile.Parse(TestUserKeys.EcdsaP256PublicKeyFile)!.Blob, key!.PublicKeyBlob);
    }

    [TestMethod]
    public void Read_DsaKeyWithParametersTheSignerRefuses_ReadsNone()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(0);
            for (int field = 0; field < 5; field++)
            {
                writer.WriteInteger(7);
            }
        }

        Assert.IsNull(SshPrivateKeyReader.Read(Pem("DSA PRIVATE KEY", writer.Encode()), []));
    }

    [TestMethod]
    public void Read_CrLfLineEnds_ReadsTheKey()
    {
        string text = TestUserKeys.RsaPkcs1.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.IsNotNull(SshPrivateKeyReader.Read(text, []));
    }

    [TestMethod]
    public void Read_TextBeforeTheBlock_ReadsTheKey()
    {
        Assert.IsNotNull(SshPrivateKeyReader.Read("Bag Attributes\n" + TestUserKeys.EcdsaP256Sec1, []));
    }

    private static string Pem(string label, byte[] body) =>
        $"-----BEGIN {label}-----\n{Convert.ToBase64String(body, Base64FormattingOptions.InsertLineBreaks)}\n-----END {label}-----\n";
}
