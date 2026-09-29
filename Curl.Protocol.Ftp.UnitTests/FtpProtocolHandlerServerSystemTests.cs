using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins the <c>SYST</c> curl 8.21.0 sends after a <c>PWD</c> reply naming a relative
/// directory, and the <c>SITE NAMEFMT 1</c> it sends to an <c>OS/400</c> server. Each
/// measured case was recorded on 2026-09-29 with
/// <c>Record-CurlExchange.ps1 -Ftp -FtpData hello -FtpReply 'PWD=257 "home" is cwd',...
/// -CurlArgs ftp://127.0.0.1:18221/file.txt,-w,[%{ftp_entry_path}]</c>: every one exit 0,
/// stdout <c>hello[home]</c> (<c>hello[/home]</c> for the rooted case), stderr empty (BL-782).
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerServerSystemTests
{
    private const string Url = "ftp://127.0.0.1:18221/file.txt";

    private const string LoggingIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n";

    private const string RelativePwd = "257 \"home\" is cwd\r\n";

    private const string Download = "229 Entering Extended Passive Mode (|||50915|)\r\n200 Type set\r\n213 5\r\n"
        + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string Login = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private const string DownloadCommands = "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";

    [TestMethod]
    [DataRow("502 Command not implemented", DisplayName = "SYST refused (measured)")]
    [DataRow("215 UNIX Type: L8", DisplayName = "215 UNIX (measured)")]
    [DataRow("215 Windows_NT", DisplayName = "215 naming another system")]
    [DataRow("215 OS/4000 x", DisplayName = "215 naming a longer word than OS/400")]
    [DataRow("215 ", DisplayName = "215 with no system")]
    public async Task ExecuteAsync_RelativePwdAndNoOs400_SendsSystAndCarriesOn(string systReply)
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggingIn + RelativePwd + systReply + "\r\n" + Download, "hello");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(Login + "SYST\r\n" + DownloadCommands, run.Sent);
        Assert.AreEqual("home", run.Report?.FtpEntryPath);
    }

    [TestMethod]
    [DataRow("215 OS/400 is the remote operating system", DisplayName = "215 OS/400 (measured)")]
    [DataRow("215   os/400", DisplayName = "any letter case, extra spaces, no commentary")]
    public async Task ExecuteAsync_Os400AndNamefmtRefused_SendsSiteNamefmtAndCarriesOn(string systReply)
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggingIn + RelativePwd + systReply + "\r\n502 Command not implemented\r\n" + Download,
            "hello");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(Login + "SYST\r\nSITE NAMEFMT 1\r\n" + DownloadCommands, run.Sent);
        Assert.AreEqual("home", run.Report?.FtpEntryPath);
    }

    [TestMethod]
    public async Task ExecuteAsync_Os400AndNamefmtAccepted_SendsPwdAgainWithNoSecondSyst()
    {
        // Measured with 'SITE=250 ok': SYST, SITE NAMEFMT 1, PWD, then straight to EPSV.
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggingIn + RelativePwd + "215 OS/400 is the remote operating system\r\n250 ok\r\n"
                + "257 \"lib\" is cwd\r\n" + Download,
            "hello");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(Login + "SYST\r\nSITE NAMEFMT 1\r\nPWD\r\n" + DownloadCommands, run.Sent);
        Assert.AreEqual("lib", run.Report?.FtpEntryPath);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondPwdWithAnUnendedQuote_FailsWithExit8AndNoQuit()
    {
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            LoggingIn + RelativePwd + "215 OS/400\r\n250 ok\r\n257 \"lib\r\n");

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply"), run.Result);
        Assert.AreEqual(Login + "SYST\r\nSITE NAMEFMT 1\r\nPWD\r\n", run.Sent);
    }

    [TestMethod]
    [DataRow("257 \"/home\" is cwd", DisplayName = "a rooted directory (measured)")]
    [DataRow("257 home is cwd", DisplayName = "no quoted directory")]
    [DataRow("550 no", DisplayName = "a refused PWD")]
    public async Task ExecuteAsync_PwdNamingNoRelativeDirectory_SendsNoSyst(string pwdReply)
    {
        FtpRun run = await FtpRun.ExecuteAsync(Url, LoggingIn + pwdReply + "\r\n" + Download, "hello");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        Assert.AreEqual(Login + DownloadCommands, run.Sent);
    }
}
