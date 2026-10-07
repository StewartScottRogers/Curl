using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>-l</c>/<c>--list-only</c> and <c>-Q</c>/<c>--quote</c> over <c>ftp://</c> against
/// curl 8.21.0: where each quote is sent, which replies end the transfer, and the output and
/// exit code. Every case was recorded from real curl on 2026-09-27 with
/// <c>Record-CurlExchange.ps1 -Ftp</c> serving the three bytes <c>abc</c> and uploading
/// <c>hello</c> (BL-436, ADR-0323's BL-436 addendum); the recorder answers <c>502</c> to a
/// command it has no reply for.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerQuoteTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:47380/d/f.txt";

    private const string DirectoryUrl = "ftp://127.0.0.1:47380/d/";

    /// <summary>The recording server's replies from the greeting through <c>PWD</c>.</summary>
    private const string LoggedIn = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Ok = "200 OK\r\n";

    private const string NotImplemented = "502 Command not implemented\r\n";

    private const string DirectoryChanged = "250 OK\r\n";

    private const string Passive = "229 Entering Extended Passive Mode (|||49783|)\r\n200 Type set\r\n";

    private const string Sized = "213 3\r\n";

    private const string Opened = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>The replies to <c>MDTM</c>, <c>TYPE I</c>, <c>SIZE</c> and <c>REST 0</c> under <c>-I</c>.</summary>
    private const string HeadReplies = "213 20260927123456\r\n200 Type set\r\n" + Sized + "350 Restarting at 0\r\n";

    private const string HeadLines = "Last-Modified: Sun, 27 Sep 2026 12:34:56 GMT\r\nContent-Length: 3\r\nAccept-ranges: bytes\r\n";

    private const string LogInSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    private const string HeadSent = "MDTM f.txt\r\nTYPE I\r\nSIZE f.txt\r\nREST 0\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyOnADirectory_SendsNlst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -l ftp://127.0.0.1:47380/d/
        FtpRun run = await RunAsync(diagnostics, DirectoryUrl, LoggedIn + DirectoryChanged + Passive + Opened + Complete + Bye, ListOnly);

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyOnAFile_ListsItsDirectoryInstead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -l ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + Passive + Opened + Complete + Bye, ListOnly);

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyRefusedWith550_QuitsWithExit19()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -l ftp://127.0.0.1:47380/d/, NLST answered 550 No files
        FtpRun run = await RunAsync(diagnostics, DirectoryUrl, LoggedIn + DirectoryChanged + Passive + "550 No files\r\n" + Bye, ListOnly);

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.FtpCouldntRetrFile, "RETR response: 550"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyAnswered450_IsAnEmptyListing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -l ftp://127.0.0.1:47380/d/, NLST answered 450 No files
        FtpRun run = await RunAsync(diagnostics, DirectoryUrl, LoggedIn + DirectoryChanged + Passive + "450 No files\r\n" + Bye, ListOnly);

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE A\r\nNLST\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(string.Empty, run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyWithNoCwd_NamesTheDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -l --ftp-method nocwd ftp://127.0.0.1:47380/a/b/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            "ftp://127.0.0.1:47380/a/b/f.txt",
            LoggedIn + Passive + Opened + Complete + Bye,
            context =>
            {
                context.ListOnly = true;
                context.FtpFileMethod = FtpFileMethod.NoCwd;
            });

        var expectedSent = LogInSent + "EPSV\r\nTYPE A\r\nNLST a/b\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyWithAnUpload_StoresAsWithout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -l -T up.txt ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + Passive + Opened + Complete + Bye, context =>
        {
            context.ListOnly = true;
            context.Upload = Hello();
        });

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSTOR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListOnlyWithHead_ReportsTheFileAsWithout()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -l -I ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + HeadReplies + Bye, context =>
        {
            context.ListOnly = true;
            Head(context);
        });

        var expectedSent = LogInSent + "CWD d\r\n" + HeadSent + "QUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(HeadLines, run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuotesOfEveryKind_SendsEachWhereCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q NOOP -Q "+SITE A" -Q "-DELE f.txt" -Q "*BOGUS" ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + Ok + NotImplemented + DirectoryChanged + Passive + Ok + Sized + Opened + Complete + "250 Deleted\r\n" + Bye,
            Quotes("NOOP", "+SITE A", "-DELE f.txt", "*BOGUS"));

        var expectedSent =
            LogInSent + "NOOP\r\nBOGUS\r\nCWD d\r\nEPSV\r\nTYPE I\r\nSITE A\r\nSIZE f.txt\r\nRETR f.txt\r\nDELE f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuoteRefused_EndsWithExit21AndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q "BOGUS x" ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + NotImplemented, Quotes("BOGUS x"));

        var expectedSent = LogInSent + "BOGUS x\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "QUOT command failed with 502"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreTransferQuoteRefused_EndsWithExit21AndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q "+BOGUS x" ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + Passive + NotImplemented, Quotes("+BOGUS x"));

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nBOGUS x\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "QUOT command failed with 502"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteRefused_QuitsWithExit21AfterWritingTheFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q "-BOGUS x" ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + DirectoryChanged + Passive + Sized + Opened + Complete + NotImplemented + Bye,
            Quotes("-BOGUS x"));

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nBOGUS x\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "QUOT string not accepted: BOGUS x", 3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteWithStarRefused_Succeeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q "-*BOGUS x" ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + DirectoryChanged + Passive + Sized + Opened + Complete + NotImplemented + Bye,
            Quotes("-*BOGUS x"));

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nBOGUS x\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuoteAnswered350_IsAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q "RNFR a" ftp://127.0.0.1:47380/d/f.txt, RNFR answered 350 Ready
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + "350 Ready\r\n" + DirectoryChanged + Passive + Sized + Opened + Complete + Bye,
            Quotes("RNFR a"));

        var expectedSent = LogInSent + "RNFR a\r\nCWD d\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuoteAnswered421_EndsWithExit28AndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q NOOP ftp://127.0.0.1:47380/d/f.txt, NOOP answered 421 Bye
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + "421 Bye\r\n", Quotes("NOOP"));

        var expectedSent = LogInSent + "NOOP\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StarAlone_SendsAnEmptyCommandAndIgnoresItsFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q "*" ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + NotImplemented + DirectoryChanged + Passive + Sized + Opened + Complete + Bye,
            Quotes("*"));

        var expectedSent = LogInSent + "\r\nCWD d\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StarBeforeAMinus_SendsTheMinusAfterLogin()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q "+*BOGUS" -Q "*-X" ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + NotImplemented + DirectoryChanged + Passive + NotImplemented + Sized + Opened + Complete + Bye,
            Quotes("+*BOGUS", "*-X"));

        var expectedSent = LogInSent + "-X\r\nCWD d\r\nEPSV\r\nTYPE I\r\nBOGUS\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuotesWithHead_SendsPreAndPostTransferQuotesAfterRest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -I -Q NOOP -Q +NOOP -Q -NOOP ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + Ok + DirectoryChanged + HeadReplies + Ok + Ok + Bye, context =>
        {
            Head(context);
            context.QuoteCommands.AddRange(["NOOP", "+NOOP", "-NOOP"]);
        });

        var expectedSent = LogInSent + "NOOP\r\nCWD d\r\n" + HeadSent + "NOOP\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(HeadLines, run.OutputText);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreTransferQuoteRefusedWithHead_EndsWithExit21AfterTheHeaders()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -I -Q +BOGUS ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + HeadReplies + NotImplemented, context =>
        {
            Head(context);
            context.QuoteCommands.Add("+BOGUS");
        });

        var expectedSent = LogInSent + "CWD d\r\n" + HeadSent + "BOGUS\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(HeadLines, run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "QUOT command failed with 502"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuotesWithHeadOnADirectory_SendsEveryQuote()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -I -Q NOOP -Q +NOOP -Q -NOOP ftp://127.0.0.1:47380/d/
        FtpRun run = await RunAsync(diagnostics, DirectoryUrl, LoggedIn + Ok + DirectoryChanged + Ok + Ok + Bye, context =>
        {
            Head(context);
            context.QuoteCommands.AddRange(["NOOP", "+NOOP", "-NOOP"]);
        });

        var expectedSent = LogInSent + "NOOP\r\nCWD d\r\nNOOP\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuotesWithAnUpload_SendsEachAroundStor()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q NOOP -Q +NOOP -Q -NOOP -T up.txt ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + Ok + DirectoryChanged + Passive + Ok + Opened + Complete + Ok + Bye,
            context =>
            {
                context.Upload = Hello();
                context.QuoteCommands.AddRange(["NOOP", "+NOOP", "-NOOP"]);
            });

        var expectedSent = LogInSent + "NOOP\r\nCWD d\r\nEPSV\r\nTYPE I\r\nNOOP\r\nSTOR f.txt\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("hello", Encoding.Latin1.GetString(run.Data.Sent));
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreTransferQuoteWithAResumedUpload_IsSentBeforeSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -C - -Q +NOOP -T up.txt ftp://127.0.0.1:47380/d/f.txt, SIZE answered 213 2
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + DirectoryChanged + Passive + Ok + "213 2\r\n" + Opened + Complete + Bye,
            context =>
            {
                context.Upload = Hello();
                context.ResumeUploadFromUnknownOffset = true;
                context.QuoteCommands.Add("+NOOP");
            });

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nNOOP\r\nSIZE f.txt\r\nAPPE f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("llo", Encoding.Latin1.GetString(run.Data.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteWithAnUploadAlreadyWhole_IsSentBeforeQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -C 5 -Q -NOOP -T up.txt ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + Passive + Ok + Bye, context =>
        {
            context.Upload = Hello();
            context.ResumeFrom = 5;
            context.QuoteCommands.Add("-NOOP");
        });

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuotesWithAListing_SendsEachAroundList()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q NOOP -Q +NOOP -Q -NOOP ftp://127.0.0.1:47380/d/
        FtpRun run = await RunAsync(diagnostics, 
            DirectoryUrl,
            LoggedIn + Ok + DirectoryChanged + Passive + Ok + Opened + Complete + Ok + Bye,
            Quotes("NOOP", "+NOOP", "-NOOP"));

        var expectedSent = LogInSent + "NOOP\r\nCWD d\r\nEPSV\r\nTYPE A\r\nNOOP\r\nLIST\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("abc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteAfterAnEmptyListing_IsSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q -NOOP ftp://127.0.0.1:47380/d/, LIST answered 450 none
        FtpRun run = await RunAsync(diagnostics, DirectoryUrl, LoggedIn + DirectoryChanged + Passive + "450 none\r\n" + Ok + Bye, Quotes("-NOOP"));

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE A\r\nLIST\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreTransferQuoteWithAResumedDownload_IsSentBeforeSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -C 1 -Q +NOOP ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + DirectoryChanged + Passive + Ok + Sized + "350 Restarting at 1\r\n" + Opened + Complete + Bye,
            context =>
            {
                context.ResumeFrom = 1;
                context.QuoteCommands.Add("+NOOP");
            },
            "bc");

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nNOOP\r\nSIZE f.txt\r\nREST 1\r\nRETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("bc", run.OutputText);
        Assert.AreEqual(TransferResult.Success(2), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteWithNothingLeftToResume_IsSentBeforeQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -C 3 -Q -NOOP ftp://127.0.0.1:47380/d/f.txt
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + Passive + Sized + Ok + Bye, context =>
        {
            context.ResumeFrom = 3;
            context.QuoteCommands.Add("-NOOP");
        });

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteAfterAFailedTransfer_IsNotSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q -NOOP ftp://127.0.0.1:47380/d/f.txt, RETR answered 550 No file
        FtpRun run = await RunAsync(diagnostics, Url, LoggedIn + DirectoryChanged + Passive + Sized + "550 No file\r\n" + Bye, Quotes("-NOOP"));

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "RETR response: 550"), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PostTransferQuoteAfterARange_IsSentAfterAborAndReadsTheRepliesInTurn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -r 0-0 -Q -NOOP ftp://127.0.0.1:47380/d/f.txt: the recorder sent 226 before
        // curl's ABOR, so curl read 226 for ABOR and ABOR's 502 for NOOP, and failed.
        FtpRun run = await RunAsync(diagnostics, 
            Url,
            LoggedIn + DirectoryChanged + Passive + Sized + Opened + Complete + NotImplemented + Ok + Bye,
            context =>
            {
                context.Range = ByteRange.Bounded(0, 0);
                context.QuoteCommands.Add("-NOOP");
            });

        var expectedSent = LogInSent + "CWD d\r\nEPSV\r\nTYPE I\r\nSIZE f.txt\r\nRETR f.txt\r\nABOR\r\nNOOP\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual("a", run.OutputText);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.QuoteError, "QUOT string not accepted: NOOP", 1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuoteWithAControlCharacterInThePath_SendsNoQuote()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // curl -Q NOOP ftp://127.0.0.1:47380/d%01/f.txt
        FtpRun run = await RunAsync(diagnostics, "ftp://127.0.0.1:47380/d%01/f.txt", LoggedIn, Quotes("NOOP"));

        var expectedSent = LogInSent;
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.UrlMalformat, "path contains control characters"), run.Result);
    }

    private static void ListOnly(MutableContext context) => context.ListOnly = true;

    private static void Head(MutableContext context)
    {
        context.NoBody = true;
        context.HeaderOutput = context.Output;
    }

    private static Action<MutableContext> Quotes(params string[] values) => context => context.QuoteCommands.AddRange(values);

    private static MemoryStream Hello() => new(Encoding.Latin1.GetBytes("hello"));

    private static async Task<FtpRun> RunAsync(TestDiagnostics diagnostics, string url, string replies, Action<MutableContext> adjust, string data = "abc")
    {
        diagnostics.ArrangeFtp(url, replies, data);

        FtpRun run = await FtpRun.ExecuteAsync(url, replies, data, context => MutableContext.Build(context, adjust));

        diagnostics.ActRun(run);

        return run;
    }
}
