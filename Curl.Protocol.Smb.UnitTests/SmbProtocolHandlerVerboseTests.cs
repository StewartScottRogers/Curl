using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins the <c>-v</c> lines <see cref="SmbProtocolHandler" /> reports against Ubuntu's curl
/// 8.18.0, measured on 2026-10-01 with <c>Record-CurlExchange.ps1 -Script</c> (BL-598 Notes):
/// a download's <c>{ [N bytes data]</c>, a failure's text only when curl reports it through
/// <c>failf</c>, and <c>closing connection #N</c> for a failure before the session is set up
/// but <c>shutting down connection #N</c> for anything after it.
/// </summary>
[TestClass]
public sealed class SmbProtocolHandlerVerboseTests
{
    private static readonly NetworkCredential User = new("User", "Password");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_File_ReportsTheDataThenShuttingDown()
    {
        string[] transcript = await RunAsync(
            SmbRecordedExchange.DownloadUrl,
            User,
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            SmbRecordedExchange.ReadAccepted,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "<= hello world", "* shutting down connection #0" }), string.Join(" | ", transcript));
        CollectionAssert.AreEqual(new[] { "<= hello world", "* shutting down connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingFile_ReportsOnlyShuttingDown()
    {
        string[] transcript = await RunAsync(
            SmbRecordedExchange.DownloadUrl,
            User,
            SmbRecordedExchange.NegotiateResponse,
            SmbRecordedExchange.SessionSetupAccepted,
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenMissingFile,
            SmbRecordedExchange.TreeDisconnectAccepted);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "* shutting down connection #0" }), string.Join(" | ", transcript));
        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_ReportsOnlyClosing()
    {
        string[] transcript = await RunAsync(SmbRecordedExchange.DownloadUrl, null);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "* closing connection #0" }), string.Join(" | ", transcript));
        CollectionAssert.AreEqual(new[] { "* closing connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooSmallFrameForTheNegotiate_ReportsItsTextThenClosing()
    {
        string[] transcript = await RunAsync(SmbRecordedExchange.DownloadUrl, User, SmbRecordedExchange.TooSmallFrame);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "* too small NetBIOS frame size 5", "* closing connection #0" }), string.Join(" | ", transcript));
        CollectionAssert.AreEqual(new[] { "* too small NetBIOS frame size 5", "* closing connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithoutShare_ReportsItsTextThenClosingConnectionMinusOne()
    {
        string[] transcript = await RunAsync("smb://" + SmbRecordedExchange.Host + "/x.txt", User);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "* missing share in URL path for SMB", "* closing connection #-1" }), string.Join(" | ", transcript));
        CollectionAssert.AreEqual(new[] { "* missing share in URL path for SMB", "* closing connection #-1" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadFromStandardInput_ReportsItsTextThenShuttingDown()
    {
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(SmbRecordedExchange.NegotiateResponse, SmbRecordedExchange.SessionSetupAccepted);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.UploadUrl),
            Output = new MemoryStream(),
            Upload = new UnseekableStream(Encoding.ASCII.GetBytes(SmbRecordedExchange.FileContent)),
            Credentials = User,
            Events = events,
        };

        await RunHandlerAsync(connection, context);
        Diagnostics.ActTranscript(events.Transcript);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "* SMB upload needs to know the size up front", "* shutting down connection #0" }), string.Join(" | ", events.Transcript));
        CollectionAssert.AreEqual(
            new[] { "* SMB upload needs to know the size up front", "* shutting down connection #0" },
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_FileUnderNoBody_ReportsTheDataThenShuttingDownWithNoLineForExit8()
    {
        string[] transcript = await RunAsync(SmbRecordedExchange.DownloadUrl, User, DownloadReplies(), noBody: true);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "<= hello world", "* shutting down connection #0" }), string.Join(" | ", transcript));
        CollectionAssert.AreEqual(new[] { "<= hello world", "* shutting down connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_FilePastMaxFileSize_ReportsTheDataThenTheLimitThenShuttingDown()
    {
        string[] transcript = await RunAsync(SmbRecordedExchange.DownloadUrl, User, DownloadReplies(), maxFileSize: 3);

        Diagnostics.Diff("transcript", string.Join(" | ", new[] { "<= hello world", "* Exceeded the maximum allowed file size (3) with 3 bytes", "* shutting down connection #0" }), string.Join(" | ", transcript));
        CollectionAssert.AreEqual(
            new[] { "<= hello world", "* Exceeded the maximum allowed file size (3) with 3 bytes", "* shutting down connection #0" },
            transcript);
    }

    private static byte[][] DownloadReplies() =>
    [
        SmbRecordedExchange.NegotiateResponse,
        SmbRecordedExchange.SessionSetupAccepted,
        SmbRecordedExchange.TreeConnectAccepted,
        SmbRecordedExchange.OpenAccepted,
        SmbRecordedExchange.ReadAccepted,
        SmbRecordedExchange.CloseAccepted,
        SmbRecordedExchange.TreeDisconnectAccepted,
    ];

    private Task<string[]> RunAsync(string url, NetworkCredential? credentials, params byte[][] replies) =>
        RunAsync(url, credentials, replies, noBody: false);

    private async Task<string[]> RunAsync(
        string url, NetworkCredential? credentials, byte[][] replies, bool noBody = false, long? maxFileSize = null)
    {
        var events = new TranscriptTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Credentials = credentials,
            Events = events,
            NoBody = noBody,
            MaxFileSize = maxFileSize,
        };

        var connection = new ScriptedConnection(replies);
        await RunHandlerAsync(connection, context);
        Diagnostics.ActTranscript(events.Transcript);

        return [.. events.Transcript];
    }

    // Runs the handler over the scripted connection, writing the context and replies before and the result and bytes sent after.
    private async Task RunHandlerAsync(ScriptedConnection connection, TransferContext context)
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
    }

    private static SmbProtocolHandler Handler(ScriptedConnection connection) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux);
}
