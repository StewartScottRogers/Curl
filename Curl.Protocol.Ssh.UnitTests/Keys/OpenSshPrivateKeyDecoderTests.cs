using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="OpenSshPrivateKeyDecoder" /> on bodies written by hand and on the
/// encrypted files <c>ssh-keygen</c> wrote: what it leaves unread, every malformation it
/// refuses, and how a wrong passphrase shows. Reading every file <c>ssh-keygen</c> wrote is
/// pinned by <see cref="SshPrivateKeyReaderTests" />.
/// </summary>
[TestClass]
public sealed class OpenSshPrivateKeyDecoderTests
{
    private static readonly byte[] DsaFields = Join(Name("ssh-dss"), Mpint(TestDsaKey.Prime), Mpint(TestDsaKey.Subprime), Mpint(TestDsaKey.Generator), Mpint(TestDsaKey.PublicKey), Mpint(TestDsaKey.PrivateKey));

    private static readonly byte[] Secret = Encoding.UTF8.GetBytes(TestUserKeys.Passphrase);

    private static readonly byte[] Salt = new byte[16];

    [TestMethod]
    [DataRow("aes256-ctr", "none", DisplayName = "a cipher without a KDF")]
    [DataRow("none", "bcrypt", DisplayName = "a KDF without a cipher")]
    [DataRow("aes256-ctr", "scrypt", DisplayName = "another KDF")]
    [DataRow("chacha20-poly1305@openssh.com", "bcrypt", DisplayName = "a cipher libssh2 does not read keys with")]
    public void Read_CipherAndKdfThisDoesNotRead_ReturnsNull(string cipher, string kdf)
    {
        byte[] body = OpenSshKeyFile.Body(cipher, kdf, 1, TestDsaKey.PublicKeyBlob, OpenSshKeyFile.Section(1, 1, DsaFields));

        Assert.IsNull(OpenSshPrivateKeyDecoder.Read(body, Secret));
    }

    [TestMethod]
    public void Read_SecurityKey_ReturnsNull()
    {
        byte[] body = OpenSshKeyFile.Body([], Join(Name("sk-ssh-ed25519@openssh.com"), String(new byte[32])));

        Assert.IsNull(OpenSshPrivateKeyDecoder.Read(body, []));
    }

    [TestMethod]
    public void Read_EcdsaOnAnotherCurve_ReturnsNull()
    {
        byte[] body = OpenSshKeyFile.Body([], Join(Name("ecdsa-sha2-nistp256"), Name("nistp192"), String([4]), Mpint([1])));

        Assert.IsNull(OpenSshPrivateKeyDecoder.Read(body, []));
    }

    [TestMethod]
    public void Read_DsaKey_ReadsIt()
    {
        SshPrivateKey? key = OpenSshPrivateKeyDecoder.Read(OpenSshKeyFile.Body(TestDsaKey.PublicKeyBlob, DsaFields), []);

        CollectionAssert.AreEqual(TestDsaKey.PublicKeyBlob, key!.PublicKeyBlob);
    }

    [TestMethod]
    public void Read_Ed25519Key_ReadsItsTwoFields()
    {
        byte[] seed = Convert.FromHexString("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");
        byte[] publicKey = Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");
        byte[] blob = Join(Name("ssh-ed25519"), String(publicKey));

        SshPrivateKey? key = OpenSshPrivateKeyDecoder.Read(OpenSshKeyFile.Body(blob, Join(Name("ssh-ed25519"), String(publicKey), String([.. seed, .. publicKey]))), []);

        Assert.IsInstanceOfType<Ed25519SshPrivateKey>(key);
        CollectionAssert.AreEqual(blob, key.PublicKeyBlob);
    }

    [TestMethod]
    public void Read_Ed25519KeyWithAShortPrivateKey_Throws()
    {
        byte[] body = OpenSshKeyFile.Body([], Join(Name("ssh-ed25519"), String(new byte[32]), String(new byte[32])));

        Assert.ThrowsExactly<CryptographicException>(() => OpenSshPrivateKeyDecoder.Read(body, []));
    }

    [TestMethod]
    [DataRow(0u, DisplayName = "no key")]
    [DataRow(2u, DisplayName = "two keys")]
    public void Read_OtherThanOneKey_Throws(uint keyCount)
    {
        byte[] body = OpenSshKeyFile.Body("none", "none", keyCount, TestDsaKey.PublicKeyBlob, OpenSshKeyFile.Section(1, 1, DsaFields));

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body, []));
    }

    [TestMethod]
    public void Read_CheckIntegersDiffer_Throws()
    {
        byte[] body = OpenSshKeyFile.Body("none", "none", 1, TestDsaKey.PublicKeyBlob, OpenSshKeyFile.Section(1, 2, DsaFields));

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body, []));
    }

    [TestMethod]
    public void Read_NoMagic_Throws()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read([.. "openssh-key-v2\0"u8], []));
    }

    [TestMethod]
    [DataRow("aes256-ctr", 15, DisplayName = "AES: not a multiple of 16")]
    [DataRow("aes128-cbc", 8, DisplayName = "AES-CBC: half a block")]
    [DataRow("3des-cbc", 12, DisplayName = "3DES: not a multiple of 8")]
    public void Read_EncryptedSectionNotWholeBlocks_Throws(string cipher, int length)
    {
        byte[] body = OpenSshKeyFile.Encrypted(cipher, Salt, 16, new byte[length], []);

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body, Secret));
    }

    [TestMethod]
    [DataRow(0u, DisplayName = "zero")]
    [DataRow(0x8000_0000u, DisplayName = "above int.MaxValue")]
    public void Read_BcryptRoundsOutOfRange_Throws(uint rounds)
    {
        byte[] body = OpenSshKeyFile.Encrypted("aes256-ctr", Salt, rounds, new byte[16], []);

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body, Secret));
    }

    [TestMethod]
    public void Read_EncryptedWithoutAPassphrase_ThrowsAsBcryptPbkdfRefusesIt()
    {
        byte[] body = OpenSshKeyFile.Encrypted("aes256-ctr", Salt, 1, new byte[16], []);

        Assert.ThrowsExactly<ArgumentException>(() => OpenSshPrivateKeyDecoder.Read(body, []));
    }

    [TestMethod]
    public void Read_GcmWithoutItsTag_Throws()
    {
        byte[] body = OpenSshKeyFile.Encrypted("aes256-gcm@openssh.com", Salt, 1, new byte[16], new byte[15]);

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body, Secret));
    }

    [TestMethod]
    [DataRow("aes256-ctr", DisplayName = "CTR")]
    [DataRow("aes256-cbc", DisplayName = "CBC")]
    [DataRow("3des-cbc", DisplayName = "3DES")]
    public void Read_WrongPassphrase_ThrowsAsTheCheckIntegersDiffer(string cipher)
    {
        byte[] body = PemBlock.Find(TestUserKeys.Ed25519OpenSshEncrypted[cipher])!.Body;

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body, "nope"u8.ToArray()));
    }

    [TestMethod]
    public void Read_GcmWrongPassphrase_ThrowsAsTheTagFails()
    {
        byte[] body = PemBlock.Find(TestUserKeys.Ed25519OpenSshEncrypted["aes256-gcm@openssh.com"])!.Body;

        Assert.Throws<CryptographicException>(() => OpenSshPrivateKeyDecoder.Read(body, "nope"u8.ToArray()));
    }
}
