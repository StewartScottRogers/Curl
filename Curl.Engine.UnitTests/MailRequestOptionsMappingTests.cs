using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Protocols;
using Curl.Cli;
using Curl.Console;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that <see cref="MailRequestOptionsMapping.FromCommandLine" /> gives an HTTP URL the command
/// line's mail options, so a redirect followed to a mail URL still has them (ADR-1378).
/// </summary>
[TestClass]
public sealed class MailRequestOptionsMappingTests
{
    private static readonly CommandLineOptions NoOptions = CommandLineParser.Parse(["http://example.com/"], _ => true).Options!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http")]
    [DataRow("HTTPS")]
    public void FromCommandLine_HttpScheme_CarriesTheMailOptionsForARedirect(string scheme)
    {
        Diagnostics.Arrange("scheme", scheme);

        MailRequestOptions? mail = MailRequestOptionsMapping.FromCommandLine(
            EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions),
            new EasyTransferOptions(),
            new EasySmtpOptions { MailFrom = "a@b", MailRecipients = ["c@d"] },
            new EasyAuthenticationOptions { LoginOptions = "AUTH=PLAIN", XOAuth2Bearer = "tok" },
            scheme);

        Diagnostics.Act("mail options", mail?.ToString() ?? "null");
        Diagnostics.Assert("login options", "AUTH=PLAIN", mail?.LoginOptions);
        Assert.IsNotNull(mail);
        Assert.AreEqual("a@b", mail.From);
        CollectionAssert.AreEqual(new[] { "c@d" }, mail.Recipients.ToArray());
        Assert.AreEqual("AUTH=PLAIN", mail.LoginOptions);
        Assert.AreEqual("tok", mail.BearerToken);
    }
}
