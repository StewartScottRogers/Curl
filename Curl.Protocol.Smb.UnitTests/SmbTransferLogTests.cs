using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

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

    [TestMethod]
    public async Task ExecuteAsync_Download_LogsEachStepAtInfo()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(FileDownload()).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

        string[] info = log.MessagesAt(DiagnosticLogLevel.Info);
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

        await Handler(FileDownload()).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

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

        await Handler(connection).ExecuteAsync(context);

        CollectionAssert.Contains(log.MessagesAt(DiagnosticLogLevel.Verbose), "received SMB_COM_WRITE_ANDX status 0x00000000");
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextLog_IsSetOnTheConnectTarget()
    {
        var log = new RecordingDiagnosticLog();
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));

        await new SmbProtocolHandler(connector).ExecuteAsync(Context("smb://h/s/f", null, log));

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

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

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

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

        Assert.AreEqual("open refused: status 0xC0000034", log.MessagesAt(DiagnosticLogLevel.Error)[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedSessionSetup_LogsItsStatusAtError()
    {
        var log = new RecordingDiagnosticLog();
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupRefused);

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

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

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

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

        await Handler(connection).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

        Assert.HasCount(2, log.Lines);
        Assert.IsTrue(log.Lines.All(line => line.Level == DiagnosticLogLevel.Error));
    }

    [TestMethod]
    public async Task ExecuteAsync_AtErrorLevel_ASuccessfulDownloadRecordsNothing()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);

        await Handler(FileDownload()).ExecuteAsync(Context(SmbRecordedExchange.DownloadUrl, User, log));

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_CredentialBearingUrl_NeverLogsThePassword()
    {
        var log = new RecordingDiagnosticLog();

        await Handler(FileDownload()).ExecuteAsync(
            Context("smb://user:s3cret@" + SmbRecordedExchange.Host + "/share/dir/x.txt", new NetworkCredential("user", "s3cret"), log));

        Assert.IsNotEmpty(log.Lines);
        Assert.IsFalse(log.Lines.Any(line => line.Message.Contains("s3cret", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Received_UnknownCommand_NamesItsByte()
    {
        var log = new RecordingDiagnosticLog();
        byte[] message = SmbRecordedExchange.NegotiateResponse;
        message[8] = 0x99;

        new SmbTransferLog(log).Received(message);

        Assert.AreEqual("received command 0x99 status 0x00000000", log.Lines.Single().Message);
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
