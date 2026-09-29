using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="OpenSshPrivateKeyDecoder" /> on bodies written by hand: what it leaves to
/// BL-681 and every malformation it refuses. Files <c>ssh-keygen</c> wrote are pinned by
/// <see cref="SshPrivateKeyReaderTests" />.
/// </summary>
[TestClass]
public sealed class OpenSshPrivateKeyDecoderTests
{
    private static readonly byte[] DsaFields = Join(Name("ssh-dss"), Mpint(TestDsaKey.Prime), Mpint(TestDsaKey.Subprime), Mpint(TestDsaKey.Generator), Mpint(TestDsaKey.PublicKey), Mpint(TestDsaKey.PrivateKey));

    [TestMethod]
    [DataRow("aes256-ctr", "bcrypt", DisplayName = "encrypted, as ssh-keygen writes it")]
    [DataRow("aes256-ctr", "none", DisplayName = "a cipher without a KDF")]
    [DataRow("none", "bcrypt", DisplayName = "a KDF without a cipher")]
    public void Read_EncryptedSection_ReturnsNullForBl681(string cipher, string kdf)
    {
        byte[] body = OpenSshKeyFile.Body(cipher, kdf, 1, TestDsaKey.PublicKeyBlob, OpenSshKeyFile.Section(1, 1, DsaFields));

        Assert.IsNull(OpenSshPrivateKeyDecoder.Read(body));
    }

    [TestMethod]
    [DataRow("ssh-ed25519", DisplayName = "Ed25519: BL-681")]
    [DataRow("sk-ssh-ed25519@openssh.com", DisplayName = "a security key")]
    public void Read_KeyTypeNotReadHere_ReturnsNull(string keyType)
    {
        byte[] body = OpenSshKeyFile.Body([], Join(Name(keyType), String(new byte[32])));

        Assert.IsNull(OpenSshPrivateKeyDecoder.Read(body));
    }

    [TestMethod]
    public void Read_EcdsaOnAnotherCurve_ReturnsNull()
    {
        byte[] body = OpenSshKeyFile.Body([], Join(Name("ecdsa-sha2-nistp256"), Name("nistp192"), String([4]), Mpint([1])));

        Assert.IsNull(OpenSshPrivateKeyDecoder.Read(body));
    }

    [TestMethod]
    public void Read_DsaKey_ReadsIt()
    {
        SshPrivateKey? key = OpenSshPrivateKeyDecoder.Read(OpenSshKeyFile.Body(TestDsaKey.PublicKeyBlob, DsaFields));

        CollectionAssert.AreEqual(TestDsaKey.PublicKeyBlob, key!.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow(0u, DisplayName = "no key")]
    [DataRow(2u, DisplayName = "two keys")]
    public void Read_OtherThanOneKey_Throws(uint keyCount)
    {
        byte[] body = OpenSshKeyFile.Body("none", "none", keyCount, TestDsaKey.PublicKeyBlob, OpenSshKeyFile.Section(1, 1, DsaFields));

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body));
    }

    [TestMethod]
    public void Read_CheckIntegersDiffer_Throws()
    {
        byte[] body = OpenSshKeyFile.Body("none", "none", 1, TestDsaKey.PublicKeyBlob, OpenSshKeyFile.Section(1, 2, DsaFields));

        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read(body));
    }

    [TestMethod]
    public void Read_NoMagic_Throws()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => OpenSshPrivateKeyDecoder.Read([.. "openssh-key-v2\0"u8]));
    }
}
