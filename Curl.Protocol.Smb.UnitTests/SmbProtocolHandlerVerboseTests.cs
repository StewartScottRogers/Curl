using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

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

        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoCredentials_ReportsOnlyClosing()
    {
        string[] transcript = await RunAsync(SmbRecordedExchange.DownloadUrl, null);

        CollectionAssert.AreEqual(new[] { "* closing connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TooSmallFrameForTheNegotiate_ReportsItsTextThenClosing()
    {
        string[] transcript = await RunAsync(SmbRecordedExchange.DownloadUrl, User, SmbRecordedExchange.TooSmallFrame);

        CollectionAssert.AreEqual(new[] { "* too small NetBIOS frame size 5", "* closing connection #0" }, transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PathWithoutShare_ReportsItsTextThenClosingConnectionMinusOne()
    {
        string[] transcript = await RunAsync("smb://" + SmbRecordedExchange.Host + "/x.txt", User);

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

        await Handler(connection).ExecuteAsync(context);

        CollectionAssert.AreEqual(
            new[] { "* SMB upload needs to know the size up front", "* shutting down connection #0" },
            events.Transcript);
    }

    private static async Task<string[]> RunAsync(string url, NetworkCredential? credentials, params byte[][] replies)
    {
        var events = new TranscriptTransferEvents();
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Credentials = credentials, Events = events };

        await Handler(new ScriptedConnection(replies)).ExecuteAsync(context);

        return [.. events.Transcript];
    }

    private static SmbProtocolHandler Handler(ScriptedConnection connection) =>
        new(new RecordingConnector(ConnectResult.Connected(connection)), SmbCurlOperatingSystem.Linux);
}
