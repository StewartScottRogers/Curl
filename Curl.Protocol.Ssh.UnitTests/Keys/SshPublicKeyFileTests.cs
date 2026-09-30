using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="SshPublicKeyFile" /> against libssh2 1.11.1's <c>file_read_publickey</c>.
/// </summary>
[TestClass]
public sealed class SshPublicKeyFileTests
{
    [TestMethod]
    public void Parse_OpenSshPublicKeyFile_ReadsTheTypeAndBlob()
    {
        SshPublicKey? key = SshPublicKeyFile.Parse(TestUserKeys.EcdsaP256PublicKeyFile);

        Assert.AreEqual("ecdsa-sha2-nistp256", key!.KeyType);
        CollectionAssert.AreEqual(SshPrivateKeyReader.Read(TestUserKeys.EcdsaP256Sec1, [])!.PublicKeyBlob, key.Blob);
    }

    [TestMethod]
    [DataRow("ssh-rsa AAAAB3Nz", DisplayName = "no comment")]
    [DataRow("ssh-rsa AAAAB3Nz a comment\r\nsecond line", DisplayName = "comment, CR LF and a second line")]
    [DataRow("ssh-rsa AAAAB3Nz   \n", DisplayName = "trailing white space")]
    [DataRow("ssh-rsa AAAA=B3Nz", DisplayName = "characters outside the alphabet skipped")]
    public void Parse_Variants_ReadsTheFirstLinesKey(string text)
    {
        SshPublicKey? key = SshPublicKeyFile.Parse(text);

        Assert.AreEqual("ssh-rsa", key!.KeyType);
        CollectionAssert.AreEqual(Name("ssh-rsa")[..6], key.Blob);
    }

    [TestMethod]
    public void Parse_TypeTakenAsWritten_EvenWhenTheBlobSaysOtherwise()
    {
        Assert.AreEqual("anything", SshPublicKeyFile.Parse("anything AAAAB3Nz")!.KeyType);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty")]
    [DataRow("x", DisplayName = "one character")]
    [DataRow("\nssh-rsa AAAAB3Nz", DisplayName = "empty first line")]
    [DataRow("     ", DisplayName = "blank")]
    [DataRow("ssh-rsa", DisplayName = "no space")]
    [DataRow("ssh-rsa ", DisplayName = "no data after the space")]
    [DataRow("ssh-rsa AAAAB", DisplayName = "a lone leftover base64 character")]
    public void Parse_NotAPublicKeyLine_ReturnsNull(string text)
    {
        Assert.IsNull(SshPublicKeyFile.Parse(text));
    }
}
