using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins <c>ftp://</c> downloads against curl 8.21.0: the commands each URL sends on the
/// control connection, the targets it connects to, the body written to the output, and
/// the exit code and message of every way a transfer ends. Each command sequence and
/// message was recorded from real curl with <c>Record-CurlExchange.ps1 -Ftp</c> (BL-431)
/// and is replayed here through <see cref="ScriptedConnection" />, so nothing touches a
/// network.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18321/file.txt";

    private const string Greeting = "220 Recorder ready\r\n";

    /// <summary>The recording server's replies from the greeting through <c>PWD</c>.</summary>
    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||61744|)\r\n";

    private const string TypeSet = "200 Type set\r\n";

    private const string Opening = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    /// <summary>What curl 8.21.0 sent for <c>curl ftp://127.0.0.1:18321/file.txt</c>, up to <c>PWD</c>.</summary>
    private const string LoginSent = "USER anonymous\r\nPASS ftp@example.com\r\nPWD\r\n";

    /// <summary>What curl 8.21.0 sent for <c>curl ftp://127.0.0.1:18321/file.txt</c>.</summary>
    private const string DownloadSent = LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";


    [TestMethod]
    public void SupportedSchemes_IsExactlyFtp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connector", "QueuedConnector (empty)");
        FtpProtocolHandler handler = new(new QueuedConnector());

        string[] schemes = handler.SupportedSchemes.ToArray();

        diagnostics.Act("supported schemes", string.Join(",", schemes));
        diagnostics.Assert("supported schemes", "ftp", string.Join(",", schemes));
        CollectionAssert.AreEqual(new[] { "ftp" }, schemes);
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("connector", "null");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new FtpProtocolHandler(null!));

        diagnostics.Act("throws", thrown.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("context", "null");
        FtpProtocolHandler handler = new(new QueuedConnector());

        ArgumentNullException thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));

        diagnostics.Act("throws", thrown.GetType().Name);
        diagnostics.Assert("exception type", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_File_SendsCurlsCommandsAndWritesTheBody()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 10\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "hello ftp\n");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "hello ftp\n");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Diff("output", "hello ftp\n", run.OutputText);
        Assert.AreEqual("hello ftp\n", run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(10), run.Result);
        Assert.AreEqual(TransferResult.Success(10), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_File_ConnectsControlToTheUrlAndDataToTheEpsvPort()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "x");

        diagnostics.ActRun(run);
        diagnostics.Act("connect targets", run.Connector.Targets.Count);
        diagnostics.Assert("connect target count", 2, run.Connector.Targets.Count);
        Assert.HasCount(2, run.Connector.Targets);
        diagnostics.Assert("control target", new ConnectTarget("127.0.0.1", 18321, false) { PoolScheme = "ftp", TcpIoTrace = new TcpIoTraceLines("TCP", 900, false) }, run.Connector.Targets[0]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 18321, false) { PoolScheme = "ftp", TcpIoTrace = new TcpIoTraceLines("TCP", 900, false) }, run.Connector.Targets[0]);
        diagnostics.Assert("data target", new ConnectTarget("127.0.0.1", 61744, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 61744, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        diagnostics.Assert("control disposed", true, run.Control.IsDisposed);
        Assert.IsTrue(run.Control.IsDisposed);
        diagnostics.Assert("data disposed", true, run.Data.IsDisposed);
        Assert.IsTrue(run.Data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoPortInUrl_ConnectsToPort21()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp("ftp://h/file.txt", replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync("ftp://h/file.txt", replies, "x");

        diagnostics.ActRun(run);
        diagnostics.Assert("control target", new ConnectTarget("h", 21, false) { PoolScheme = "ftp", TcpIoTrace = new TcpIoTraceLines("TCP", 900, false) }, run.Connector.Targets[0]);
        Assert.AreEqual(new ConnectTarget("h", 21, false) { PoolScheme = "ftp", TcpIoTrace = new TcpIoTraceLines("TCP", 900, false) }, run.Connector.Targets[0]);
        diagnostics.Assert("data target", new ConnectTarget("h", 61744, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("h", 61744, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Proxy_TunnelsBothConnections()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var proxy = new ProxyEndpoint(ProxyKind.Socks5, "proxy", 1080, null);
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");
        diagnostics.Arrange("proxy", proxy);

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x",
            context => new TransferContext { Url = context.Url, Output = context.Output, Proxy = proxy });

        diagnostics.ActRun(run);
        diagnostics.Assert("control proxy", proxy, run.Connector.Targets[0].Proxy);
        Assert.AreEqual(proxy, run.Connector.Targets[0].Proxy);
        diagnostics.Assert("data proxy", proxy, run.Connector.Targets[1].Proxy);
        Assert.AreEqual(proxy, run.Connector.Targets[1].Proxy);
    }

    [TestMethod]
    public async Task ExecuteAsync_File_ReportsProgress()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var progress = new RecordingProgress();
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "abc",
            context => new TransferContext { Url = context.Url, Output = context.Output, Progress = progress });

        diagnostics.ActRun(run);
        diagnostics.Act("progress started", progress.Started);
        diagnostics.Assert("progress started", true, progress.Started);
        Assert.IsTrue(progress.Started);
        diagnostics.Assert("downloaded", "3 of 3", string.Join(",", progress.Downloaded.Select(step => $"{step.Item1} of {step.Item2}")));
        CollectionAssert.AreEqual(new[] { (3L, (long?)3) }, progress.Downloaded);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnectRefused_ReturnsTheConnectorsFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ftp://h/f");
        diagnostics.Arrange("control connect", "refused: Failed to connect to h port 21");
        var connector = new QueuedConnector(ConnectResult.Refused("Failed to connect to h port 21"));
        FtpProtocolHandler handler = new(connector);

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ftp://h/f"), Output = new MemoryStream() });

        diagnostics.ActResult(result);
        diagnostics.Act("connection refused", result.IsConnectionRefused);
        diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        diagnostics.Assert("error", "Failed to connect to h port 21", result.ErrorMessage);
        Assert.AreEqual("Failed to connect to h port 21", result.ErrorMessage);
        diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.IsTrue(result.IsConnectionRefused);
        diagnostics.Assert("connect target count", 1, connector.Targets.Count);
        Assert.HasCount(1, connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_MultiLineGreeting_SkipsToTheLastLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = "220-Welcome\r\nno code here\r\n22\r\n220-second\n220 ready\r\n331 Password required\r\n230 Logged in\r\n257\r\n257x\r\n257 done\r\n"
            + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_RepliesSplitAcrossReads_AreReassembled()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] replies = Encoding.Latin1.GetBytes(LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye);
        var control = new ScriptedConnection(replies[..5], replies[5..30], replies[30..]);
        diagnostics.Arrange("url", Url);
        diagnostics.Arrange("control reads split at", "5 and 30");
        diagnostics.Bytes("control replies", replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection("x"u8.ToArray()));

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadGreeting_FailsWithExit8AndSendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, "500 go away\r\n");

        FtpRun run = await FtpRun.ExecuteAsync(Url, "500 go away\r\n");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(string.Empty, run.Sent);
        Assert.AreEqual(string.Empty, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.WeirdServerReply, "Got a 500 ftp-server response when 220 was expected");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_BareCodeLine_DoesNotEndTheReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "331\r\n230 ok\r\n257 \"/\" is current directory\r\n" + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x");

        diagnostics.ActRun(run);
        const string expectedSent = "USER anonymous\r\nPWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Greeting230_SkipsTheLogin()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = "230 welcome back\r\n257 \"/\" is current directory\r\n" + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "abc");

        diagnostics.ActRun(run);
        const string expectedSent = "PWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(3), run.Result);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Greeting421_FailsWithExit28AndSendsNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, "421 busy\r\n");

        FtpRun run = await FtpRun.ExecuteAsync(Url, "421 busy\r\n");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(string.Empty, run.Sent);
        Assert.AreEqual(string.Empty, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pass421_FailsWithExit28WithoutQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "331 Password required\r\n421 bye\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        const string expectedSent = "USER anonymous\r\nPASS ftp@example.com\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.OperationTimedOut, "Timeout was reached");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferEndsWith421_FailsWithExit28ControlConnectionLooksDead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening + "421 bye\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "abc");

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.OperationTimedOut, "control connection looks dead", 3);
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pass430_FailsWithExit67AccessDenied430AndNoQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "331 Password required\r\n430 Access denied\r\n";
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        const string expectedSent = "USER anonymous\r\nPASS ftp@example.com\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 430");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_User530_FailsWithExit67WithoutSendingPass()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "530 nope\r\n";
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        diagnostics.DiffSent("USER anonymous\r\n", run.Sent);
        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 530");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Pass332_FailsWithExit67AcctRequested()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "331 Password required\r\n332 need account\r\n";
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.LoginDenied, "ACCT requested but none available");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_User230_SkipsPass()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "230 ok\r\n257 \"/\" is current directory\r\n" + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x");

        diagnostics.ActRun(run);
        const string expectedSent = "USER anonymous\r\nPWD\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Assert("success", true, run.Result.IsSuccess);
        Assert.IsTrue(run.Result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_Credentials_LogInAsThatUser()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");
        diagnostics.Arrange("credentials", "bob:s3cret");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x",
            context => new TransferContext { Url = context.Url, Output = context.Output, Credentials = new NetworkCredential("bob", "s3cret") });

        diagnostics.ActRun(run);
        diagnostics.Assert("sent starts with", "USER bob\r\nPASS s3cret\r\nPWD\r\n", run.Sent);
        StringAssert.StartsWith(run.Sent, "USER bob\r\nPASS s3cret\r\nPWD\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialsWithCrLf_AreSentAsGivenAsCurlDoes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "331 Password required\r\n502 Command not implemented\r\n";
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("credentials", FtpDiagnostics.Escape("a\r\nDELE x:pw"));

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            adjust: context => new TransferContext { Url = context.Url, Output = context.Output, Credentials = new NetworkCredential("a\r\nDELE x", "pw") });

        diagnostics.ActRun(run);
        diagnostics.DiffSent("USER a\r\nDELE x\r\nPASS pw\r\n", run.Sent);
        Assert.AreEqual("USER a\r\nDELE x\r\nPASS pw\r\n", run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.LoginDenied, "Access denied: 502");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LatinOneCredentials_AreSentOneBytePerCharacter()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, Greeting + "430 no\r\n");
        diagnostics.Arrange("credentials", "jörg:pw");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            Greeting + "430 no\r\n",
            adjust: context => new TransferContext { Url = context.Url, Output = context.Output, Credentials = new NetworkCredential("jörg", "pw") });

        diagnostics.ActRun(run);
        byte[] expectedBytes = [0x55, 0x53, 0x45, 0x52, 0x20, 0x6A, 0xF6, 0x72, 0x67, 0x0D, 0x0A];
        diagnostics.Diff("sent bytes", expectedBytes, run.SentBytes);
        CollectionAssert.AreEqual(expectedBytes, run.SentBytes);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65535BytesWithItsCrLf_IsRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = "220 " + new string('x', 65529) + "\r\n530 no\r\n";
        diagnostics.Arrange("url", Url);
        diagnostics.Arrange("greeting length", 65535);
        diagnostics.Arrange("control replies length", replies.Length);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        diagnostics.DiffSent("USER anonymous\r\n", run.Sent);
        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        diagnostics.Assert("exit code", CurlExitCode.LoginDenied, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.LoginDenied, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReplyLineOf65536Bytes_FailsWithExit100()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = "220 " + new string('x', 65530) + "\r\n";
        diagnostics.Arrange("url", Url);
        diagnostics.Arrange("greeting length", 65536);
        diagnostics.Arrange("control replies length", replies.Length);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        diagnostics.DiffSent(string.Empty, run.Sent);
        Assert.AreEqual(string.Empty, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OversizedReplyToQuit_StillSucceeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + "221 " + new string('x', 70000);
        diagnostics.Arrange("url", Url);
        diagnostics.Arrange("quit reply length", 70004);
        diagnostics.Arrange("control replies length", replies.Length);

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PwdRefused_CarriesOn()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "331 Password required\r\n230 Logged in\r\n500 no\r\n" + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "abc");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(3), run.Result);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Directories_SendOneCwdEachSkippingEmptySegments()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "250 OK\r\n250 OK\r\n" + Epsv + TypeSet + "213 3\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18321/dir//sub/file.txt", replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/dir//sub/file.txt",
            replies,
            "abc");

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "CWD dir\r\nCWD sub\r\nEPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Diff("output", "abc", run.OutputText);
        Assert.AreEqual("abc", run.OutputText);
    }

    [TestMethod]
    public async Task ExecuteAsync_PercentEncodedPath_SendsTheDecodedBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "250 OK\r\n250 OK\r\n" + Epsv + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18321/caf%C3%A9/a%20b%2x%/%E9.txt", replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/caf%C3%A9/a%20b%2x%/%E9.txt",
            replies,
            "x");

        diagnostics.ActRun(run);
        byte[] expectedBytes = Encoding.Latin1.GetBytes(LoginSent + "CWD cafÃ©\r\nCWD a b%2x%\r\nEPSV\r\nTYPE I\r\nSIZE é.txt\r\nRETR é.txt\r\nQUIT\r\n");
        diagnostics.Diff("sent bytes", expectedBytes, run.SentBytes);
        CollectionAssert.AreEqual(
            expectedBytes,
            run.SentBytes);
    }

    [TestMethod]
    public async Task ExecuteAsync_CwdRefused_QuitsAndFailsWithExit9()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "550 nope\r\n" + Bye;
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18321/dir/file.txt", replies);

        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/dir/file.txt", replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "CWD dir\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.RemoteAccessDenied, "Server denied you to change to the given directory");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [DataRow("ftp://127.0.0.1:18321/a%0Ab/f")]
    [DataRow("ftp://127.0.0.1:18321/f%00x")]
    public async Task ExecuteAsync_ControlCharacterInPath_FailsWithExit3AfterPwd(string url)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(url, LoggedIn);

        FtpRun run = await FtpRun.ExecuteAsync(url, LoggedIn);

        diagnostics.ActRun(run);
        diagnostics.DiffSent(LoginSent, run.Sent);
        Assert.AreEqual(LoginSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.UrlMalformat, "path contains control characters");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvRefused_FallsBackToPasvAndItsPort()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "500 no\r\n227 Entering Passive Mode (127,0,0,1,245,222)\r\n" + TypeSet + "213 3\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "abc");

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nPASV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Assert("data target", new ConnectTarget("127.0.0.1", 62942, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 62942, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        diagnostics.Assert("result", TransferResult.Success(3), run.Result);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasvNumbersWithoutParentheses_AreRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "500 no\r\n227 127,0,0,1,1,2\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x");

        diagnostics.ActRun(run);
        diagnostics.Assert("data target", new ConnectTarget("127.0.0.1", 258, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 258, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        diagnostics.Assert("success", true, run.Result.IsSuccess);
        Assert.IsTrue(run.Result.IsSuccess);
    }

    [TestMethod]
    public async Task ExecuteAsync_Cancelled_ThrowsAndDisposesBothConnections()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening;
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies));
        var data = new ScriptedConnection("abc"u8.ToArray());
        diagnostics.ArrangeFtp(Url, replies, "abc");
        diagnostics.Arrange("output", "refuses the write by cancelling");

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await FtpRun.ExecuteAsync(
            Url,
            control,
            data,
            context => new TransferContext { Url = context.Url, Output = new WriteRefusingStream(new OperationCanceledException()) }));

        diagnostics.Act("throws", thrown.GetType().Name);
        diagnostics.Assert("control disposed", true, control.IsDisposed);
        Assert.IsTrue(control.IsDisposed);
        diagnostics.Assert("data disposed", true, data.IsDisposed);
        Assert.IsTrue(data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledBeforeTheDataConnection_ThrowsAndDisposesTheControlConnection()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting))
        {
            WritesBeforeFailure = 0,
            WriteFailure = new OperationCanceledException(),
        };
        var data = new ScriptedConnection();
        diagnostics.ArrangeFtp(Url, Greeting);
        diagnostics.Arrange("control write", "cancels on the first write");

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await FtpRun.ExecuteAsync(Url, control, data));

        diagnostics.Act("throws", thrown.GetType().Name);
        diagnostics.Assert("control disposed", true, control.IsDisposed);
        Assert.IsTrue(control.IsDisposed);
        diagnostics.Assert("data disposed", false, data.IsDisposed);
        Assert.IsFalse(data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasvAddress_IsIgnoredForTheControlHost()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "500 no\r\n227 Entering Passive Mode (10,1,2,3,0,21)\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "x");

        diagnostics.ActRun(run);
        diagnostics.Assert("data target", new ConnectTarget("127.0.0.1", 21, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", 21, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
    }

    [TestMethod]
    [DataRow("229 garbage")]
    [DataRow("229 Entering Extended Passive Mode |||40000|")]
    [DataRow("229 Entering Extended Passive Mode (|!|40000|)")]
    [DataRow("229 Entering Extended Passive Mode (||!40000|)")]
    [DataRow("229 Entering Extended Passive Mode (||x|)")]
    [DataRow("229 Entering Extended Passive Mode (||")]
    [DataRow("229 Entering Extended Passive Mode (|||")]
    [DataRow("229 Entering Extended Passive Mode (||||)")]
    public async Task ExecuteAsync_EpsvReplyWithoutThreeDelimitersAndADigit_QuitsAndFailsWithExit13WeirdlyFormatted(string reply)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + reply + "\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("epsv reply", reply);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.FtpWeirdPasvReply, "Weirdly formatted EPSV reply");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [DataRow("229 Entering Extended Passive Mode (|||99999|)")]
    [DataRow("229 Entering Extended Passive Mode (|||65536|)")]
    [DataRow("229 Entering Extended Passive Mode (|||99999999999|)")]
    [DataRow("229 Entering Extended Passive Mode (|||123x)")]
    [DataRow("229 Entering Extended Passive Mode (|||4a000|)")]
    [DataRow("229 Entering Extended Passive Mode (|||40000")]
    [DataRow("229 Entering Extended Passive Mode (1112|)")]
    public async Task ExecuteAsync_EpsvReplyWithAnUnreadablePort_QuitsAndFailsWithExit13IllegalPort(string reply)
    {
        // curl 8.21.0 -v, measured 2026-10-02 for (|||99999|) and (|||123x) (BL-1240).
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + reply + "\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("epsv reply", reply);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.FtpWeirdPasvReply, "Illegal port number in EPSV reply");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [DataRow("229 Entering Extended Passive Mode (|||65535|)", 65535)]
    [DataRow("229 Entering Extended Passive Mode (|||40000|", 40000)]
    [DataRow("229 Entering Extended Passive Mode (|||40000|x", 40000)]
    [DataRow("229 Entering Extended Passive Mode (!!!40000!)", 40000)]
    public async Task ExecuteAsync_EpsvPortClosedByTheDelimiter_DialsThatPortWhateverFollows(string reply, int port)
    {
        // curl 8.21.0 dials port 0 and (|||40000| with no ')' alike, measured 2026-10-02 (BL-1240).
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + reply + "\r\n" + TypeSet + "213 1\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");
        diagnostics.Arrange("epsv reply", reply);
        diagnostics.Arrange("expected port", port);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "x");

        diagnostics.ActRun(run);
        diagnostics.Assert("data target", new ConnectTarget("127.0.0.1", port, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        Assert.AreEqual(new ConnectTarget("127.0.0.1", port, false) { TcpIoTrace = new TcpIoTraceLines("TCP-1", null, true) }, run.Connector.Targets[1]);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvAndPasvRefused_QuitsAndFailsWithExit13()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "500 no\r\n500 no\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nPASV\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.FtpWeirdPasvReply, "Bad PASV/EPSV response: 500");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [DataRow("227 garbage")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,256,1)")]
    [DataRow("227 Entering Passive Mode (300,0,0,1,0,21)")]
    [DataRow("227 Entering Passive Mode (1234,0,0,1,0,21)")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0)")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0,)")]
    [DataRow("227 Entering Passive Mode (127,0,0,1,0;21)")]
    public async Task ExecuteAsync_UnreadablePasvReply_FailsWithExit14WithoutQuit(string reply)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "500 no\r\n" + reply + "\r\n";
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("pasv reply", reply);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nPASV\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.FtpWeird227Format, "Could not interpret the 227-response");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataConnectRefused_ReturnsTheConnectorsFailure()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "227 Entering Passive Mode (127,0,0,1,241,48)\r\n";
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies));
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Refused("Failed to connect to 127.0.0.1 port 61744"));
        diagnostics.ArrangeFtp(Url, replies);
        diagnostics.Arrange("data connect", "refused: Failed to connect to 127.0.0.1 port 61744");
        diagnostics.Arrange("FtpDisableEpsv", true);

        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), FtpDisableEpsv = true });

        diagnostics.ActResult(result);
        diagnostics.Act("connection refused", result.IsConnectionRefused);
        diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        diagnostics.Assert("error", "Failed to connect to 127.0.0.1 port 61744", result.ErrorMessage);
        Assert.AreEqual("Failed to connect to 127.0.0.1 port 61744", result.ErrorMessage);
        diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.IsTrue(result.IsConnectionRefused);
        diagnostics.Assert("control disposed", true, control.IsDisposed);
        Assert.IsTrue(control.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_TypeRefused_QuitsAndFailsWithExit17()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + "500 no\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nTYPE I\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.FtpCouldntSetType, "Could not set desired mode");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Size550_QuitsAndFailsWithExit78()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "550 no\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "The file does not exist");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [DataRow("500 not supported")]
    [DataRow("213 ")]
    [DataRow("213 many")]
    [DataRow("213 -5")]
    public async Task ExecuteAsync_SizeUnknown_DownloadsWhateverArrives(string reply)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + reply + "\r\n" + Opening + Complete + Bye;
        diagnostics.ArrangeFtp(Url, replies, "abc");
        diagnostics.Arrange("size reply", reply);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "abc");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(3), run.Result);
        Assert.AreEqual(TransferResult.Success(3), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr550_QuitsAndFailsWithExit78()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 0\r\n550 No such file\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.RemoteFileNotFound, "RETR response: 550");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr500_QuitsAndFailsWithExit19()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 0\r\n500 x\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.FtpCouldntRetrFile, "RETR response: 500");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_Retr125_Downloads()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n125 go\r\n250 ok\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies, "x");

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "x");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathEndingInSlash_ListsTheDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + "250 OK\r\n" + Epsv + TypeSet + Opening + Complete + Bye;
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18321/dir/", replies, "drw a\r\n");

        FtpRun run = await FtpRun.ExecuteAsync(
            "ftp://127.0.0.1:18321/dir/",
            replies,
            "drw a\r\n");

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "CWD dir\r\nEPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Diff("output", FtpDiagnostics.Escape("drw a\r\n"), FtpDiagnostics.Escape(run.OutputText));
        Assert.AreEqual("drw a\r\n", run.OutputText);
        diagnostics.Assert("result", TransferResult.Success(7), run.Result);
        Assert.AreEqual(TransferResult.Success(7), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_List450_QuitsAndSucceedsWithNothing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "450 x\r\n" + Bye;
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18321/", replies);

        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/", replies);

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nTYPE A\r\nLIST\r\nQUIT\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(0), run.Result);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_List550_QuitsAndFailsWithExit19()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "550 x\r\n" + Bye;
        diagnostics.ArrangeFtp("ftp://127.0.0.1:18321/", replies);

        FtpRun run = await FtpRun.ExecuteAsync("ftp://127.0.0.1:18321/", replies);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.FtpCouldntRetrFile, "RETR response: 550");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_TransferEndsWith451_QuitsAndFailsWithExit18()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening + "451 aborted\r\n" + Bye;
        diagnostics.ArrangeFtp(Url, replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "abc");

        diagnostics.ActRun(run);
        diagnostics.DiffSent(DownloadSent, run.Sent);
        Assert.AreEqual(DownloadSent, run.Sent);
        diagnostics.Diff("output", "abc", run.OutputText);
        Assert.AreEqual("abc", run.OutputText);
        var expectedResult = TransferResult.Failure(CurlExitCode.PartialFile, "server did not report OK, got 451", 3);
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataShorterThanSize_FailsWithExit18WithoutQuit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 10\r\n" + Opening + Complete;
        diagnostics.ArrangeFtp(Url, replies, "abc");

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies, "abc");

        diagnostics.ActRun(run);
        string expectedSent = LoginSent + "EPSV\r\nTYPE I\r\nSIZE file.txt\r\nRETR file.txt\r\n";
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.PartialFile, "transfer closed with 7 bytes remaining to read", 3);
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataReadFails_FailsWithExit56()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening;
        var data = new ScriptedConnection("ab"u8.ToArray()) { FailReadsWhenExhausted = true };
        diagnostics.ArrangeFtp(Url, replies, "ab");
        diagnostics.Arrange("data connection", "fails its read once the script is exhausted");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(replies)),
            data);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.RecvError, "Failure when receiving data from the peer", 2);
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
        diagnostics.Assert("data disposed", true, data.IsDisposed);
        Assert.IsTrue(data.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesWithCount_FailsWithExit23NamingIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening;
        diagnostics.ArrangeFtp(Url, replies, "abc");
        diagnostics.Arrange("output", "refuses the write, count 1");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "abc",
            context => new TransferContext { Url = context.Url, Output = new WriteRefusingStream(new OutputWriteFailedException(1, "full")) });

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 3 returned 1");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefuses_FailsWithExit23Returned0()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening;
        diagnostics.ArrangeFtp(Url, replies, "abc");
        diagnostics.Arrange("output", "refuses the write with an IOException");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            replies,
            "abc",
            context => new TransferContext { Url = context.Url, Output = new WriteRefusingStream(new IOException("full")) });

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.WriteError, "Failure writing output to destination, passed 3 returned 0");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlClosesMidConversation_FailsWithExit56()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = Greeting + "331 Password required\r\n230 Logged in\r\n257 partial";
        diagnostics.ArrangeFtp(Url, replies);

        FtpRun run = await FtpRun.ExecuteAsync(Url, replies);

        diagnostics.ActRun(run);
        diagnostics.DiffSent(LoginSent, run.Sent);
        Assert.AreEqual(LoginSent, run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlReadFails_FailsWithExit56()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(Greeting)) { FailReadsWhenExhausted = true };
        diagnostics.ArrangeFtp(Url, Greeting);
        diagnostics.Arrange("control connection", "fails its read once the script is exhausted");

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection());

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandCannotBeSent_FailsWithExit55()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn)) { WritesBeforeFailure = 1 };
        diagnostics.ArrangeFtp(Url, LoggedIn);
        diagnostics.Arrange("control writes before failure", 1);

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection());

        diagnostics.ActRun(run);
        diagnostics.DiffSent("USER anonymous\r\n", run.Sent);
        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandSendReset_FailsWithExit55SendFailureConnectionWasReset()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, LoggedIn);
        diagnostics.Arrange("send error", System.Net.Sockets.SocketError.ConnectionReset);

        FtpRun run = await RunWithCommandSendFailingAsync(System.Net.Sockets.SocketError.ConnectionReset);

        diagnostics.ActRun(run);
        diagnostics.DiffSent("USER anonymous\r\n", run.Sent);
        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was reset");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandSendResetOffWindows_FailsWithExit55SendFailureAndTheErrorsOwnMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, LoggedIn);
        diagnostics.Arrange("send error", System.Net.Sockets.SocketError.ConnectionReset);

        FtpRun run = await RunWithCommandSendFailingAsync(System.Net.Sockets.SocketError.ConnectionReset);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.SendError, "Send failure: " + SocketMessage(System.Net.Sockets.SocketError.ConnectionReset));
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandSendAborted_FailsWithExit55SendFailureConnectionWasAborted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, LoggedIn);
        diagnostics.Arrange("send error", System.Net.Sockets.SocketError.ConnectionAborted);

        FtpRun run = await RunWithCommandSendFailingAsync(System.Net.Sockets.SocketError.ConnectionAborted);

        diagnostics.ActRun(run);
        diagnostics.DiffSent("USER anonymous\r\n", run.Sent);
        Assert.AreEqual("USER anonymous\r\n", run.Sent);
        var expectedResult = TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was aborted");
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandSendAbortedOffWindows_FailsWithExit55SendFailureAndTheErrorsOwnMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, LoggedIn);
        diagnostics.Arrange("send error", System.Net.Sockets.SocketError.ConnectionAborted);

        FtpRun run = await RunWithCommandSendFailingAsync(System.Net.Sockets.SocketError.ConnectionAborted);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.SendError, "Send failure: " + SocketMessage(System.Net.Sockets.SocketError.ConnectionAborted));
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_DataReadAborted_FailsWithExit56RecvFailureConnectionWasAbortedCountingTheBytesReceived()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening, "ab");
        diagnostics.Arrange("read error", System.Net.Sockets.SocketError.ConnectionAborted);

        FtpRun run = await RunWithDataReadFailingAsync(System.Net.Sockets.SocketError.ConnectionAborted);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.RecvError, "Recv failure: Connection was aborted", 2);
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_DataReadAbortedOffWindows_FailsWithExit56RecvFailureAndTheErrorsOwnMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeFtp(Url, LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening, "ab");
        diagnostics.Arrange("read error", System.Net.Sockets.SocketError.ConnectionAborted);

        FtpRun run = await RunWithDataReadFailingAsync(System.Net.Sockets.SocketError.ConnectionAborted);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.RecvError, "Recv failure: " + SocketMessage(System.Net.Sockets.SocketError.ConnectionAborted), 2);
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataReadFailsWithTheSocketErrorDeeperDown_KeepsTheFallbackText()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening;
        var data = new ScriptedConnection("ab"u8.ToArray())
        {
            FailReadsWhenExhausted = true,
            ReadFailure = new IOException("tls", new IOException("inner", new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.ConnectionAborted))),
        };
        diagnostics.ArrangeFtp(Url, replies, "ab");
        diagnostics.Arrange("read failure", "IOException wrapping IOException wrapping SocketException(ConnectionAborted)");

        FtpRun run = await FtpRun.ExecuteAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(replies)),
            data);

        diagnostics.ActRun(run);
        var expectedResult = TransferResult.Failure(CurlExitCode.RecvError, "Failure when receiving data from the peer", 2);
        diagnostics.Assert("result", expectedResult, run.Result);
        Assert.AreEqual(expectedResult, run.Result);
    }

    private static string SocketMessage(System.Net.Sockets.SocketError error) => new System.Net.Sockets.SocketException((int)error).Message;

    private static Task<FtpRun> RunWithCommandSendFailingAsync(System.Net.Sockets.SocketError error)
    {
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn))
        {
            WritesBeforeFailure = 1,
            WriteFailure = new IOException("failed", new System.Net.Sockets.SocketException((int)error)),
        };

        return FtpRun.ExecuteAsync(Url, control, new ScriptedConnection());
    }

    private static Task<FtpRun> RunWithDataReadFailingAsync(System.Net.Sockets.SocketError error)
    {
        var data = new ScriptedConnection("ab"u8.ToArray())
        {
            FailReadsWhenExhausted = true,
            ReadFailure = new IOException("failed", new System.Net.Sockets.SocketException((int)error)),
        };

        return FtpRun.ExecuteAsync(
            Url,
            new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Epsv + TypeSet + "213 3\r\n" + Opening)),
            data);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitCannotBeSent_StillSucceeds()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string replies = LoggedIn + Epsv + TypeSet + "213 1\r\n" + Opening + Complete;
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(replies)) { WritesBeforeFailure = 7 };
        diagnostics.ArrangeFtp(Url, replies, "x");
        diagnostics.Arrange("control writes before failure", 7);

        FtpRun run = await FtpRun.ExecuteAsync(Url, control, new ScriptedConnection("x"u8.ToArray()));

        diagnostics.ActRun(run);
        string expectedSent = DownloadSent[..^"QUIT\r\n".Length];
        diagnostics.DiffSent(expectedSent, run.Sent);
        Assert.AreEqual(expectedSent, run.Sent);
        diagnostics.Assert("result", TransferResult.Success(1), run.Result);
        Assert.AreEqual(TransferResult.Success(1), run.Result);
    }
}
