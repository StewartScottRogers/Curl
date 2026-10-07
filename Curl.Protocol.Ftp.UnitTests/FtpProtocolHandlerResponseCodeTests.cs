using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <see cref="TransferReport.ResponseCode" /> on <c>ftp://</c> against curl 8.21.0's
/// <c>%{response_code}</c>: the code of the last reply read before <c>QUIT</c>, whose own
/// reply is never reported. Each case was recorded on 2026-09-27 with
/// <c>Record-CurlExchange.ps1 -Ftp -CurlArgs -s,-o,NUL,-w,%{response_code},ftp://127.0.0.1:port/f.txt</c>
/// and the named <c>-FtpReply</c> override (BL-392).
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerResponseCodeTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18321/f.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Sized = LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 5\r\n";

    private const string Opening = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsTheTransferCompleteCode226NotQuits221()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // curl -w %{response_code}: 226, exit 0.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Sized + Opening + Complete + Bye, "hello");
        diagnostics.ActRun(run);

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        diagnostics.Assert("response code", 226, run.Report?.ResponseCode);
        Assert.AreEqual(226, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pass430_Reports430()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // -FtpReply 'PASS=430 Invalid': 430, exit 67.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "331 Password required\r\n430 Invalid\r\n");
        diagnostics.ActRun(run);

        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        diagnostics.Assert("response code", 430, run.Report?.ResponseCode);
        Assert.AreEqual(430, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_User421_Reports421()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // -FtpReply 'USER=421 bye': 421, exit 28.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Greeting + "421 bye\r\n");
        diagnostics.ActRun(run);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, run.Result.ExitCode);
        diagnostics.Assert("response code", 421, run.Report?.ResponseCode);
        Assert.AreEqual(421, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr550_Reports550NotQuits221()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // -FtpReply 'RETR=550 No': 550, exit 78.
        FtpRun run = await FtpRun.ExecuteAsync(Url, Sized + "550 No\r\n" + Bye);
        diagnostics.ActRun(run);

        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        diagnostics.Assert("response code", 550, run.Report?.ResponseCode);
        Assert.AreEqual(550, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Size550_Reports550()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // -FtpReply 'SIZE=550 No': 550, exit 78.
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggedIn + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n550 No\r\n" + Bye);
        diagnostics.ActRun(run);

        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        diagnostics.Assert("response code", 550, run.Report?.ResponseCode);
        Assert.AreEqual(550, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cwd550_Reports550()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ftp://127.0.0.1:18321/d/f.txt");

        // -FtpReply 'CWD=550 No' against /d/f.txt: 550, exit 9.
        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/d/f.txt", LoggedIn + "550 No\r\n" + Bye);
        diagnostics.ActRun(run);

        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, run.Result.ExitCode);
        diagnostics.Assert("response code", 550, run.Report?.ResponseCode);
        Assert.AreEqual(550, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedPostTransferQuote_ReportsTheQuotesCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // -Q -NOOP, answered 502: 502, exit 21.
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Sized + Opening + Complete + "502 Command not implemented\r\n" + Bye,
            "hello",
            c => new TransferContext { Url = c.Url, Output = c.Output, QuoteCommands = ["-NOOP"] });
        diagnostics.ActRun(run);

        Assert.AreEqual(CurlExitCode.QuoteError, run.Result.ExitCode);
        diagnostics.Assert("response code", 502, run.Report?.ResponseCode);
        Assert.AreEqual(502, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Head_ReportsRest0s350()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // -I: 350, exit 0.
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggedIn + "213 20260927123456\r\n200 Type set\r\n213 5\r\n350 Restarting at 0\r\n" + Bye,
            "",
            c => new TransferContext { Url = c.Url, Output = c.Output, NoBody = true });
        diagnostics.ActRun(run);

        Assert.IsTrue(run.Result.IsSuccess);
        diagnostics.Assert("response code", 350, run.Report?.ResponseCode);
        Assert.AreEqual(350, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_Reports226()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ftp://127.0.0.1:18321/up.txt");

        // -T - with USER answered 230: 226, exit 0.
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/up.txt",
            Greeting + "230 ok\r\n257 \"/\" is current directory\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n" + Opening + Complete + Bye,
            "",
            c => new TransferContext { Url = c.Url, Output = c.Output, Upload = new MemoryStream("abc"u8.ToArray()) });
        diagnostics.ActRun(run);

        Assert.IsTrue(run.Result.IsSuccess);
        diagnostics.Assert("response code", 226, run.Report?.ResponseCode);
        Assert.AreEqual(226, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRange_ReportsTheReplyReadForAbor()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        // -r 0-1: the 226 already sent is read as ABOR's reply, and curl reports it: 226.
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Sized + Opening + Complete + "502 Command not implemented\r\n" + Bye,
            "hello",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 1) });
        diagnostics.ActRun(run);

        Assert.AreEqual(TransferResult.Success(2), run.Result);
        diagnostics.Assert("response code", 226, run.Report?.ResponseCode);
        Assert.AreEqual(226, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeWithNoReplyToAbor_KeepsTheCodeBeforeIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Sized + Opening,
            "hello",
            c => new TransferContext { Url = c.Url, Output = c.Output, Range = ByteRange.Bounded(0, 1) });
        diagnostics.ActRun(run);

        Assert.AreEqual(TransferResult.Success(2), run.Result);
        diagnostics.Assert("response code", 150, run.Report?.ResponseCode);
        Assert.AreEqual(150, run.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlClosedBeforeTheGreeting_Reports0()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", Url);

        FtpRun run = await FtpRun.ExecuteAsync(Url, "");
        diagnostics.ActRun(run);

        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        diagnostics.Assert("response code", 0, run.Report?.ResponseCode);
        Assert.AreEqual(0, run.Report?.ResponseCode);
    }
}
