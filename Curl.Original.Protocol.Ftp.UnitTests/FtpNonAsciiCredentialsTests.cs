using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins the bytes <c>USER</c> and <c>PASS</c> carry for a non-ASCII user name or password
/// (BL-3855). Measured with <c>Record-CurlExchange.ps1 -Ftp</c> and curl 8.22.0 (Schannel) on
/// Windows: <c>ftp://u%C3%A9:p%E2%82%AC%FF@127.0.0.1/f.txt</c> sends <c>USER u C3 A9</c> and
/// <c>PASS p E2 82 AC FF</c>, the percent-decoded bytes, and <c>-u ué:p€</c> sends
/// <c>USER u E9</c> and <c>PASS p 80</c>, the ANSI code page's bytes. The engine holds each
/// credential so that the platform's credential encoding gives those bytes back (ADR-0022): the
/// ANSI code page on Windows, and UTF-8 keeping every byte elsewhere, where the OpenSSL build
/// sends the percent-decoded and argument bytes as they are.
/// </summary>
[TestClass]
public sealed class FtpNonAsciiCredentialsTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:41855/f.txt";

    private const string Replies = "220 Recorder ready\r\n331 Password required\r\n530 Login incorrect\r\n";

    private const string ExpectedSent = "USER uÃ©\r\nPASS pâ\u0082¬ÿ\r\n";

    private static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_PercentEncodedUtf8CredentialsOnWindows_SendsTheDecodedBytes()
    {
        Encoding ansi = CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Windows1252;

        // The engine holds a percent-decoded URL credential as the ANSI code page reads its bytes.
        await AssertSentAsync(
            ansi.GetString([0x75, 0xC3, 0xA9]),
            ansi.GetString([0x70, 0xE2, 0x82, 0xAC, 0xFF]),
            ExpectedSent);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_PercentEncodedUtf8CredentialsOffWindows_SendsTheDecodedBytes() =>
        // The engine holds a percent-decoded URL credential as UTF-8, an invalid byte as U+DC00 + byte.
        await AssertSentAsync("ué", "p€\uDCFF", ExpectedSent);

    [TestMethod]
    public void CommandTextOfCredential_OnWindows_IsTheAnsiCodePageBytes()
    {
        Assert.AreEqual("ué", FtpSession.CommandTextOfCredential("ué", isWindows: true, Windows1252));
        Assert.AreEqual("p\u0080", FtpSession.CommandTextOfCredential("p€", isWindows: true, Windows1252));
        Assert.AreEqual("uÃ©", FtpSession.CommandTextOfCredential("uÃ©", isWindows: true, Windows1252));
    }

    [TestMethod]
    public void CommandTextOfCredential_OnWindowsWithNoAnsiCodePage_IsWindows1252Bytes() =>
        Assert.AreEqual("p\u0080", FtpSession.CommandTextOfCredential("p€", isWindows: true, systemAnsiCodePage: null));

    [TestMethod]
    [DataRow("ué", "uÃ©", DisplayName = "é is two UTF-8 bytes")]
    [DataRow("😀", "ð\u009F\u0098\u0080", DisplayName = "a surrogate pair is four UTF-8 bytes")]
    [DataRow("anonymous", "anonymous", DisplayName = "ASCII is unchanged")]
    public void CommandTextOfCredential_OffWindows_IsUtf8KeepingEscapedBytes(string credential, string expected) =>
        Assert.AreEqual(expected, FtpSession.CommandTextOfCredential(credential, isWindows: false, systemAnsiCodePage: null));

    // A lone surrogate cannot sit in an attribute argument: metadata holds its strings as UTF-8.
    [TestMethod]
    public void CommandTextOfCredential_OffWindowsEscapingSurrogate_IsItsByte() =>
        Assert.AreEqual("p\u00E2\u0082\u00AC\u00FF", FtpSession.CommandTextOfCredential("p\u20AC\uDCFF", isWindows: false, systemAnsiCodePage: null));

    [TestMethod]
    public void CommandTextOfCredential_OffWindowsLoneHighSurrogate_IsReplacementCharacter() =>
        Assert.AreEqual("a\u00EF\u00BF\u00BDb", FtpSession.CommandTextOfCredential("a\uD800b", isWindows: false, systemAnsiCodePage: null));

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerNull_AsksAgain()
    {
        var answers = new Queue<Encoding?>([null, Windows1252]);

        Assert.AreSame(Windows1252, FtpSession.ReadSystemAnsiCodePage(answers.Dequeue));
    }

    [TestMethod]
    public void ReadSystemAnsiCodePage_FirstAnswerGiven_AsksOnce()
    {
        var answers = new Queue<Encoding?>([Windows1252]);

        Assert.AreSame(Windows1252, FtpSession.ReadSystemAnsiCodePage(answers.Dequeue));
    }

    private async Task AssertSentAsync(string user, string password, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Replies,
            adjust: c => new TransferContext { Url = c.Url, Output = c.Output, Credentials = new NetworkCredential(user, password) });
        diagnostics.ActRun(run);

        diagnostics.DiffSent(expected, run.Sent);
        Assert.AreEqual(expected, run.Sent);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
    }
}
