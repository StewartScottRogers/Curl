using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="SshPublicKeyFile" /> against libssh2 1.11.1's <c>file_read_publickey</c>,
/// its error messages included (BL-1043).
/// </summary>
[TestClass]
public sealed class SshPublicKeyFileTests
{
    [TestMethod]
    public void Parse_OpenSshPublicKeyFile_ReadsTheTypeAndBlob()
    {
        SshPublicKeyReading reading = SshPublicKeyFile.Parse(TestUserKeys.EcdsaP256PublicKeyFile);

        Assert.AreEqual("ecdsa-sha2-nistp256", reading.Key!.KeyType);
        CollectionAssert.AreEqual(SshPrivateKeyReader.Read(TestUserKeys.EcdsaP256Sec1, [])!.PublicKeyBlob, reading.Key.Blob);
        Assert.IsNull(reading.DenialReason);
    }

    [TestMethod]
    [DataRow("ssh-rsa AAAAB3Nz", DisplayName = "no comment")]
    [DataRow("ssh-rsa AAAAB3Nz a comment\r\nsecond line", DisplayName = "comment, CR LF and a second line")]
    [DataRow("ssh-rsa AAAAB3Nz \t\v\f \n", DisplayName = "trailing white space")]
    [DataRow("ssh-rsa AAAA=B3Nz", DisplayName = "characters outside the alphabet skipped")]
    public void Parse_Variants_ReadsTheFirstLinesKey(string text)
    {
        SshPublicKey? key = SshPublicKeyFile.Parse(text).Key;

        Assert.AreEqual("ssh-rsa", key!.KeyType);
        CollectionAssert.AreEqual(Name("ssh-rsa")[..6], key.Blob);
    }

    [TestMethod]
    public void Parse_TypeTakenAsWritten_EvenWhenTheBlobSaysOtherwise()
    {
        Assert.AreEqual("anything", SshPublicKeyFile.Parse("anything AAAAB3Nz").Key!.KeyType);
    }

    [TestMethod]
    [DataRow("ssh-rsa  comment", DisplayName = "two spaces")]
    [DataRow("ssh-rsa === comment", DisplayName = "padding only")]
    public void Parse_NoBase64Characters_ReadsAnEmptyBlobAsLibssh2Does(string text)
    {
        SshPublicKey? key = SshPublicKeyFile.Parse(text).Key;

        Assert.AreEqual("ssh-rsa", key!.KeyType);
        Assert.IsEmpty(key.Blob);
    }

    [TestMethod]
    [DataRow("", "Invalid data in public key file", DisplayName = "empty")]
    [DataRow("x", "Invalid data in public key file", DisplayName = "one character")]
    [DataRow("\nssh-rsa AAAAB3Nz", "Invalid data in public key file", DisplayName = "empty first line")]
    [DataRow("     ", "Missing public key data", DisplayName = "blank")]
    [DataRow("  ", "Invalid public key data", DisplayName = "no-break spaces are not C white space")]
    [DataRow("ssh-rsa", "Invalid public key data", DisplayName = "no space")]
    [DataRow("ssh-rsa ", "Invalid public key data", DisplayName = "no data after the space")]
    [DataRow("ssh-rsa AAAAB", "Invalid key data, not base64 encoded", DisplayName = "a lone leftover base64 character")]
    public void Parse_NotAPublicKeyLine_GivesFileReadPublicKeysReason(string text, string reason)
    {
        SshPublicKeyReading reading = SshPublicKeyFile.Parse(text);

        Assert.IsNull(reading.Key);
        Assert.AreEqual(reason, reading.DenialReason);
    }
}
