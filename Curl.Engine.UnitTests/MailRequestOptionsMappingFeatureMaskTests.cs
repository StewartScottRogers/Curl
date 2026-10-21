using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Protocols;
using Curl.Cli;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="MailRequestOptionsMapping.FromCommandLine" /> under the run-time feature mask (ADR-0746, BL-2983):
/// hiding IDN tells the SMTP handler to send a mailbox's host as written, and no mask leaves it converted.
/// </summary>
[TestClass]
public sealed class MailRequestOptionsMappingFeatureMaskTests
{
    private static readonly CommandLineOptions NoOptions = CommandLineParser.Parse(["smtp://example.com/"], _ => true).Options!;

    [TestMethod]
    public void FromCommandLine_UnderMaskHidingIdn_SendsMailboxHostAsWritten()
    {
        MailRequestOptions mail = MapSmtp(EngineFeatureMask.Idn);

        Assert.IsTrue(mail.SendsMailboxHostAsWritten);
    }

    [TestMethod]
    public void FromCommandLine_WithoutMask_ConvertsMailboxHost()
    {
        MailRequestOptions mail = MapSmtp(EngineFeatureMask.None);

        Assert.IsFalse(mail.SendsMailboxHostAsWritten);
    }

    [TestMethod]
    public void FromCommandLine_UnderMaskHidingOnlyProxy_ConvertsMailboxHost()
    {
        MailRequestOptions mail = MapSmtp(EngineFeatureMask.Proxy);

        Assert.IsFalse(mail.SendsMailboxHostAsWritten);
    }

    private static MailRequestOptions MapSmtp(EngineFeatureMask featureMask) =>
        MailRequestOptionsMapping.FromCommandLine(
            EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions),
            new EasyTransferOptions(),
            new EasySmtpOptions { MailFrom = "sender@åäö.se" },
            new EasyAuthenticationOptions(),
            "smtp",
            featureMask)!;
}
