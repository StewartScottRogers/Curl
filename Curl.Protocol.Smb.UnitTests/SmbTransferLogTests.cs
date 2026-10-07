using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins the lines an SMB transfer writes to Curl's diagnostic log under the <c>smb</c>
/// component (ADR-0222, BL-927): each reply at <c>verbose</c>, the negotiate, session setup,
/// tree connect, open and transfer end at <c>info</c>, and a refused step's NT status and
/// the failure that ends the transfer at <c>error</c>; never the password.
/// </summary>
[TestClass]
public sealed class SmbTransferLogTests
{
    private static readonly NetworkCredential User = new("User", "Password");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsEachStepAtInfo()
    {
        var log = new RecordingDiagnosticLog();

        await RunAsync(FileDownload(), Context(SmbRecordedExchange.DownloadUrl, User, log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
        Diagnostics.Assert("info lines", 5, info.Length);
        Assert.AreEqual(5, info.Length);
        Assert.AreEqual("negotiate done: dialect NT LM 0.12", info[0]);
        Assert.AreEqual("session setup done: UID 100", info[1]);
        Assert.AreEqual("tree connect to share share done: TID 7", info[2]);
        Assert.AreEqual("file opened: 11 bytes", info[3]);
        Assert.StartsWith("transfer done: 11 bytes in ", info[4]);
        Assert.EndsWith(" ms", info[4]);
        Assert.IsTrue(log.Lines.All(line => line.Component == DiagnosticLogComponents.Smb));
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsEachReplysCommandAndStatusAtVerbose()
    {
        var log = new RecordingDiagnosticLog();

        await RunAsync(FileDownload(), Context(SmbRecordedExchange.DownloadUrl, User, log));

        Diagnostics.Assert("verbose lines", 7, log.MessagesAt(DiagnosticLogLevel.Verbose).Length);
        CollectionAssert.AreEqual(
            new[]
            {
                "received SMB_COM_NEGOTIATE status 0x00000000",
                "received SMB_COM_SESSION_SETUP_ANDX status 0x00000000",
                "received SMB_COM_TREE_CONNECT_ANDX status 0x00000000",
                "received SMB_COM_NT_CREATE_ANDX status 0x00000000",
                "received SMB_COM_READ_ANDX status 0x00000000",
                "received SMB_COM_CLOSE status 0x00000000",
                "received SMB_COM_TREE_DISCONNECT status 0x00000000",
            },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_LogsTheWriteReplyAtVerbose()
    {
        var log = new RecordingDiagnosticLog();
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.UploadOpenCreated,
            SmbRecordedExchange.WriteAccepted(11),
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.UploadUrl),
            Output = new MemoryStream(),
            Upload = new MemoryStream("hello world"u8.ToArray()),
            Credentials = User,
            DiagnosticLog = log,
        };

        await RunAsync(connection, context);

        Diagnostics.Assert("verbose lines include the write reply", true, log.MessagesAt(DiagnosticLogLevel.Verbose).Contains("received SMB_COM_WRITE_ANDX status 0x00000000"));
        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "received SMB_COM_WRITE_ANDX status 0x00000000");
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextLog_IsSetOnTheConnectTarget()
    {
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        Diagnostics.Arrange("url", "smb://h/s/f, a connected connection with no replies");

        await new SmbProtocolHandler(connector).ExecuteAsync(Context("smb://h/s/f", null, log));

        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));
        Diagnostics.Assert("target log is the context log", true, ReferenceEquals(log, connector.Targets.SingleOrDefault()?.DiagnosticLog));
        Assert.AreSame(log, connector.Targets.Single().DiagnosticLog);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedTreeConnect_LogsItsStatusAndTheExitCodeAtError()
    {
        var log = new RecordingDiagnosticLog();
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectMissingShare);

        await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User, log));

