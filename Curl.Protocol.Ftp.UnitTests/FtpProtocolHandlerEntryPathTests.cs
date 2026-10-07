using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <see cref="TransferReport.FtpEntryPath" /> against curl 8.21.0's
/// <c>%{ftp_entry_path}</c>. Each case was recorded on 2026-09-28 with
/// <c>Record-CurlExchange.ps1 -Ftp -FtpData hi -CurlArgs -sS,-o,NUL,-w,%{ftp_entry_path},ftp://127.0.0.1:port/f.txt</c>
/// and the named <c>-FtpReply 'PWD=...'</c> override (BL-514).
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerEntryPathTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18521/f.txt";

    private const string LoggingIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n";

    private const string Download = "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 2\r\n"
        + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    [DataRow("257 \"/\" is cwd", "/", DisplayName = "257 \"/\" prints /")]
    [DataRow("257 \"/home/u\" is cwd", "/home/u", DisplayName = "257 \"/home/u\" prints /home/u")]
    [DataRow("257 \"/a \"\"b\"\"\" is cwd", "/a \"b\"", DisplayName = "doubled quotes print one each")]
    [DataRow("257 rubbish \"/r\" x", "/r", DisplayName = "text before the first quote is skipped")]
    [DataRow("257 \"home\" is cwd\r\n502 Command not implemented", "home", DisplayName = "a relative directory is printed as given")]
    [DataRow("257 /home/u is cwd", null, DisplayName = "no quote prints nothing")]
    [DataRow("257 \"\"", null, DisplayName = "an empty quoted name prints nothing")]
    [DataRow("550 no", null, DisplayName = "a refused PWD prints nothing")]
    public async Task ExecuteAsync_PwdReply_ReportsTheDirectoryItNames(string pwdReply, string? expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pwdReply", pwdReply);
        diagnostics.Arrange("expected", expected);

        // Measured: stdout the directory (or nothing), stderr empty, exit 0.
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggingIn + pwdReply + "\r\n" + Download, "hi");
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Success(2), run.Result);
        Assert.AreEqual(TransferResult.Success(2), run.Result);
        diagnostics.Assert("entry path", expected, run.Report?.FtpEntryPath);
        Assert.AreEqual(expected, run.Report?.FtpEntryPath);
    }

    [TestMethod]
    [DataRow("257 \"/x", DisplayName = "quote never closed")]
    [DataRow("257 \"/x is cwd", DisplayName = "quote never closed before the comment")]
    public async Task ExecuteAsync_PwdReplyWithAnUnendedQuote_FailsWithExit8AndNoQuit(string pwdReply)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pwdReply", pwdReply);

        // Measured: stdout empty, stderr "curl: (8) Weird server reply", exit 8, no QUIT.
        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggingIn + pwdReply + "\r\n");
        diagnostics.ActRun(run);

        diagnostics.Assert("result", TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
        diagnostics.Assert("entry path", null, run.Report?.FtpEntryPath);
        Assert.IsNull(run.Report?.FtpEntryPath);
        Assert.EndsWith("PWD\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrRefusedAfterPwd_StillReportsTheEntryPath()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggingIn + "257 \"/home/u\"\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 2\r\n550 No\r\n221 Bye\r\n");
        diagnostics.ActRun(run);

        diagnostics.Assert("result", CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        diagnostics.Assert("entry path", "/home/u", run.Report?.FtpEntryPath);
        Assert.AreEqual("/home/u", run.Report?.FtpEntryPath);
    }

    [TestMethod]
    public async Task ExecuteAsync_LoginRefused_ReportsNoEntryPath()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        diagnostics.ArrangeFtp(Url);
        FtpRun run = await FtpRun.ExecuteAsync(Url, "220 Recorder ready\r\n331 Password required\r\n530 No\r\n");
        diagnostics.ActRun(run);

        diagnostics.Assert("result", CurlExitCode.LoginDenied, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
        diagnostics.Assert("entry path", null, run.Report?.FtpEntryPath);
        Assert.IsNull(run.Report?.FtpEntryPath);
    }
}
