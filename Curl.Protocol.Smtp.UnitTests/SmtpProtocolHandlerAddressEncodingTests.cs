using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins the bytes of a non-ASCII address in <c>MAIL FROM</c>, <c>RCPT TO</c>, <c>VRFY</c> and
/// the <c>-X</c> command (BL-776): curl sends its argv bytes, the ANSI code page with best fit
/// on Windows and UTF-8 on Linux and macOS, with the host converted to an IDNA A-label. The
/// Windows cases were recorded from real curl 8.21.0 (the Schannel build, code page 1252) on
/// 2026-09-29 with <c>Record-CurlExchange.ps1 -Smtp</c> and the 18-byte
/// <c>Subject: x\r\n\r\nhi\r\n</c>; the UTF-8 cases follow curl's source, which sends the argv
/// bytes as given (BL-776 Notes).
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerAddressEncodingTests
{
    private const string Url = "smtp://127.0.0.1:18776/h";

    private const string Greeting = "220 localhost ESMTP\r\n";

    /// <summary>The recorder's <c>EHLO</c> reply, which advertises <c>SIZE</c> and <c>SMTPUTF8</c>.</summary>
    private const string EhloWithSmtpUtf8 =
        "250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5\r\n250-STARTTLS\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8\r\n";

    private const string EhloWithoutSmtpUtf8 = "250-localhost\r\n250 SIZE 1000000\r\n";

    private const string Message = "Subject: x\r\n\r\nhi\r\n";

    private const string MessageAccepted = "250 OK\r\n250 OK\r\n354 go\r\n250 OK\r\n221 Bye\r\n";

    private const string Tail = "DATA\r\n" + Message + ".\r\nQUIT\r\n";

    private static SmtpCommandLineText Utf8 { get; } = SmtpCommandLineText.ForPlatform(isWindows: false, systemAnsiCodePage: null);

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiAddressesOnWindows_SendAnsiLocalPartAndALabelHostWithSmtpUtf8()
    {
        // Measured: --mail-from jörg@bücher.example --mail-rcpt a@bücher.example -T mail.txt.
        SmtpRun run = await SendAsync(EhloWithSmtpUtf8, "jörg@bücher.example", "a@bücher.example", SmtpRun.Windows1252);

        AssertSent(
            "EHLO h\r\nMAIL FROM:<jörg@xn--bcher-kva.example> SIZE=18 SMTPUTF8\r\nRCPT TO:<a@xn--bcher-kva.example>\r\n" + Tail,
            run);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiAddressesOnLinuxAndMacOs_SendUtf8LocalPartAndALabelHostWithSmtpUtf8()
    {
        SmtpRun run = await SendAsync(EhloWithSmtpUtf8, "jörg@bücher.example", "ö@bücher.example", Utf8);

        AssertSent(
            "EHLO h\r\nMAIL FROM:<jÃ¶rg@xn--bcher-kva.example> SIZE=18 SMTPUTF8\r\nRCPT TO:<Ã¶@xn--bcher-kva.example>\r\n" + Tail,
            run);
    }

    [TestMethod]
    [DataRow(true, "jörg", DisplayName = "Windows: ö is F6")]
    [DataRow(false, "jÃ¶rg", DisplayName = "Linux and macOS: ö is C3 B6")]
    public async Task ExecuteAsync_NonAsciiAddressWithoutSmtpUtf8Advertised_SendsTheSameBytesWithoutSmtpUtf8(bool windows, string localPart)
    {
        SmtpRun run = await SendAsync(EhloWithoutSmtpUtf8, "jörg@bücher.example", "a@b", windows ? SmtpRun.Windows1252 : Utf8);

        AssertSent("EHLO h\r\nMAIL FROM:<" + localPart + "@xn--bcher-kva.example> SIZE=18\r\nRCPT TO:<a@b>\r\n" + Tail, run);
    }

    [TestMethod]
    public async Task ExecuteAsync_CharacterWindowsBestFitsToAscii_SendsItAsAsciiWithoutSmtpUtf8()
    {
        // Measured: --mail-from łx@ł.example --mail-rcpt a@b went out as MAIL FROM:<lx@l.example> SIZE=18.
        SmtpRun run = await SendAsync(EhloWithSmtpUtf8, "łx@ł.example", "a@b", SmtpRun.Windows1252);

        AssertSent("EHLO h\r\nMAIL FROM:<lx@l.example> SIZE=18\r\nRCPT TO:<a@b>\r\n" + Tail, run);
    }

    [TestMethod]
    public async Task ExecuteAsync_CharacterOnlyInWindows1252_SendsItsCodePageByte()
    {
        // Measured: --mail-from €łx@b.example went out as <80 6C 78 @b.example> SMTPUTF8.
        SmtpRun run = await SendAsync(EhloWithSmtpUtf8, "€łx@b.example", "a@b", SmtpRun.Windows1252);

        AssertSent("EHLO h\r\nMAIL FROM:<\u0080lx@b.example> SIZE=18 SMTPUTF8\r\nRCPT TO:<a@b>\r\n" + Tail, run);
    }

    [TestMethod]
    [DataRow(true, "VRFY l\u0080@xn--bcher-kva.example SMTPUTF8", DisplayName = "Windows (measured): ł best-fitted, € is 80")]
    [DataRow(false, "VRFY Å\u0082â\u0082¬@xn--bcher-kva.example SMTPUTF8", DisplayName = "Linux and macOS: UTF-8")]
    public async Task ExecuteAsync_VrfyOfNonAsciiRecipient_SendsItsArgvBytes(bool windows, string expected)
    {
        SmtpRun run = await RunAsync(
            EhloWithSmtpUtf8 + "250 ok\r\n221 Bye\r\n",
            new MailRequestOptions { Recipients = ["ł€@bücher.example"] },
            upload: null,
            windows ? SmtpRun.Windows1252 : Utf8);

        AssertSent("EHLO h\r\n" + expected + "\r\nQUIT\r\n", run);
    }

    [TestMethod]
    [DataRow(true, "EXPN jörg SMTPUTF8", DisplayName = "Windows (measured): ö is F6")]
    [DataRow(false, "EXPN jÃ¶rg SMTPUTF8", DisplayName = "Linux and macOS: ö is C3 B6")]
    public async Task ExecuteAsync_CustomCommandWithNonAsciiRecipient_SendsItsArgvBytes(bool windows, string expected)
    {
        // Measured: --mail-rcpt jörg -X EXPN.
        SmtpRun run = await RunAsync(
            EhloWithSmtpUtf8 + "250 ok\r\n221 Bye\r\n",
            new MailRequestOptions { Recipients = ["jörg"], CustomCommand = "EXPN" },
            upload: null,
            windows ? SmtpRun.Windows1252 : Utf8);

        AssertSent("EHLO h\r\n" + expected + "\r\nQUIT\r\n", run);
    }

    [TestMethod]
    [DataRow(true, true, "ö", DisplayName = "Windows with its ANSI code page")]
    [DataRow(true, false, "ö", DisplayName = "Windows behaviour on a host with no ANSI code page: Windows-1252")]
    [DataRow(false, true, "Ã¶", DisplayName = "Linux and macOS: UTF-8")]
    public void ForPlatform_ChoosesThePlatformsArgvEncoding(bool windows, bool hostHasAnsiCodePage, string expected)
    {
        Encoding? ansi = hostHasAnsiCodePage ? CodePagesEncodingProvider.Instance.GetEncoding(1252) : null;

        Assert.AreEqual(expected, SmtpCommandLineText.ForPlatform(windows, ansi).ToWire("ö"));
    }

    [TestMethod]
    public void Platform_IsTheHostsArgvEncoding()
    {
        Encoding? ansi = SmtpCommandLineText.ReadSystemAnsiCodePage(() => CodePagesEncodingProvider.Instance.GetEncoding(0));
        string expected = SmtpCommandLineText.ForPlatform(OperatingSystem.IsWindows(), ansi).ToWire("ö");

        Assert.AreEqual(expected, SmtpCommandLineText.Platform.ToWire("ö"));
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstReadAnswersACodePage_ReadsOnce()
    {
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        int reads = 0;

        Encoding? encoding = SmtpCommandLineText.ReadSystemAnsiCodePage(() => { reads++; return shiftJis; });

        Assert.AreEqual(932, encoding!.CodePage);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstReadFails_ReadsAgain()
    {
        Encoding shiftJis = CodePagesEncodingProvider.Instance.GetEncoding(932)!;
        int reads = 0;

        Encoding? encoding = SmtpCommandLineText.ReadSystemAnsiCodePage(() => ++reads == 1 ? null : shiftJis);

        Assert.AreEqual(932, encoding!.CodePage);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_HostHasNone_IsNullAfterTwoReads()
    {
        int reads = 0;

        Encoding? encoding = SmtpCommandLineText.ReadSystemAnsiCodePage(() => { reads++; return null; });

        Assert.IsNull(encoding);
        Assert.AreEqual(2, reads);
    }

    [TestMethod]
    public void Constructor_NullCommandLineText_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => new SmtpProtocolHandler(new QueuedConnector(), new QueuedTlsProvider(), null, () => "h", null!));
    }

    private static void AssertSent(string expected, SmtpRun run) =>
        Assert.AreEqual(expected, run.Sent, "sent bytes: " + Convert.ToHexString(run.Connection.Sent));

    private static Task<SmtpRun> SendAsync(string ehlo, string from, string recipient, SmtpCommandLineText commandLineText) =>
        RunAsync(
            ehlo + MessageAccepted,
            new MailRequestOptions { From = from, Recipients = [recipient] },
            new MemoryStream(Encoding.ASCII.GetBytes(Message)),
            commandLineText);

    private static Task<SmtpRun> RunAsync(string replies, MailRequestOptions mail, Stream? upload, SmtpCommandLineText commandLineText)
    {
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = Stream.Null, Upload = upload, Mail = mail };
        return SmtpRun.ExecuteAsync(
            context, new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + replies)), saslAuthenticator: null, commandLineText);
    }
}
