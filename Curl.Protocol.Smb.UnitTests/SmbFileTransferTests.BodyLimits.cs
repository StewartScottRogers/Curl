using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smb.Fakes;

namespace Curl.Protocol.Smb;

/// <summary>
/// Pins how a download honours <c>-I</c> and <c>--max-filesize</c> as curl 8.21.0's download
/// writer does (<c>lib/sendf.c</c> <c>cw_download_write</c>, BL-1296): under <c>-I</c> the
/// first bytes read end the download with exit 8 and nothing written; past the limit the
/// bytes allowed are written and the download ends with exit 63; either way the file is
/// closed and the tree disconnected. An upload ignores both.
/// </summary>
public sealed partial class SmbFileTransferTests
{
    private const string FileContent = SmbRecordedExchange.FileContent;

    [TestMethod]
    public async Task TransferAsync_NoBody_Exits8WritingNothingAfterClosingAndDisconnecting()
    {
        var output = new MemoryStream();
        var events = new TranscriptTransferEvents();
        var connection = new ScriptedConnection(Replies());

        TransferResult result = await RunAsync(Download(connection, output, events, noBody: true), connection, SmbRecordedExchange.Host, DownloadPath());

        Diagnostics.ActOutput(output);
        Diagnostics.ActTranscript(events.Transcript);
        Diagnostics.AssertResult(CurlExitCode.WeirdServerReply, "Weird server reply", result);
        Diagnostics.Assert("output length", 0L, output.Length);
        Diagnostics.DiffSent(Requests().SelectMany(request => request).ToArray(), connection.Sent);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual("Weird server reply", result.ErrorMessage);
        Assert.AreEqual(0L, output.Length);
        CollectionAssert.AreEqual(new[] { "<= " + FileContent }, events.Transcript);
        CollectionAssert.AreEqual(Requests().SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task TransferAsync_NoBodyEmptyFile_SucceedsAsNoBytesArrive()
    {
        var connection = new ScriptedConnection(Replies(ReadResponse([])));

        TransferResult result = await RunAsync(Download(connection, noBody: true), connection, SmbRecordedExchange.Host, DownloadPath());

        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task TransferAsync_MaxFileSizeBelowAFullRead_Exits63WritingTheAllowedBytesWithoutReadingAgain()
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(Replies(ReadResponse(FullRead())));

        TransferResult result = await RunAsync(Download(connection, output, maxFileSize: 3), connection, SmbRecordedExchange.Host, DownloadPath());

        Diagnostics.ActOutput(output);
        Diagnostics.AssertResult(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (3) with 3 bytes", result);
        Diagnostics.Assert("bytes transferred", 3L, result.BytesTransferred);
        Diagnostics.Diff("output", "aaa", Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.DiffSent(Requests().SelectMany(request => request).ToArray(), connection.Sent);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        Assert.AreEqual(3L, result.BytesTransferred);
        Assert.AreEqual("aaa", Encoding.ASCII.GetString(output.ToArray()));
        CollectionAssert.AreEqual(Requests().SelectMany(request => request).ToArray(), connection.Sent);
    }

    [TestMethod]
    public async Task TransferAsync_MaxFileSizeInsideTheSecondRead_WritesTheFirstWholeAndCutsTheSecond()
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(
            SmbRecordedExchange.TreeConnectAccepted,
            SmbRecordedExchange.OpenAccepted,
            ReadResponse(FullRead()),
            SmbRecordedExchange.ReadAccepted,
            SmbRecordedExchange.CloseAccepted,
            SmbRecordedExchange.TreeDisconnectAccepted);

        TransferResult result = await RunAsync(Download(connection, output, maxFileSize: 0x8000 + 3), connection, SmbRecordedExchange.Host, DownloadPath());

        Diagnostics.Act("output length", output.Length);
        Diagnostics.AssertResult(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (32771) with 32771 bytes", result);
        Diagnostics.Assert("output length", 0x8000L + 3, output.Length);
        Diagnostics.DiffSentEnding([.. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest], connection.Sent);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (32771) with 32771 bytes", result.ErrorMessage);
        byte[] written = output.ToArray();
        Assert.AreEqual(0x8000 + 3, written.Length);
        Assert.AreEqual("hel", Encoding.ASCII.GetString(written, 0x8000, 3));
        Assert.IsTrue(connection.Sent.AsSpan().EndsWith(
            [.. SmbRecordedExchange.CloseRequest, .. SmbRecordedExchange.TreeDisconnectRequest]));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(0L)]
    [DataRow(11L)]
    public async Task TransferAsync_MaxFileSizeNoneZeroOrTheFilesSize_WritesTheWholeFile(long? maxFileSize)
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(Replies());

        TransferResult result = await RunAsync(Download(connection, output, maxFileSize: maxFileSize), connection, SmbRecordedExchange.Host, DownloadPath());

        Diagnostics.ActOutput(output);
        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Diagnostics.Diff("output", FileContent, Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(FileContent, Encoding.ASCII.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task TransferAsync_UploadUnderNoBodyAndMaxFileSize_UploadsTheWholeFile()
    {
        var connection = new ScriptedConnection(UploadReplies(SmbRecordedExchange.WriteAccepted(11)));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.UploadUrl),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes(FileContent)),
            NoBody = true,
            MaxFileSize = 3,
        };
        Diagnostics.ArrangeContext(context);

        TransferResult result = await RunAsync(
            new SmbFileTransfer(connection, new SmbMessageReader(connection, TimeProvider.System), UserId, context),
            connection,
            SmbRecordedExchange.Host,
            DownloadPath());

        Diagnostics.AssertResult(CurlExitCode.Ok, null, result);
        Diagnostics.Assert("report upload size", 11L, result.Report?.UploadSize);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(11L, result.Report!.UploadSize);
    }

    private static byte[] FullRead()
    {
        byte[] data = new byte[SmbReadRequest.MaxPayloadSize];
        data.AsSpan().Fill((byte)'a');
        return data;
    }

    private SmbFileTransfer Download(
        ScriptedConnection connection, Stream? output = null, TranscriptTransferEvents? events = null, bool noBody = false, long? maxFileSize = null)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(SmbRecordedExchange.DownloadUrl),
            Output = output ?? new MemoryStream(),
            Events = events ?? new TranscriptTransferEvents(),
            NoBody = noBody,
            MaxFileSize = maxFileSize,
        };
        Diagnostics.ArrangeContext(context);
        return new(connection, new SmbMessageReader(connection, TimeProvider.System), UserId, context);
    }
}
