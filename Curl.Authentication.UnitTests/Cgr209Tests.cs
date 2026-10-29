using System.Text;
using Curl.Authentication;

namespace Curl.Authentication.UnitTests;

[TestClass]
public sealed class Cgr209Tests
{
    [TestMethod]
    public void ReadAsSspiDoes_AnsiUmlautReadThroughOem437_IsBlockCharacter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string read = NtlmUserNameCodePages.ReadAsSspiDoes("ÜSER", Encoding.GetEncoding(1252), Encoding.GetEncoding(437));

        Assert.AreEqual("\u2584SER", read);
    }

    [TestMethod]
    public void OptionsFor_NtlmAsciiUser_KeepsTheName() =>
        Assert.AreEqual("user", SystemSecurityContextFactory.OptionsFor(
            new Curl.Protocol.Abstractions.SecurityContextRequest(Curl.Protocol.Abstractions.SecurityMechanism.Ntlm, "HTTP", "h") { UserName = "user", Password = "p" }).Credential.GetCredential(new Uri("http://h"), "NTLM")!.UserName);

    [TestMethod]
    public void ReadAsSspiDoes_AsciiName_IsUnchanged() =>
        Assert.AreEqual("user", NtlmUserNameCodePages.ReadAsSspiDoes("user"));

    [TestMethod]
    public void ReadAsSspiDoes_ThisMachine_ReturnsAName() =>
        Assert.IsFalse(string.IsNullOrEmpty(NtlmUserNameCodePages.ReadAsSspiDoes("\u00dcSER")));
}