        Diagnostics.Diff("error lines", "tree connect refused: status 0xC00000CC | transfer failed with RemoteFileNotFound (exit 78): Remote file not found", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Error)));
        CollectionAssert.AreEqual(
            new[]
            {
                "tree connect refused: status 0xC00000CC",
                "transfer failed with RemoteFileNotFound (exit 78): Remote file not found",
            },
            log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedOpen_LogsItsStatusAtError()
    {
        var log = new RecordingDiagnosticLog();
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenMissingFile,
            SmbRecordedExchange.TreeDisconnectAccepted);

        await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User, log));

        Diagnostics.Diff("first error line", "open refused: status 0xC0000034", log.MessagesAt(DiagnosticLogLevel.Error).FirstOrDefault() ?? string.Empty);
        Assert.AreEqual("open refused: status 0xC0000034", log.MessagesAt(DiagnosticLogLevel.Error)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedSessionSetup_LogsItsStatusAtError()
    {
        var log = new RecordingDiagnosticLog();
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupRefused);

        await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User, log));

        Diagnostics.Diff("error lines", "session setup refused: status 0xC000006D | transfer failed with LoginDenied (exit 67): Login denied", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Error)));
        CollectionAssert.AreEqual(
            new[]
            {
                "session setup refused: status 0xC000006D",
                "transfer failed with LoginDenied (exit 67): Login denied",
            },
            log.MessagesAt(DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedNegotiate_LogsItsStatusAtError()
    {
        var log = new RecordingDiagnosticLog();
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateRefused);

        await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User, log));

        Diagnostics.Diff("first error line", "negotiate refused: status 0xC0000022", log.MessagesAt(DiagnosticLogLevel.Error).FirstOrDefault() ?? string.Empty);
        Assert.AreEqual("negotiate refused: status 0xC0000022", log.MessagesAt(DiagnosticLogLevel.Error)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_RecordsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectMissingShare);

        await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User, log));

        Diagnostics.Assert("log lines", 2, log.Lines.Count);
        Diagnostics.Assert("non-error lines", 0, log.Lines.Count(line => line.Level != DiagnosticLogLevel.Error));
        Assert.HasCount(2, log.Lines);
        Assert.IsTrue(log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_ASuccessfulDownloadRecordsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await RunAsync(FileDownload(), Context(SmbRecordedExchange.DownloadUrl, User, log));

        Diagnostics.Assert("log lines", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialBearingUrl_NeverLogsThePassword()
    {
        var log = new RecordingDiagnosticLog();

        await RunAsync(
            FileDownload(),
            Context("smb://user:s3cret@" + SmbRecordedExchange.Host + "/share/dir/x.txt", new NetworkCredential("user", "s3cret"), log));

        Diagnostics.Assert("lines carrying the password", 0, log.Lines.Count(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
        Assert.IsNotEmpty(log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Received_UnknownCommand_NamesItsByte()
    {
        var log = new RecordingDiagnosticLog();
        byte[] message = SmbRecordedExchange.NegotiateResponse;
        Diagnostics.Arrange("message", "the negotiate response with its command byte set to 0x99");
        Diagnostics.Bytes("message with command 0x99", message);
        message[8] = 0x99;

        new SmbTransferLog(log).Received(message);

        Diagnostics.ActLog(log);
        Diagnostics.Diff("line", "received command 0x99 status 0x00000000", log.Lines.SingleOrDefault().Message ?? string.Empty);
        Assert.AreEqual("received command 0x99 status 0x00000000", log.Lines.Single().Message);
    }

    // Runs the handler over the scripted connection, writing the context and replies before and the result, bytes sent and log lines after.
    private async Task RunAsync(ScriptedConnection connection, TransferContext context)
    {
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReplies(connection);
        TransferResult result;
        using (Diagnostics.Phase("execute"))
        {
            result = await Handler(connection).ExecuteAsync(context);
        }

        Diagnostics.ActResult(result);
        Diagnostics.ActSent(connection);
        if (context.DiagnosticLog is RecordingDiagnosticLog log)
        {
            Diagnostics.ActLog(log);
        }
    }

    private static SmbProtocolHandler Handler(ScriptedConnection connection) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux);

    private static TransferContext Context(string url, NetworkCredential? credentials, IDiagnosticLog log) =>
        new() { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credentials, DiagnosticLog = log };

    private static ScriptedConnection FileDownload() => new(
        SmbRecordedExchange.NegotiateResponse,
        SmbRecordedExchange.SessionSetupAccepted,
        SmbRecordedExchange.TreeConnectAccepted,
        SmbRecordedExchange.OpenAccepted,
        SmbRecordedExchange.ReadAccepted,
        SmbRecordedExchange.CloseAccepted,
        SmbRecordedExchange.TreeDisconnectAccepted);
}
