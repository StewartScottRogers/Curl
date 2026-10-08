using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins the <c>smb://</c> download against curl, measured on 2026-09-29
/// (<see cref="SmbRecordedExchange" />): the bytes sent, the file written, and the exit code
/// and message of each outcome - 3 for a path with no share, 67 with no user or a refused
/// session setup, 7 for a refused negotiate, 78 for a missing share or file, 9 for a share
/// refused with ERRnoaccess, 56 for a directory - and the <c>-T</c> upload (BL-597): a new or
/// existing file, 78 for a read-only share, 25 for a refused write, 55 for <c>-T -</c>.
/// </summary>
[TestClass]
public sealed class SmbProtocolHandlerTests
{
    private const string Url = "smb://" + SmbRecordedExchange.Host + "/share/x.txt";

    private static readonly NetworkCredential User = new("User", "Password");

    private static readonly byte[] Upload = Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_AreSmbAndSmbs()
    {
        var handler = new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));
        Diagnostics.Arrange("handler", "SmbProtocolHandler over a connected scripted connection");

        string[] schemes = handler.SupportedSchemes.ToArray();
        Diagnostics.Act("supported schemes", string.Join(", ", schemes));

        Diagnostics.Assert("supported schemes", "smb, smbs", string.Join(", ", schemes));
        CollectionAssert.AreEqual(new[] { "smb", "smbs" }, schemes);
    }

    [TestMethod]
    public void Constructor_NullConnector_Throws()
    {
        Diagnostics.Arrange("connector", "null");

        var thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new SmbProtocolHandler(null!));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("thrown", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_Throws()
    {
        var handler = new SmbProtocolHandler(new RecordingConnector(ConnectResult.Connected(new ScriptedConnection())));
        Diagnostics.Arrange("context", "null");

        var thrown = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await handler.ExecuteAsync(null!));

        Diagnostics.Act("thrown", $"{thrown.GetType().Name} for {thrown.ParamName}");
        Diagnostics.Assert("thrown", nameof(ArgumentNullException), thrown.GetType().Name);
    }

    [TestMethod]
    public async Task ExecuteAsync_File_SendsCurlsSevenRequestsAndWritesTheFile()
    {
        var connection = FileDownload();
        var output = new MemoryStream();

        TransferResult result = await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User, output));

        Diagnostics.ActOutput(output);
        byte[] expectedSent = Concat(
            SmbRecordedExchange.NegotiateRequest,
            SmbRecordedExchange.SessionSetupRequest,
            SmbRecordedExchange.TreeConnectRequest,
            SmbRecordedExchange.OpenRequest,
            SmbRecordedExchange.ReadRequest,
            SmbRecordedExchange.CloseRequest,
            SmbRecordedExchange.TreeDisconnectRequest);
        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Diagnostics.Assert("bytes transferred", 11L, result.BytesTransferred);
        Diagnostics.Diff("output", SmbRecordedExchange.FileContent, Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.DiffSent(expectedSent, connection.Sent);
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(11L, result.BytesTransferred);
        Assert.AreEqual(SmbRecordedExchange.FileContent, Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(
            Concat(
                SmbRecordedExchange.NegotiateRequest,
                SmbRecordedExchange.SessionSetupRequest,
                SmbRecordedExchange.TreeConnectRequest,
                SmbRecordedExchange.OpenRequest,
                SmbRecordedExchange.ReadRequest,
                SmbRecordedExchange.CloseRequest,
                SmbRecordedExchange.TreeDisconnectRequest),
            connection.Sent);
        Assert.IsNull(result.SourceLastWriteTimeUtc);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_FileWithRemoteTime_ReturnsItsLastChangeTime()
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
            Output = new MemoryStream(),
            Credentials = User,
            RemoteTime = true,
        };
        Diagnostics.Arrange("remote time", context.RemoteTime);

        TransferResult result = await RunAsync(FileDownload(), context);

        Diagnostics.Assert("remote time", SmbRecordedExchange.FileLastChangeTimeUtc, result.SourceLastWriteTimeUtc);
        Assert.AreEqual(SmbRecordedExchange.FileLastChangeTimeUtc, result.SourceLastWriteTimeUtc);
    }

    [TestMethod]
    public async Task ExecuteAsync_DomainInUserName_SendsItAsTheDomain()
    {
        var connection = FileDownload();

        await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, new NetworkCredential(@"DOM\Us", "pw")));

        byte[] sessionSetup = connection.Sent
            .Skip(SmbRecordedExchange.NegotiateRequest.Length)
            .Take(SmbRecordedExchange.DomainSessionSetupRequest.Length)
            .ToArray();
        Diagnostics.Bytes("session setup sent", sessionSetup);
        Diagnostics.Diff("session setup sent", SmbRecordedExchange.DomainSessionSetupRequest, sessionSetup);
        CollectionAssert.AreEqual(
            SmbRecordedExchange.DomainSessionSetupRequest,
            connection.Sent
                .Skip(SmbRecordedExchange.NegotiateRequest.Length)
                .Take(SmbRecordedExchange.DomainSessionSetupRequest.Length)
                .ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingShare_Exits78AfterTheTreeConnect()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectMissingShare);

        TransferResult result = await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User));

        Diagnostics.AssertResult(CurlExitCode.RemoteFileNotFound, "Remote file not found", result);
        Diagnostics.DiffSentEnding(SmbRecordedExchange.TreeConnectRequest, connection.Sent);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, result.ExitCode);
        Assert.AreEqual("Remote file not found", result.ErrorMessage);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(SmbRecordedExchange.TreeConnectRequest));
    }

    [TestMethod]
    public async Task ExecuteAsync_ShareRefusedWithNoAccess_Exits9()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectNoAccess);

        TransferResult result = await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User));

        Diagnostics.AssertResult(CurlExitCode.RemoteAccessDenied, "Access denied to remote resource", result);
        Assert.AreEqual(CurlExitCode.RemoteAccessDenied, result.ExitCode);
        Assert.AreEqual("Access denied to remote resource", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingFile_Exits78AfterDisconnectingWithoutAClose()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenMissingFile,
            SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User));

        Diagnostics.AssertResult(CurlExitCode.RemoteFileNotFound, "Remote file not found", result);
        Diagnostics.DiffSentEnding(Concat(SmbRecordedExchange.OpenRequest, SmbRecordedExchange.TreeDisconnectRequest), connection.Sent);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, result.ExitCode);
        Assert.AreEqual("Remote file not found", result.ErrorMessage);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            Concat(SmbRecordedExchange.OpenRequest, SmbRecordedExchange.TreeDisconnectRequest)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Directory_Exits56AfterClosingAndDisconnecting()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenDirectory,
            SmbRecordedExchange.ReadRefused,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);
        var output = new MemoryStream();

        TransferResult result = await RunAsync(connection, Context(SmbRecordedExchange.DownloadUrl, User, output));

        Diagnostics.ActOutput(output);
        Diagnostics.AssertResult(CurlExitCode.RecvError, "Failure when receiving data from the peer", result);
        Diagnostics.Assert("output length", 0L, output.Length);
        Diagnostics.DiffSentEnding(
            Concat(SmbRecordedExchange.ReadRequest, SmbRecordedExchange.CloseRequest, SmbRecordedExchange.TreeDisconnectRequest), connection.Sent);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Failure when receiving data from the peer", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            Concat(SmbRecordedExchange.ReadRequest, SmbRecordedExchange.CloseRequest, SmbRecordedExchange.TreeDisconnectRequest)));
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedSessionSetup_Exits67LoginDenied()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupRefused);

        TransferResult result = await RunAsync(connection, Context(Url, new NetworkCredential("User", "Password")));

        Diagnostics.AssertResult(CurlExitCode.LoginDenied, "Login denied", result);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("Login denied", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedNegotiate_Exits7WithoutSendingASessionSetup()
    {
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateRefused);

        TransferResult result = await RunAsync(connection, Context(Url, new NetworkCredential("User", "Password")));

        Diagnostics.AssertResult(CurlExitCode.CouldntConnect, "Could not connect to server", result);
        Diagnostics.DiffSent(SmbRecordedExchange.NegotiateRequest, connection.Sent);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Could not connect to server", result.ErrorMessage);
        CollectionAssert.AreEqual(SmbRecordedExchange.NegotiateRequest, connection.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_Exits67LoginDeniedWithNothingSent()
    {
        var connection = new ScriptedConnection();

        TransferResult result = await RunAsync(connection, Context(Url, null));

        Diagnostics.AssertResult(CurlExitCode.LoginDenied, "Login denied", result);
        Diagnostics.Assert("bytes sent", 0, connection.Sent.Length);
        Diagnostics.Assert("connection disposed", true, connection.IsDisposed);
        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("Login denied", result.ErrorMessage);
        Assert.IsEmpty(connection.Sent);
        Assert.IsTrue(connection.IsDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithoutShare_Exits3BeforeConnecting()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        TransferContext context = Context("smb://h/x.txt", new NetworkCredential("User", "Password"));
        Diagnostics.ArrangeContext(context);

        TransferResult result = await new SmbProtocolHandler(connector).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.Act("connect targets", connector.Targets.Count);
        Diagnostics.AssertResult(CurlExitCode.UrlMalformat, "missing share in URL path for SMB", result);
        Diagnostics.Assert("connect targets", 0, connector.Targets.Count);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("missing share in URL path for SMB", result.ErrorMessage);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectRefused_ReturnsTheConnectorsFailure()
    {
        var connector = new RecordingConnector(ConnectResult.Refused("Failed to connect"));
        TransferContext context = Context(Url, null);
        Diagnostics.ArrangeContext(context);
        Diagnostics.Arrange("connector", "refuses with \"Failed to connect\"");

        TransferResult result = await new SmbProtocolHandler(connector).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.AssertResult(CurlExitCode.CouldntConnect, "Failed to connect", result);
        Diagnostics.Assert("connection refused", true, result.IsConnectionRefused);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
    }

    [TestMethod]
    [DataRow("smb://h/s/f", "h", 445, false)]
    [DataRow("smbs://h/s/f", "h", 445, true)]
    [DataRow("smb://h:1445/s/f", "h", 1445, false)]
    public async Task ExecuteAsync_Url_ConnectsToItsHostAndPortWithTlsForSmbs(string url, string host, int port, bool useTls)
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        TransferContext context = Context(url, null);
        Diagnostics.ArrangeContext(context);

        TransferResult result = await new SmbProtocolHandler(connector).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));
        Diagnostics.Assert("connect target", new ConnectTarget(host, port, useTls), connector.Targets.SingleOrDefault());
        Assert.AreEqual(new ConnectTarget(host, port, useTls), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_ContextWithProxy_TunnelsThroughIt()
    {
        var connector = new RecordingConnector(ConnectResult.Connected(new ScriptedConnection()));
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null);
        var context = new TransferContext { Url = CurlUrl.Parse("smb://h/s/f"), Output = new MemoryStream(), Proxy = proxy };
        Diagnostics.ArrangeContext(context);

        TransferResult result = await new SmbProtocolHandler(connector).ExecuteAsync(context);

        Diagnostics.ActResult(result);
        Diagnostics.Act("connect targets", string.Join(", ", connector.Targets));
        Diagnostics.Assert("connect target", new ConnectTarget("h", 445, false) { Proxy = proxy }, connector.Targets.SingleOrDefault());
        Assert.AreEqual(new ConnectTarget("h", 445, false) { Proxy = proxy }, connector.Targets.Single());
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_SendsCurlsRequestsAndReportsTheBytesUploaded()
    {
        var connection = FileUpload(SmbRecordedExchange.UploadOpenCreated, SmbRecordedExchange.WriteAccepted(11));
        var progress = new RecordingProgress();

        TransferResult result = await RunAsync(connection, UploadContext(new MemoryStream(Upload), progress));

        Diagnostics.Act("progress", $"started {progress.Started}, uploads [{string.Join(", ", progress.Uploads)}]");
        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Diagnostics.Assert("bytes transferred", 11L, result.BytesTransferred);
        Diagnostics.Assert("report upload size", 11L, result.Report?.UploadSize);
        Diagnostics.DiffSent(
            Concat(
                SmbRecordedExchange.NegotiateRequest,
                SmbRecordedExchange.SessionSetupRequest,
                SmbRecordedExchange.TreeConnectRequest,
                SmbRecordedExchange.UploadOpenRequest,
                SmbRecordedExchange.WriteRequest,
                SmbRecordedExchange.CloseRequest,
                SmbRecordedExchange.TreeDisconnectRequest),
            connection.Sent);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(11L, result.BytesTransferred);
        Assert.AreEqual(11L, result.Report!.UploadSize);
        Assert.AreEqual(0L, result.Report.DownloadSize);
        CollectionAssert.AreEqual(
            Concat(
                SmbRecordedExchange.NegotiateRequest,
                SmbRecordedExchange.SessionSetupRequest,
                SmbRecordedExchange.TreeConnectRequest,
                SmbRecordedExchange.UploadOpenRequest,
                SmbRecordedExchange.WriteRequest,
                SmbRecordedExchange.CloseRequest,
                SmbRecordedExchange.TreeDisconnectRequest),
            connection.Sent);
        Assert.IsTrue(progress.Started);
        CollectionAssert.AreEqual(new (long, long?)[] { (11, 11) }, progress.Uploads.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOverAnExistingFile_SendsTheSameRequests()
    {
        byte[] overwritten = SmbRecordedExchange.UploadOpenCreated;
        overwritten[44] = 3; // FILE_OVERWRITTEN
        var connection = FileUpload(overwritten, SmbRecordedExchange.WriteAccepted(11));
        Diagnostics.Arrange("open response create action", "3 (FILE_OVERWRITTEN)");

        TransferResult result = await RunAsync(connection, UploadContext(new MemoryStream(Upload)));

        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Diagnostics.DiffSentEnding(
            Concat(SmbRecordedExchange.WriteRequest, SmbRecordedExchange.CloseRequest, SmbRecordedExchange.TreeDisconnectRequest), connection.Sent);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            Concat(SmbRecordedExchange.WriteRequest, SmbRecordedExchange.CloseRequest, SmbRecordedExchange.TreeDisconnectRequest)));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToAReadOnlyShare_Exits78AfterDisconnecting()
    {
        var connection = new ScriptedConnection(
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccessDenied,
            SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await RunAsync(connection, UploadContext(new MemoryStream(Upload)));

        Diagnostics.AssertResult(CurlExitCode.RemoteFileNotFound, "Remote file not found", result);
        Diagnostics.Assert("report", "none", result.Report is null ? "none" : "present");
        Diagnostics.DiffSentEnding(Concat(SmbRecordedExchange.UploadOpenRequest, SmbRecordedExchange.TreeDisconnectRequest), connection.Sent);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, result.ExitCode);
        Assert.AreEqual("Remote file not found", result.ErrorMessage);
        Assert.IsNull(result.Report);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            Concat(SmbRecordedExchange.UploadOpenRequest, SmbRecordedExchange.TreeDisconnectRequest)));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWriteRefused_Exits25AfterClosingAndDisconnecting()
    {
        var connection = FileUpload(SmbRecordedExchange.UploadOpenCreated, SmbRecordedExchange.WriteRefused);

        TransferResult result = await RunAsync(connection, UploadContext(new MemoryStream(Upload)));

        Diagnostics.AssertResult(CurlExitCode.UploadFailed, "Upload failed (at start/before it took off)", result);
        Diagnostics.Assert("report upload size", 0L, result.Report?.UploadSize);
        Diagnostics.DiffSentEnding(
            Concat(SmbRecordedExchange.WriteRequest, SmbRecordedExchange.CloseRequest, SmbRecordedExchange.TreeDisconnectRequest), connection.Sent);
        Assert.AreEqual(CurlExitCode.UploadFailed, result.ExitCode);
        Assert.AreEqual("Upload failed (at start/before it took off)", result.ErrorMessage);
        Assert.AreEqual(0L, result.Report!.UploadSize);
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            Concat(SmbRecordedExchange.WriteRequest, SmbRecordedExchange.CloseRequest, SmbRecordedExchange.TreeDisconnectRequest)));
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFromStandardInput_Exits55AfterTheSessionSetup()
    {
        var connection = FileUpload(SmbRecordedExchange.UploadOpenCreated, SmbRecordedExchange.WriteAccepted(11));

        TransferResult result = await RunAsync(connection, UploadContext(new UnseekableStream(Upload)));

        Diagnostics.AssertResult(CurlExitCode.SendError, "SMB upload needs to know the size up front", result);
        Diagnostics.DiffSent(Concat(SmbRecordedExchange.NegotiateRequest, SmbRecordedExchange.SessionSetupRequest), connection.Sent);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual("SMB upload needs to know the size up front", result.ErrorMessage);
        CollectionAssert.AreEqual(Concat(SmbRecordedExchange.NegotiateRequest, SmbRecordedExchange.SessionSetupRequest), connection.Sent);
    }

    private static TransferContext UploadContext(Stream upload, ITransferProgress? progress = null) => new()
    {
        Url = CurlUrl.Parse(SmbRecordedExchange.UploadUrl),
        Output = new MemoryStream(),
        Upload = upload,
        Credentials = User,
        Progress = progress ?? NoTransferProgress.Instance,
    };

    private static ScriptedConnection FileUpload(byte[] openResponse, byte[] writeResponse) => new(
        SmbRecordedExchange.NegotiateResponse,
        SmbRecordedExchange.SessionSetupAccepted,
        SmbRecordedExchange.TreeConnectAccepted,
        openResponse,
        writeResponse,
        SmbRecordedExchange.CloseAccepted,
        SmbRecordedExchange.TreeDisconnectAccepted);

    private static SmbProtocolHandler Handler(ScriptedConnection connection) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux);

    // Runs the handler over the scripted connection, writing the context and replies before and the result and bytes sent after.
    private async Task<TransferResult> RunAsync(ScriptedConnection connection, TransferContext context)
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
        return result;
    }

    private static TransferContext Context(string url, NetworkCredential? credentials, Stream? output = null) =>
        new() { Url = CurlUrl.Parse(url), Output = output ?? new MemoryStream(), Credentials = credentials };

    private static ScriptedConnection FileDownload() => new(
        SmbRecordedExchange.NegotiateResponse,
        SmbRecordedExchange.SessionSetupAccepted,
        SmbRecordedExchange.TreeConnectAccepted,
        SmbRecordedExchange.OpenAccepted,
        SmbRecordedExchange.ReadAccepted,
        SmbRecordedExchange.CloseAccepted,
        SmbRecordedExchange.TreeDisconnectAccepted);

    private static byte[] Concat(params byte[][] messages) => [.. messages.SelectMany(message => message)];
}
