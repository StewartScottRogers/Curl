using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins what curl 8.21.0 adds to an SMTP upload's envelope: <c>AUTH=</c> for
/// <c>--mail-auth</c>, <c>SIZE=</c>, <c>SMTPUTF8</c> and the A-label of a non-ASCII host, and
/// how <c>--mail-rcpt-allowfails</c> carries on past refused recipients. Every case was
/// recorded from real curl (the Schannel build) on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Smtp</c>, curl running
/// <c>-sS --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18025/h</c> with the
/// 21-byte <c>Subject: t\r\n\r\nhello\r\n</c> and the options each test names (BL-544 Notes).
/// curl sent <c>ü</c> as the single byte <c>FC</c>, its Windows argv, which is what the
/// Latin-1 channel sends.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerMailExtensionTests
{
    private const string Url = "smtp://127.0.0.1:18025/h";

    private const string Greeting = "220 localhost ESMTP\r\n";

    /// <summary>The recorder's <c>EHLO</c> reply: <c>AUTH</c>, <c>SIZE</c> and <c>SMTPUTF8</c>.</summary>
    private const string EhloReply =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-STARTTLS\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string Message = "Subject: t\r\n\r\nhello\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Ehlo = "EHLO h\r\n";

    /// <summary>What follows an accepted <c>RCPT</c>: <c>DATA</c>, the message, and <c>QUIT</c>.</summary>
    private const string DataAndQuit = "DATA\r\n" + Message + ".\r\nQUIT\r\n";

    /// <summary>The replies after <c>MAIL</c> for one accepted recipient.</summary>
    private const string OneRecipientAccepted = Ok + Ok + StartData + Ok + Bye;

    private const string Authenticated = "334 \r\n235 Authentication successful\r\n";

    private const string AuthPlain = "AUTH PLAIN\r\nAHUAcA==\r\n";

    [TestMethod]
    [DataRow("x@y", "MAIL FROM:<a@b> AUTH=<x@y> SIZE=21", DisplayName = "--mail-auth x@y")]
    [DataRow("<x@y>", "MAIL FROM:<a@b> AUTH=<x@y> SIZE=21", DisplayName = "--mail-auth <x@y>: brackets taken off")]
    [DataRow("xy", "MAIL FROM:<a@b> AUTH=<xy> SIZE=21", DisplayName = "--mail-auth xy: no @")]
    [DataRow("x@yü.de", "MAIL FROM:<a@b> AUTH=<x@xn--y-eha.de> SIZE=21 SMTPUTF8", DisplayName = "non-ASCII host: A-label and SMTPUTF8")]
    [DataRow("xü@y", "MAIL FROM:<a@b> AUTH=<xü@y> SIZE=21 SMTPUTF8", DisplayName = "non-ASCII local part: SMTPUTF8")]
    [DataRow("", "MAIL FROM:<a@b> AUTH=<> SIZE=21", DisplayName = "empty, which the command line refuses: curl's <>")]
    public async Task ExecuteAsync_MailAuthOnceAuthenticated_AddsAuthToMailFrom(string auth, string mailFrom)
    {
        // -u u:p --mail-auth <auth>: AUTH CRAM-MD5 against the recorder, then MAIL FROM with AUTH=.
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Authenticated + OneRecipientAccepted,
            new MailRequestOptions { From = "a@b", Recipients = ["c@d"], Auth = auth },
            authenticate: true);

        Assert.AreEqual(Ehlo + AuthPlain + mailFrom + "\r\nRCPT TO:<c@d>\r\n" + DataAndQuit, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailAuthWithoutAuthenticating_SendsNoAuth()
    {
        // --mail-auth x@y without -u: MAIL FROM:<a@b> SIZE=21.
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + OneRecipientAccepted, new MailRequestOptions { From = "a@b", Recipients = ["c@d"], Auth = "x@y" });

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b> SIZE=21\r\nRCPT TO:<c@d>\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailAuthWhenAuthIsNotOffered_SendsNoAuth()
    {
        // The authenticator is there, but EHLO offers no AUTH, so nothing authenticates.
        SmtpRun run = await RunAsync(
            Greeting + "250-localhost\r\n250 SIZE 100\r\n" + OneRecipientAccepted,
            new MailRequestOptions { From = "a@b", Recipients = ["c@d"], Auth = "x@y" },
            authenticate: true);

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b> SIZE=21\r\nRCPT TO:<c@d>\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    [DataRow("250-localhost\r\n250 SIZE 1000000\r\n", " SIZE=21", DisplayName = "SIZE advertised")]
    [DataRow("250-localhost\r\n250 size 100\r\n", " SIZE=21", DisplayName = "size in lower case")]
    [DataRow("250-localhost\r\n250 SIZE\r\n", " SIZE=21", DisplayName = "SIZE without a limit")]
    [DataRow("250-localhost\r\n250 SIZEX\r\n", " SIZE=21", DisplayName = "SIZEX: a prefix is enough")]
    [DataRow("250-localhost\r\n250 8BITMIME\r\n", "", DisplayName = "SIZE not advertised")]
    [DataRow("250-localhost\r\n250 SIZ\r\n", "", DisplayName = "SIZ: too short")]
    public async Task ExecuteAsync_FileUpload_AddsSizeWhenAdvertised(string ehloReply, string sizeParameter)
    {
        SmtpRun run = await RunAsync(Greeting + ehloReply + OneRecipientAccepted, Mail());

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b>" + sizeParameter + "\r\nRCPT TO:<c@d>\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFromStandardInput_SendsNoSize()
    {
        // -T - with SIZE advertised: MAIL FROM:<a@b>.
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + OneRecipientAccepted, Mail(), upload: new NonSeekableStream(Encoding.Latin1.GetBytes(Message)));

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyFile_SendsNoSize()
    {
        // -T empty.txt with SIZE advertised: MAIL FROM:<a@b>, then the end-of-data mark alone.
        SmtpRun run = await RunAsync(Greeting + EhloReply + OneRecipientAccepted, Mail(), upload: new MemoryStream());

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nDATA\r\n.\r\nQUIT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_PartReadUpload_SizesWhatIsLeft()
    {
        var upload = new MemoryStream(Encoding.Latin1.GetBytes("skip:" + Message)) { Position = 5 };

        SmtpRun run = await RunAsync(Greeting + EhloReply + OneRecipientAccepted, Mail(), upload: upload);

        StringAssert.StartsWith(run.Sent, Ehlo + "MAIL FROM:<a@b> SIZE=21\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_HeloSession_SendsNoSizeOrSmtpUtf8()
    {
        // EHLO=502 no: HELO h, then MAIL FROM:<aü@b> with nothing added.
        SmtpRun run = await RunAsync(
            Greeting + "502 no\r\n250 localhost\r\n" + OneRecipientAccepted, new MailRequestOptions { From = "aü@b", Recipients = ["c@d"] });

        Assert.AreEqual(Ehlo + "HELO h\r\nMAIL FROM:<aü@b>\r\nRCPT TO:<c@d>\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    [DataRow("aü@b", "c@d", "MAIL FROM:<aü@b> SIZE=21 SMTPUTF8\r\nRCPT TO:<c@d>", DisplayName = "non-ASCII reverse path")]
    [DataRow("a@bü.de", "c@d", "MAIL FROM:<a@xn--b-eha.de> SIZE=21 SMTPUTF8\r\nRCPT TO:<c@d>", DisplayName = "non-ASCII reverse-path host")]
    [DataRow("a@b", "cü@d", "MAIL FROM:<a@b> SIZE=21 SMTPUTF8\r\nRCPT TO:<cü@d>", DisplayName = "non-ASCII recipient")]
    [DataRow("aü@b", "cü@dü.de", "MAIL FROM:<aü@b> SIZE=21 SMTPUTF8\r\nRCPT TO:<cü@xn--d-eha.de>", DisplayName = "both, recipient host an A-label")]
    [DataRow(null, "c@d", "MAIL FROM:<> SIZE=21\r\nRCPT TO:<c@d>", DisplayName = "no --mail-from")]
    public async Task ExecuteAsync_NonAsciiAddressWithSmtpUtf8Advertised_AddsSmtpUtf8(string? from, string recipient, string envelope)
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + OneRecipientAccepted, new MailRequestOptions { From = from, Recipients = [recipient] });

        Assert.AreEqual(Ehlo + envelope + "\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiAddressesWithoutSmtpUtf8Advertised_SendsTheALabelButNoSmtpUtf8()
    {
        // EHLO=250-localhost\r\n250 SIZE 100, --mail-from aü@b --mail-rcpt cü@dü.de.
        SmtpRun run = await RunAsync(
            Greeting + "250-localhost\r\n250 SIZE 100\r\n" + OneRecipientAccepted,
            new MailRequestOptions { From = "aü@b", Recipients = ["cü@dü.de"] });

        Assert.AreEqual(Ehlo + "MAIL FROM:<aü@b> SIZE=21\r\nRCPT TO:<cü@xn--d-eha.de>\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_SmtpUtf8AdvertisedInLowerCase_AddsSmtpUtf8()
    {
        // EHLO=250-AUTH PLAIN\r\n250 smtputf8, --mail-from aü@b: MAIL FROM:<aü@b> SMTPUTF8.
        SmtpRun run = await RunAsync(
            Greeting + "250-AUTH PLAIN\r\n250 smtputf8\r\n" + OneRecipientAccepted, new MailRequestOptions { From = "aü@b", Recipients = ["c@d"] });

        Assert.AreEqual(Ehlo + "MAIL FROM:<aü@b> SMTPUTF8\r\nRCPT TO:<c@d>\r\n" + DataAndQuit, run.Sent);
    }

    [TestMethod]
    [DataRow("550 no\r\n", Ok, DisplayName = "first refused")]
    [DataRow(Ok, "551 no\r\n", DisplayName = "second refused")]
    [DataRow("450 later\r\n", Ok, DisplayName = "first refused for now")]
    public async Task ExecuteAsync_AllowFailsWithOneRecipientAccepted_SendsTheMessage(string firstReply, string secondReply)
    {
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + firstReply + secondReply + StartData + Ok + Bye,
            new MailRequestOptions { From = "a@b", Recipients = ["c@d", "e@f"], RecipientAllowFails = true });

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b> SIZE=21\r\nRCPT TO:<c@d>\r\nRCPT TO:<e@f>\r\n" + DataAndQuit, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(250, run.Result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AllowFailsWithEveryRecipientRefused_IsExit55WithTheLastCodeAndQuits()
    {
        // --mail-rcpt-allowfails, RCPT=550 no then RCPT=551 no: curl: (55) RCPT failed: 551 (last error).
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + "550 no\r\n551 no\r\n" + Bye,
            new MailRequestOptions { From = "a@b", Recipients = ["c@d", "e@f"], RecipientAllowFails = true });

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b> SIZE=21\r\nRCPT TO:<c@d>\r\nRCPT TO:<e@f>\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("RCPT failed: 551 (last error)", run.Result.ErrorMessage);
        Assert.AreEqual(551, run.Result.Report!.ResponseCode);
        Assert.AreEqual(0, run.Result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutAllowFails_FirstRefusedRecipientStopsTheMessage()
    {
        // RCPT=550 no then RCPT=250 OK without --mail-rcpt-allowfails: curl: (55) RCPT failed: 550.
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + "550 no\r\n" + Bye, new MailRequestOptions { From = "a@b", Recipients = ["c@d", "e@f"] });

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b> SIZE=21\r\nRCPT TO:<c@d>\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("RCPT failed: 550", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_AllowFailsThenDataRefused_IsExit55()
    {
        // RCPT=550 no, RCPT=250 OK, DATA=554 no: curl: (55) DATA failed: 554.
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + "550 no\r\n" + Ok + "554 no\r\n" + Bye,
            new MailRequestOptions { From = "a@b", Recipients = ["c@d", "e@f"], RecipientAllowFails = true });

        Assert.AreEqual(Ehlo + "MAIL FROM:<a@b> SIZE=21\r\nRCPT TO:<c@d>\r\nRCPT TO:<e@f>\r\nDATA\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("DATA failed: 554", run.Result.ErrorMessage);
    }

    private static MailRequestOptions Mail() => new() { From = "a@b", Recipients = ["c@d"] };

    private static Task<SmtpRun> RunAsync(string replies, MailRequestOptions mail, bool authenticate = false, Stream? upload = null)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Upload = upload ?? new MemoryStream(Encoding.Latin1.GetBytes(Message)),
            Mail = mail,
            Credentials = authenticate ? new NetworkCredential("u", "p") : null,
        };
        FakeSaslAuthenticator? sasl = authenticate ? new FakeSaslAuthenticator("PLAIN", "\0u\0p"u8.ToArray()) : null;
        return SmtpRun.ExecuteAsync(context, new ScriptedConnection(Encoding.Latin1.GetBytes(replies)), sasl);
    }
}
