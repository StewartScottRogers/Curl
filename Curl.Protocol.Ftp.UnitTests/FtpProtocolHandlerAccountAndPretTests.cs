using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>--ftp-account</c>, <c>--ftp-alternative-to-user</c> and <c>--ftp-pret</c> as curl
/// 8.21.0 was measured to send them (BL-635): every case was recorded with
/// <c>Record-CurlExchange.ps1 -Ftp -FtpReply</c> and <c>curl -sS</c>, with <c>-u u:p</c> for
/// the login cases.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerAccountAndPretTests
{
    private const string Url = "ftp://127.0.0.1:47811/f.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    private const string Password = "331 Password required\r\n";

    private const string LoggedIn = "230 Logged in\r\n";

    /// <summary>The recording server's reply to <c>PWD</c>.</summary>
    private const string Pwd = "257 \"/\" is current directory\r\n";

    /// <summary>The replies from <c>EPSV</c> through <c>QUIT</c> for a one-byte download.</summary>
    private const string Downloaded =
        "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 1\r\n"
        + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";

    private const string RetrieveSent = "PWD\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";

    private const string AnonymousSent = "USER anonymous\r\nPASS ftp@example.com\r\n";

    [TestMethod]
    [DataRow(Password + "332 Need account\r\n", "USER u\r\nPASS p\r\n")]
    [DataRow("332 Need account\r\n", "USER u\r\n")]
    public async Task ExecuteAsync_332WithFtpAccount_SendsAcctAndDownloads(string beforeAccount, string sentBeforeAccount)
    {
        // curl -sS -u u:p --ftp-account acc, PASS (or USER) answered 332, ACCT answered 230: exit 0.
        FtpRun run = await RunAsync(Greeting + beforeAccount + "230 OK\r\n" + Pwd + Downloaded, account: "acc");

        Assert.AreEqual(sentBeforeAccount + "ACCT acc\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
        Assert.AreEqual("x", run.OutputText);
    }

    [TestMethod]
    [DataRow("202 Superfl", "ACCT rejected by server: 202")]
    [DataRow("530 No", "ACCT rejected by server: 530")]
    [DataRow("332 More", "ACCT rejected by server: 332")]
    public async Task ExecuteAsync_AcctAnsweredWithAnythingBut230_FailsWithExit11AndNoQuit(string reply, string message)
    {
        // curl: (11) ACCT rejected by server: 530 (ADR-0216).
        FtpRun run = await RunAsync(Greeting + Password + "332 Need account\r\n" + reply + "\r\n", account: "acc");

        Assert.AreEqual("USER u\r\nPASS p\r\nACCT acc\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpWeirdPassReply, message), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserAnswered332WithoutFtpAccount_FailsWithExit67()
    {
        // curl: (67) ACCT requested but none available
        FtpRun run = await RunAsync(Greeting + "332 Need account\r\n");

        Assert.AreEqual("USER u\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "ACCT requested but none available"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassAnswered230WithFtpAccount_SendsNoAcct()
    {
        FtpRun run = await RunAsync(Greeting + Password + LoggedIn + Pwd + Downloaded, account: "acc");

        Assert.AreEqual("USER u\r\nPASS p\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    [DataRow("530 No")]
    [DataRow("500 No")]
    public async Task ExecuteAsync_UserRefusedWithAlternative_SendsTheAlternativeThenPass(string refusal)
    {
        // curl -sS -u u:p --ftp-alternative-to-user "USER alt", USER answered 530 (or 500),
        // then 331: exit 0.
        FtpRun run = await RunAsync(Greeting + refusal + "\r\n331 Pw\r\n" + LoggedIn + Pwd + Downloaded, alternative: "USER alt");

        Assert.AreEqual("USER u\r\nUSER alt\r\nPASS p\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AlternativeAnswered230_LogsInWithoutPass()
    {
        FtpRun run = await RunAsync(Greeting + "530 No\r\n230 In\r\n" + Pwd + Downloaded, alternative: "USER alt");

        Assert.AreEqual("USER u\r\nUSER alt\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AlternativeAnswered332WithFtpAccount_SendsAcct()
    {
        FtpRun run = await RunAsync(Greeting + "530 No\r\n332 Acct\r\n230 OK\r\n" + Pwd + Downloaded, account: "acc", alternative: "USER alt");

        Assert.AreEqual("USER u\r\nUSER alt\r\nACCT acc\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_AlternativeRefusedToo_FailsWithExit67()
    {
        // curl: (67) Access denied: 530 - the alternative is sent once.
        FtpRun run = await RunAsync(Greeting + "530 No\r\n530 Again\r\n", alternative: "USER alt");

        Assert.AreEqual("USER u\r\nUSER alt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 530"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassRefusedWithAlternative_SendsTheAlternativeAndPassAgainOnce()
    {
        // PASS answered 530: curl sends the alternative, PASS again, and gives up at the second 530.
        FtpRun run = await RunAsync(Greeting + Password + "530 No\r\n" + Password + "530 No\r\n", alternative: "USER alt");

        Assert.AreEqual("USER u\r\nPASS p\r\nUSER alt\r\nPASS p\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 530"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassAnswered331WithAlternative_SendsTheAlternative()
    {
        // A 331 to PASS is a refusal, not a request for the password.
        FtpRun run = await RunAsync(Greeting + Password + "331 Again\r\n" + Password + "230 In\r\n" + Pwd + Downloaded, alternative: "USER alt");

        Assert.AreEqual("USER u\r\nPASS p\r\nUSER alt\r\nPASS p\r\n" + RetrieveSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PassAnswered331WithoutAlternative_FailsWithExit67()
    {
        // curl: (67) Access denied: 331
        FtpRun run = await RunAsync(Greeting + Password + "331 Again\r\n");

        Assert.AreEqual("USER u\r\nPASS p\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 331"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_UserAnswered421WithAlternative_FailsWithExit28()
    {
        // curl: (28) Timeout was reached - a 421 ends the session before the alternative.
        FtpRun run = await RunAsync(Greeting + "421 No\r\n", alternative: "USER alt");

        Assert.AreEqual("USER u\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpPretDownload_SendsPretRetrBeforeEpsv()
    {
        // curl -sS --ftp-pret ftp://127.0.0.1:47811/f.txt, PRET answered 200: exit 0.
        FtpRun run = await RunPretAsync(Url, "200 OK\r\n" + Downloaded);

        Assert.AreEqual(AnonymousSent + "PWD\r\nPRET RETR f.txt\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpPretInADirectory_SendsPretAfterCwd()
    {
        FtpRun run = await RunPretAsync("ftp://127.0.0.1:47811/d/f.txt", "250 OK\r\n200 OK\r\n" + Downloaded);

        StringAssert.StartsWith(run.Sent, AnonymousSent + "PWD\r\nCWD d\r\nPRET RETR f.txt\r\nEPSV\r\n");
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1:47811/", false, "PRET LIST\r\n", "LIST\r\n")]
    [DataRow("ftp://127.0.0.1:47811/", true, "PRET NLST\r\n", "NLST\r\n")]
    [DataRow("ftp://127.0.0.1:47811/d/", false, "PRET LIST\r\n", "LIST d\r\n")]
    public async Task ExecuteAsync_FtpPretListing_SendsPretWithTheListVerbAlone(string url, bool listOnly, string pretSent, string listSent)
    {
        // curl -sS --ftp-pret [-l] [--ftp-method nocwd] ftp://127.0.0.1:47811/[d/]: PRET LIST
        // or PRET NLST, with no argument even when LIST carries the directory.
        const string replies = "257 \"/\" is current directory\r\n200 OK\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n"
            + "150 Opening ASCII mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";
        FtpRun run = await FtpRun.ExecuteAsync(
            url,
            Greeting + Password + LoggedIn + replies,
            "x",
            c => new TransferContext
            {
                Url = c.Url,
                Output = c.Output,
                FtpSendPret = true,
                ListOnly = listOnly,
                FtpFileMethod = FtpFileMethod.NoCwd,
            });

        Assert.AreEqual(AnonymousSent + "PWD\r\n" + pretSent + "EPSV\r\nTYPE A\r\n" + listSent + "QUIT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    [DataRow(false, "STOR up.txt")]
    [DataRow(true, "APPE up.txt")]
    public async Task ExecuteAsync_FtpPretUpload_SendsPretStorForStorAndAppe(bool append, string storeSent)
    {
        // curl -sS --ftp-pret [-a] -T file ftp://127.0.0.1:47811/up.txt: PRET STOR up.txt either way.
        const string replies = "257 \"/\" is current directory\r\n200 OK\r\n229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n"
            + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";
        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:47811/up.txt",
            Greeting + Password + LoggedIn + replies,
            adjust: c => new TransferContext
            {
                Url = c.Url,
                Output = c.Output,
                FtpSendPret = true,
                Upload = new MemoryStream(Encoding.Latin1.GetBytes("up")),
                Append = append,
            });

        Assert.AreEqual(AnonymousSent + "PWD\r\nPRET STOR up.txt\r\nEPSV\r\nTYPE I\r\n" + storeSent + "\r\nQUIT\r\n", run.Sent);
        Assert.AreEqual("up", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(2), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpPretWithDisableEpsv_SendsPretBeforePasv()
    {
        const string replies = "257 \"/\" is current directory\r\n200 OK\r\n227 Entering Passive Mode (127,0,0,1,249,77)\r\n200 Type set\r\n213 1\r\n"
            + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";
        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + Password + LoggedIn + replies,
            "x",
            c => new TransferContext { Url = c.Url, Output = c.Output, FtpSendPret = true, FtpDisableEpsv = true });

        StringAssert.StartsWith(run.Sent, AnonymousSent + "PWD\r\nPRET RETR f.txt\r\nPASV\r\n");
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpPretThenEpsvRefused_SendsPretOnceBeforeBoth()
    {
        const string replies = "200 OK\r\n500 no\r\n227 Entering Passive Mode (127,0,0,1,250,48)\r\n200 Type set\r\n213 1\r\n"
            + "150 Opening BINARY mode data connection\r\n226 Transfer complete\r\n221 Bye\r\n";
        FtpRun run = await RunPretAsync(Url, replies);

        StringAssert.StartsWith(run.Sent, AnonymousSent + "PWD\r\nPRET RETR f.txt\r\nEPSV\r\nPASV\r\nTYPE I\r\n");
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    [DataRow("500 No", "PRET command not accepted: 500")]
    [DataRow("202 Superfl", "PRET command not accepted: 202")]
    public async Task ExecuteAsync_PretAnsweredWithAnythingBut200_FailsWithExit84AndNoQuit(string reply, string message)
    {
        // curl: (84) PRET command not accepted: 500
        FtpRun run = await RunPretAsync(Url, reply + "\r\n");

        Assert.AreEqual(AnonymousSent + "PWD\r\nPRET RETR f.txt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPretFailed, message), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PretRefusedForAListing_FailsWithExit84()
    {
        FtpRun run = await RunPretAsync("ftp://127.0.0.1:47811/", "500 No\r\n");

        Assert.AreEqual(AnonymousSent + "PWD\r\nPRET LIST\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpPretFailed, "PRET command not accepted: 500"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PretAnswered421_FailsWithExit28()
    {
        // curl: (28) Timeout was reached
        FtpRun run = await RunPretAsync(Url, "421 No\r\n");

        Assert.AreEqual(AnonymousSent + "PWD\r\nPRET RETR f.txt\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpPretInActiveMode_SendsNoPret()
    {
        // curl -sS --ftp-pret -P - : EPRT with no PRET before it. A handler with no listener
        // cannot bind, so the run ends there with exit 30.
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting + Password + LoggedIn + "257 \"/\" is current directory\r\n221 Bye\r\n"));
        var context = new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), FtpPort = "-", FtpSendPret = true };

        TransferResult result = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control))).ExecuteAsync(context);

        Assert.AreEqual(AnonymousSent + "PWD\r\nQUIT\r\n", Encoding.Latin1.GetString(control.Sent));
        Assert.AreEqual(CurlExitCode.FtpPortFailed, result.ExitCode);
    }

    private static Task<FtpRun> RunAsync(string replies, string? account = null, string? alternative = null) =>
        FtpRun.ExecuteAsync(
            Url,
            replies,
            "x",
            c => new TransferContext
            {
                Url = c.Url,
                Output = c.Output,
                Credentials = new System.Net.NetworkCredential("u", "p"),
                FtpAccount = account,
                FtpAlternativeToUser = alternative,
            });

    /// <summary>
    /// Runs an anonymous <c>--ftp-pret</c> transfer; <paramref name="afterPwd" /> holds the
    /// replies after <c>PWD</c>'s.
    /// </summary>
    private static Task<FtpRun> RunPretAsync(string url, string afterPwd) =>
        FtpRun.ExecuteAsync(
            url,
            Greeting + Password + LoggedIn + Pwd + afterPwd,
            "x",
            c => new TransferContext { Url = c.Url, Output = c.Output, FtpSendPret = true });
}
