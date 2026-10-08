using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Keys;

/// <summary>
/// Pins <see cref="SshPublicKeyFile" /> against libssh2 1.11.1's <c>file_read_publickey</c>,
/// its error messages included (BL-1043).
/// </summary>
[TestClass]
public sealed class SshPublicKeyFileTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_OpenSshPublicKeyFile_ReadsTheTypeAndBlob()
    {
        Diagnostics.ArrangeText("text", TestUserKeys.EcdsaP256PublicKeyFile);

        SshPublicKeyReading reading = SshPublicKeyFile.Parse(TestUserKeys.EcdsaP256PublicKeyFile);

        Diagnostics.ActReading(reading);
        Diagnostics.Assert("key type", "ecdsa-sha2-nistp256", reading.Key?.KeyType);
        Diagnostics.AssertBytes("public key blob", SshPrivateKeyReader.Read(TestUserKeys.EcdsaP256Sec1, [])!.PublicKeyBlob, reading.Key?.Blob);
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
        Diagnostics.ArrangeText("text", text);

        SshPublicKey? key = SshPublicKeyFile.Parse(text).Key;

        Diagnostics.ActPublicKey(key);
        Diagnostics.Assert("key type", "ssh-rsa", key?.KeyType);
        Diagnostics.AssertBytes("public key blob", Name("ssh-rsa")[..6], key?.Blob);
        Assert.AreEqual("ssh-rsa", key!.KeyType);
        CollectionAssert.AreEqual(Name("ssh-rsa")[..6], key.Blob);
    }

    [TestMethod]
    public void Parse_TypeTakenAsWritten_EvenWhenTheBlobSaysOtherwise()
    {
        Diagnostics.ArrangeText("text", "anything AAAAB3Nz");

        SshPublicKey? key = SshPublicKeyFile.Parse("anything AAAAB3Nz").Key;

        Diagnostics.ActPublicKey(key);
        Diagnostics.Assert("key type", "anything", key?.KeyType);
        Assert.AreEqual("anything", key!.KeyType);
    }

    [TestMethod]
    [DataRow("ssh-rsa  comment", DisplayName = "two spaces")]
    [DataRow("ssh-rsa === comment", DisplayName = "padding only")]
    public void Parse_NoBase64Characters_ReadsAnEmptyBlobAsLibssh2Does(string text)
    {
        Diagnostics.ArrangeText("text", text);

        SshPublicKey? key = SshPublicKeyFile.Parse(text).Key;

        Diagnostics.ActPublicKey(key);
        Diagnostics.Assert("blob length", 0, key?.Blob.Length);
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
        Diagnostics.ArrangeText("text", text);

        SshPublicKeyReading reading = SshPublicKeyFile.Parse(text);

        Diagnostics.ActReading(reading);
        Diagnostics.Diff("denial reason", reason, reading.DenialReason ?? string.Empty);
        Assert.IsNull(reading.Key);
        Assert.AreEqual(reason, reading.DenialReason);
    }
}
