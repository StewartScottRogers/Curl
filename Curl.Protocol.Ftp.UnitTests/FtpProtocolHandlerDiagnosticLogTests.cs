using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ftp.Fakes;

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
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Run run = await RunAsync(Url, LoggedIn + Epsv + Retrieved, log, data: "hello");

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
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync("ftp://127.0.0.1:18321/d/e/file.txt", LoggedIn + "250 OK\r\n250 OK\r\n" + Epsv + Retrieved, log, data: "hello");

        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "--ftp-method MultiCwd: 2 CWD");
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "reached directory d/e");
    }

    [TestMethod]
    public async Task ExecuteAsync_EpsvRefused_LogsAWarningForTheFallbackToPasvAndTheSkippedAddress()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        Run run = await RunAsync(Url, LoggedIn + Refused + "227 Entering Passive Mode (10,0,0,9,241,32)\r\n" + Retrieved, log, data: "hello");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        CollectionAssert.AreEqual(
            new[] { "EPSV refused with 500; falling back to PASV", "--ftp-skip-pasv-ip: PASV named 10.0.0.9; using 127.0.0.1" },
            log.At(DiagnosticLogLevel.Warning));
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtVerbose_LogsEachCommandAndReplyButNeverThePassword()
    {
        // curl ftp://user:s3cret@127.0.0.1:18321/file.txt
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync(
            "ftp://user:" + Secret + "@127.0.0.1:18321/file.txt",
            "220-Welcome\r\n" + LoggedIn + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, Credentials = new NetworkCredential("user", Secret) },
            "hello");

        string[] verbose = log.At(DiagnosticLogLevel.Verbose);
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
        // curl --ftp-account s3cret ftp://127.0.0.1:18321/file.txt, PASS answered 332, ACCT 530.
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        Run run = await RunAsync(
            Url,
            Greeting + "331 Password required\r\n332 Need account\r\n530 No\r\n",
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, FtpAccount = Secret });

        Assert.AreEqual(CurlExitCode.FtpWeirdPassReply, run.Result.ExitCode);
        Assert.IsTrue(log.Lines.Any(line => line.Message.Contains("ACCT", StringComparison.Ordinal)));
        AssertNoMessageContainsTheSecret(log);
    }

    [TestMethod]
    public async Task ExecuteAsync_AcctQuoteAndIgnoredRefusal_LogsTheVerbAloneAndAWarning()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        Run run = await RunAsync(
            Url,
            LoggedIn + "230 ok\r\n" + Refused + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, QuoteCommands = ["acct " + Secret, "*BOGUS x"] },
            "hello");

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "sent acct (not logged)");
        CollectionAssert.AreEqual(new[] { "quote BOGUS x refused with 500; ignored" }, log.At(DiagnosticLogLevel.Warning));
        AssertNoMessageContainsTheSecret(log);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrRefusedWith550_LogsAnErrorNamingTheExitCode()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        Run run = await RunAsync(Url, LoggedIn + Epsv + TypeSet + "213 5\r\n550 No such file\r\n" + Bye, log);

        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        string error = log.At(DiagnosticLogLevel.Error).Single();
        Assert.StartsWith("failed with RemoteFileNotFound (78): ", error);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtError_RecordsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(Url, LoggedIn + Epsv + TypeSet + "213 5\r\n550 No such file\r\n" + Bye, log);

        Assert.AreEqual(1, log.Lines.Count);
        Assert.AreEqual(DiagnosticLogLevel.Error, log.Lines[0].Level);
    }

    [TestMethod]
    public async Task ExecuteAsync_Success_PassesTheDiagnosticLogToTheControlAndDataConnectTargets()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Run run = await RunAsync(Url, LoggedIn + Epsv + Retrieved, log, data: "hello");

        Assert.AreSame(log, run.Connector.Targets[0].DiagnosticLog);
        Assert.AreSame(log, run.Connector.Targets[1].DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_ControlConnectFails_LogsAnError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = new QueuedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect"));

        await new FtpProtocolHandler(connector).ExecuteAsync(Context(Url, log));

        CollectionAssert.AreEqual(new[] { "failed with CouldntConnect (7): Failed to connect" }, log.At(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_LogsTheTransferStarted()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Run run = await RunAsync(
            Url,
            LoggedIn + Epsv + TypeSet + Opening + Complete + Bye,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, Upload = new MemoryStream(Encoding.Latin1.GetBytes("hello")) });

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "transfer started: STOR file.txt");
    }

    [TestMethod]
    public async Task ExecuteAsync_SslReqd_LogsTlsOnTheControlAndDataConnections()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var securedControl = Scripted("331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 P\r\n257 \"/\"\r\n" + Epsv + Retrieved);
        var tls = new QueuedTlsProvider(ConnectResult.Connected(securedControl), ConnectResult.Connected(Scripted("hello")));

        Run run = await RunAsync(
            Url,
            Greeting + "234 AUTH accepted\r\n",
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, SslLevel = TransportSecurityLevel.Required },
            "hello",
            tls);

        Assert.AreEqual(TransferResult.Success(5), run.Result);
        string[] info = log.At(DiagnosticLogLevel.Info);
        CollectionAssert.Contains(info, "TLS on the control connection");
        CollectionAssert.Contains(info, "PROT P accepted: data connections will be TLS");
        CollectionAssert.Contains(info, "TLS on the data connection");
    }

    [TestMethod]
    public async Task ExecuteAsync_SslAndAuthRefused_WarnsTheControlConnectionStaysPlaintext()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        await RunAsync(
            Url,
            Greeting + Refused + Refused + "331 Password required\r\n230 Logged in\r\n257 \"/\"\r\n" + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, SslLevel = TransportSecurityLevel.Try },
            "hello");

        CollectionAssert.AreEqual(
            new[] { "AUTH SSL and AUTH TLS refused; the control connection stays plaintext" },
            log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsProtRefused_WarnsTheDataStaysPlaintext()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);

        await RunAsync(
            "ftps://127.0.0.1:18321/file.txt",
            Greeting + "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n" + Refused + "257 \"/\"\r\n" + Epsv + Retrieved,
            log,
            data: "hello",
            tls: new QueuedTlsProvider());

        CollectionAssert.AreEqual(new[] { "PROT P refused; data connections stay plaintext" }, log.At(DiagnosticLogLevel.Warning));
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpSslControl_LogsProtCAtVerbose()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        await RunAsync(
            "ftps://127.0.0.1:18321/file.txt",
            Greeting + "331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 C\r\n257 \"/\"\r\n" + Epsv + Retrieved,
            log,
            context => new TransferContext { Url = context.Url, Output = context.Output, DiagnosticLog = context.DiagnosticLog, FtpSslControlOnly = true },
            "hello",
            new QueuedTlsProvider());

        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Verbose), "PROT C: data connections stay plaintext");
    }

    [TestMethod]
    public async Task ExecuteAsync_ActiveEprtRefused_WarnsOfTheFallbackToPortAndLogsTheAcceptedConnection()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var control = new ScriptedConnection(Encoding.Latin1.GetBytes(LoggedIn + Refused + "200 PORT ok\r\n" + Retrieved))
        {
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 55129),
        };
        var listener = new QueuedListener(ListenResult.Listening(Pending(56717)), ListenResult.Listening(Pending(56718)));
        TransferContext context = new() { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), DiagnosticLog = log, FtpPort = "-" };

        TransferResult result = await new FtpProtocolHandler(new QueuedConnector(ConnectResult.Connected(control)), listener, new QueuedTlsProvider())
            .ExecuteAsync(context);

        Assert.AreEqual(TransferResult.Success(5), result with { Report = null });
        CollectionAssert.AreEqual(new[] { "EPRT refused; falling back to PORT" }, log.At(DiagnosticLogLevel.Warning));
        CollectionAssert.Contains(log.At(DiagnosticLogLevel.Info), "active data connection announced with PORT");
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
        string url,
        string replies,
        IDiagnosticLog log,
        Func<TransferContext, TransferContext>? adjust = null,
        string data = "",
        QueuedTlsProvider? tls = null)
    {
        var dataConnection = data.Length == 0 ? new ScriptedConnection() : Scripted(data);
        var connector = new QueuedConnector(ConnectResult.Connected(Scripted(replies)), ConnectResult.Connected(dataConnection));
        TransferContext context = Context(url, log);
        context = adjust?.Invoke(context) ?? context;
        FtpProtocolHandler handler = tls is null
            ? new FtpProtocolHandler(connector)
            : new FtpProtocolHandler(connector, new QueuedListener(), tls);

        TransferResult result = await handler.ExecuteAsync(context);

        return new Run(result with { Report = null }, connector);
    }

    /// <summary>One transfer and the connector that answered it.</summary>
    private sealed record Run(TransferResult Result, QueuedConnector Connector);
}
