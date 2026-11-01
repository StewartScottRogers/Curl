using System.Globalization;

namespace Curl.Original.Protocol.Smtp;

/// <summary>
/// Pins <see cref="SmtpMailbox" /> under the run-time feature mask hiding IDN (ADR-0746, BL-2983): curl
/// 8.22.0's <c>smtp_parse_address</c> built without <c>USE_IDN</c> sends the host after <c>@</c> as written,
/// so upstream cases 959 to 961 see UTF-8 the SMTP stand-in refuses; without the mask it is an A-label.
/// </summary>
[TestClass]
public sealed class SmtpMailboxFeatureMaskTests
{
    private const string Sender = "sender@åäö.se";

    private static SmtpCommandLineText Utf8 { get; } = SmtpCommandLineText.ForPlatform(isWindows: false, systemAnsiCodePage: null);

    [TestMethod]
    public void Bracketed_HostAsWritten_SendsHostInItsArgvBytes()
    {
        string sent = SmtpMailbox.Bracketed(Sender, Utf8, hostAsWritten: true);

        Assert.AreEqual(Utf8.ToWire("<" + Sender + ">"), sent);
    }

    [TestMethod]
    public void Bracketed_HostNotAsWritten_SendsHostAsALabel()
    {
        string sent = SmtpMailbox.Bracketed(Sender, Utf8, hostAsWritten: false);

        Assert.AreEqual("<sender@" + new IdnMapping().GetAscii("åäö.se") + ">", sent);
    }

    [TestMethod]
    public void Bare_HostAsWritten_SendsHostInItsArgvBytes()
    {
        string sent = SmtpMailbox.Bare(Sender, Utf8, hostAsWritten: true);

        Assert.AreEqual(Utf8.ToWire(Sender), sent);
    }

    [TestMethod]
    public void Bare_HostNotAsWritten_SendsHostAsALabel()
    {
        string sent = SmtpMailbox.Bare(Sender, Utf8, hostAsWritten: false);

        Assert.StartsWith("sender@xn--", sent);
    }
}
