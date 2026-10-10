using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Pins what an FTP or FTPS session writes to Curl's own diagnostic log, component
/// <c>ftp</c> (ADR-0222, BL-924): the failure that ends it as <c>error</c>, each fallback as
/// <c>warning</c>, each milestone as <c>info</c>, each command and reply as <c>verbose</c>,
/// and never a password or account.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerDiagnosticLogTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ftp://127.0.0.1:18321/file.txt";

    private const string Secret = "s3cret";

    private const string Greeting = "220 Recorder ready\r\n";

    private const string LoggedIn = Greeting + "331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n";

    private const string Epsv = "229 Entering Extended Passive Mode (|||61744|)\r\n";

    private const string Refused = "500 no\r\n";

    private const string TypeSet = "200 Type set\r\n";

    private const string Opening = "150 Opening BINARY mode data connection\r\n";

    private const string Complete = "226 Transfer complete\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Retrieved = TypeSet + "213 5\r\n" + Opening + Complete + Bye;

    [TestMethod]
    public async Task ExecuteAsync_PassiveDownloadAtInfo_LogsLoginDataConnectionAndTransferEnd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);

        Run run = await RunAsync(diagnostics, Url, LoggedIn + Epsv + Retrieved, log, data: "hello");

        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        string[] info = log.At(DiagnosticLogLevel.Info);
        Assert.AreEqual(4, log.Lines.Count);
        Assert.AreEqual("logged in", info[0]);
        Assert.AreEqual("passive data connection to 127.0.0.1:61744", info[1]);
        Assert.AreEqual("transfer started: RETR file.txt", info[2]);
        StringAssert.Matches(info[3], new System.Text.RegularExpressions.Regex("^transfer finished: 5 bytes in [0-9]+ ms$"));
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Ftp));
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectoryPathAtVerbose_LogsTheWalkAndTheDirectoryReached()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        await RunAsync(diagnostics, "ftp://127.0.0.1:18321/d/e/file.txt", LoggedIn + "250 OK\r\n250 OK\r\n" + Epsv + Retrieved, log, data: "hello");

        diagnostics.Assert("Verbose lines contain", "--ftp-method MultiCwd: 2 CWD", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "--ftp-method MultiCwd: 2 CWD");
        diagnostics.Assert("Info lines contain", "reached directory d/e", string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "reached directory d/e");
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvRefused_LogsAWarningForTheFallbackToPasvAndTheSkippedAddress()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Warning);

        Run run = await RunAsync(diagnostics, Url, LoggedIn + Refused + "227 Entering Passive Mode (10,0,0,9,241,32)\r\n" + Retrieved, log, data: "hello");

        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        diagnostics.Assert("warning lines", "EPSV refused with 500; falling back to PASV | --ftp-skip-pasv-ip: PASV named 10.0.0.9; using 127.0.0.1", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(
            new[] { "EPSV refused with 500; falling back to PASV", "--ftp-skip-pasv-ip: PASV named 10.0.0.9; using 127.0.0.1" },
            log.At(DiagnosticLogLevel.Warning));
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtVerbose_LogsEachCommandAndReplyButNeverThePassword()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl ftp://user:s3cret@127.0.0.1:18321/file.txt
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        await RunAsync(
            diagnostics,
            "ftp://user:" + Secret + "@127.0.0.1:18321/file.txt",
            "220-Welcome\r\n" + LoggedIn + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, Credentials = new NetworkCredential("user", Secret) },
            "hello");

        string[] verbose = log.At(DiagnosticLogLevel.Verbose);
        diagnostics.Assert("first verbose line", "reply 220: 220-Welcome", verbose[0]);
        Assert.AreEqual("reply 220: 220-Welcome", verbose[0]);
        Assert.AreEqual("sent USER user", verbose[1]);
        Assert.AreEqual("sent PASS (not logged)", verbose[3]);
        CollectionAssert.Contains(verbose, "sent QUIT");
        CollectionAssert.Contains(verbose, "reply 221: 221 Bye");
        AssertNoMessageContainsTheSecret(log);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpAccount_NeverLogsTheAccount()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl --ftp-account s3cret ftp://127.0.0.1:18321/file.txt, PASS answered 332, ACCT 530.
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        Run run = await RunAsync(
            diagnostics,
            Url,
            Greeting + "331 Password required\r\n332 Need account\r\n530 No\r\n",
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, FtpAccount = Secret });

        diagnostics.Assert("result", CurlExitCode.FtpWeirdPassReply, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.FtpWeirdPassReply, run.Result.ExitCode);
        Assert.IsTrue(log.Lines.Any(line => line.Message.Contains("ACCT", StringComparison.Ordinal)));
        AssertNoMessageContainsTheSecret(log);
    }

    [TestMethod]
    public async Task ExecuteAsync_AcctQuoteAndIgnoredRefusal_LogsTheVerbAloneAndAWarning()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        Run run = await RunAsync(
            diagnostics,
            Url,
            LoggedIn + "230 ok\r\n" + Refused + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, QuoteCommands = ["acct " + Secret, "*BOGUS x"] },
            "hello");

        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        diagnostics.Assert("Verbose lines contain", "sent acct (not logged)", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "sent acct (not logged)");
        diagnostics.Assert("Warning lines", "quote BOGUS x refused with 500; ignored", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "quote BOGUS x refused with 500; ignored" }, log.At(DiagnosticLogLevel.Warning));
        AssertNoMessageContainsTheSecret(log);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrRefusedWith550_LogsAnErrorNamingTheExitCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        Run run = await RunAsync(diagnostics, Url, LoggedIn + Epsv + TypeSet + "213 5\r\n550 No such file\r\n" + Bye, log);

        diagnostics.Assert("result", CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        string error = log.At(DiagnosticLogLevel.Error).Single();
        Assert.StartsWith("failed with RemoteFileNotFound (78): ", error);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtError_RecordsNoInfoOrVerboseLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Error);

        await RunAsync(diagnostics, Url, LoggedIn + Epsv + TypeSet + "213 5\r\n550 No such file\r\n" + Bye, log);

        diagnostics.Assert("log line count", 1, log.Lines.Count);
        Assert.AreEqual(1, log.Lines.Count);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_Success_PassesTheDiagnosticLogToTheControlAndDataConnectTargets()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);

        Run run = await RunAsync(diagnostics, Url, LoggedIn + Epsv + Retrieved, log, data: "hello");

        diagnostics.Assert("control target carries the log", true, ReferenceEquals(log, run.Connector.Targets[0].DiagnosticLog));
        Assert.AreSame(log, run.Connector.Targets[0].DiagnosticLog);
        Assert.AreSame(log, run.Connector.Targets[1].DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnectFails_LogsAnError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);
        var connector = new QueuedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));

        diagnostics.ArrangeFtp(Url);
        diagnostics.Arrange("connect result", "failed: CouldntConnect");
        TransferResult result = await new FtpProtocolHandler(connector).ExecuteAsync(Context(Url, log));
        diagnostics.ActResult(result);

        diagnostics.Assert("Error lines", "failed with CouldntConnect (7): Failed to connect", string.Join(" | ", log.At(DiagnosticLogLevel.Error)));
        CollectionAssert.AreEqual(new[] { "failed with CouldntConnect (7): Failed to connect" }, log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_LogsTheTransferStarted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);

        Run run = await RunAsync(
            diagnostics,
            Url,
            LoggedIn + Epsv + TypeSet + Opening + Complete + Bye,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, Upload = new MemoryStream(Encoding.Latin1.GetBytes("hello")) });

        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        diagnostics.Assert("Info lines contain", "transfer started: STOR file.txt", string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "transfer started: STOR file.txt");
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqd_LogsTlsOnTheControlAndDataConnections()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);
        var securedControl = Scripted("331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 P\r\n257 \"/\"\r\n" + Epsv + Retrieved);
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl), ConnectResult.Connected(Scripted("hello")));

        Run run = await RunAsync(
            diagnostics,
            Url,
            Greeting + "234 AUTH accepted\r\n",
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, SslLevel = TransportSecurityLevel.Required },
            "hello",
            tls);

        diagnostics.Assert("result", TransferResult.Success(5), run.Result);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
        string[] info = log.At(DiagnosticLogLevel.Info);
        diagnostics.Assert("info lines contain", "TLS on the control connection", string.Join(" | ", info));
        CollectionAssert.Contains(info, "TLS on the control connection");
        diagnostics.Assert("info lines contain", "PROT P accepted: data connections will be TLS", string.Join(" | ", info));
        CollectionAssert.Contains(info, "PROT P accepted: data connections will be TLS");
        diagnostics.Assert("info lines contain", "TLS on the data connection", string.Join(" | ", info));
        CollectionAssert.Contains(info, "TLS on the data connection");
    }

    [TestMethod]
    public async Task ExecuteAsync_SslAndAuthRefused_WarnsTheControlConnectionStaysPlaintext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Warning);

        await RunAsync(
            diagnostics,
            Url,
            Greeting + Refused + ScriptedConnection.NextRead + Refused + ScriptedConnection.NextRead + "331 Password required\r\n230 Logged in\r\n257 \"/\"\r\n" + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, SslLevel = TransportSecurityLevel.Try },
            "hello");

        diagnostics.Assert("warning lines", "AUTH SSL and AUTH TLS refused; the control connection stays plaintext", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(
            new[] { "AUTH SSL and AUTH TLS refused; the control connection stays plaintext" },
            log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsProtRefused_WarnsTheDataStaysPlaintext()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Warning);

        await RunAsync(
            diagnostics,
            "ftps://127.0.0.1:18321/file.txt",
            Greeting + "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n" + Refused + "257 \"/\"\r\n" + Epsv + Retrieved,
            log,
            data: "hello",
            tls: new QueuedTlsProvider());

        diagnostics.Assert("Warning lines", "PROT P refused; data connections stay plaintext", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "PROT P refused; data connections stay plaintext" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSslControl_LogsProtCAtVerbose()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Verbose);

        await RunAsync(
            diagnostics,
            "ftps://127.0.0.1:18321/file.txt",
            Greeting + "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 C\r\n257 \"/\"\r\n" + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, FtpSslControlOnly = true },
            "hello",
            new QueuedTlsProvider());

        diagnostics.Assert("Verbose lines contain", "PROT C: data connections stay plaintext", string.Join(" | ", log.At(DiagnosticLogLevel.Verbose)));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "PROT C: data connections stay plaintext");
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveEprtRefused_WarnsOfTheFallbackToPortAndLogsTheAcceptedConnection()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        diagnostics.Arrange("log level", DiagnosticLogLevel.Info);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Refused + "200 PORT ok\r\n" + Retrieved))
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55129),
        };
        var listener = new QueuedListener(ListenResult.Listening(Pending(56717)), ListenResult.Listening(Pending(56718)));
        TransferContext context = new() { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), DiagnosticLog = log, FtpPort = "-" };

        diagnostics.ArrangeFtp(Url, LoggedIn + Refused + "200 PORT ok\r\n" + Retrieved);
        diagnostics.Arrange("FtpPort", "-");

        TransferResult result = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control)), listener, new QueuedTlsProvider())
            .ExecuteAsync(context);
        diagnostics.ActResult(result);

        diagnostics.Assert("result", TransferResult.Success(5), result with { Report = null });
        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
        diagnostics.Assert("Warning lines", "EPRT refused; falling back to PORT", string.Join(" | ", log.At(DiagnosticLogLevel.Warning)));
        CollectionAssert.AreEqual(new[] { "EPRT refused; falling back to PORT" }, log.At(DiagnosticLogLevel.Warning));
        diagnostics.Assert("Info lines contain", "active data connection announced with PORT", string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "active data connection announced with PORT");
        diagnostics.Assert("Info lines contain", "active data connection accepted", string.Join(" | ", log.At(DiagnosticLogLevel.Info)));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "active data connection accepted");
    }

    private static void AssertNoMessageContainsTheSecret(RecordingDiagnosticLog log)
    {
        Assert.IsNotEmpty(log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains(Secret, StringComparison.OrdinalIgnoreCase)));
    }

    private static ScriptedPendingConnection Pending(int port) =>
        new(new IPEndPoint(IPAddress.Loopback, port), ConnectResult.Connected(Scripted("hello")));

    private static ScriptedConnection Scripted(string text) => new(Encoding.Latin1.GetBytes(text));

    private static TransferContext Context(string url, IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), DiagnosticLog = log };

    private static async Task<Run> RunAsync(
        TestDiagnostics diagnostics,
        string url,
        string replies,
        IDiagnosticLog log,
        Func<TransferContext, TransferContext>? adjust = null,
        string data = "",
        QueuedTlsProvider? tls = null)
    {
        diagnostics.ArrangeFtp(url, replies, data);
        var dataConnection = data.Length == 0 ? new ScriptedConnection() : Scripted(data);
        var connector = new QueuedConnector(ConnectResult.Connected(ScriptedConnection.FromReplies(replies)), ConnectResult.Connected(dataConnection));
        TransferContext context = Context(url, log);
        context = adjust?.Invoke(context) ?? context;
        FtpProtocolHandler handler = tls is null
            ? new FtpProtocolHandler(connector)
            : new FtpProtocolHandler(connector, new QueuedListener(), tls);

        TransferResult result = await handler.ExecuteAsync(context);
        diagnostics.ActResult(result);

        return new Run(result with { Report = null }, connector);
    }

    /// <summary>One transfer and the connector that answered it.</summary>
    private sealed record Run(TransferResult Result, QueuedConnector Connector);
}
