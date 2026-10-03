using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Rtsp.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Pins how an <c>rtsp://</c> reply meets <c>--max-filesize</c>, as curl 8.21.0 does (measured
/// with <c>Record-CurlExchange.ps1</c>, BL-1292): a <c>Content-Length</c> over the limit fails
/// with 63 at the end of the head, before the blank line is reported and before any body byte
/// is read; one too large for 64 bits fails the same way as its line is read; a limit of 0, no
/// limit and a length equal to the limit read the body as before.
/// </summary>
[TestClass]
public sealed class RtspProtocolHandlerMaxFileSizeTests
{
    private const string Request = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n";

    private const string Head = "RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthOverMaxFileSize_FailsWith63BeforeTheBodyAndClosesTheConnection()
    {
        ScriptedConnection server = new(Bytes(Head), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, 3);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "> " + Request,
                "* Request completely sent off",
                "< RTSP/1.0 200 OK\r\n",
                "< CSeq: 1\r\n",
                "< Content-Length: 5\r\n",
                "* Maximum file size exceeded",
                "< \r\n",
                "* closing connection #0",
            },
            transcript);
        Assert.IsTrue(server.HasUnreadBytes);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverflowingContentLengthWithMaxFileSize_FailsWith63AsTheLineIsRead()
    {
        ScriptedConnection server = new(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999\r\n\r\n"), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, 3);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "< CSeq: 1\r\n", "* Maximum file size exceeded", "* closing connection #0" },
            transcript[^3..]);
        Assert.IsTrue(server.HasUnreadBytes);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverflowingContentLengthWithMaxFileSize0_LeavesNoBodyAsBefore()
    {
        ScriptedConnection server = new(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999\r\n\r\n"));

        (TransferResult result, _) = await RunAsync(server, 0);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOn404OverMaxFileSize_ReportsTheFailRefusalOnly()
    {
        ScriptedConnection server = new(Bytes("RTSP/1.0 404 NF\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\n"), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, 3, new HttpRequestOptions { Fail = HttpFailMode.Fail });

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< Content-Length: 5\r\n", "* The requested URL returned error: 404", "< \r\n", "* closing connection #0" },
            transcript[^4..]);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(null)]
    [DataRow(5L)]
    public async Task ExecuteAsync_MaxFileSizeNoneOrAtTheLength_ReadsTheBody(long? maxFileSize)
    {
        ScriptedConnection server = new(Bytes(Head), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, maxFileSize);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(5, result.BytesTransferred);
        CollectionAssert.AreEqual(new[] { "< \r\n", "{ 5", "* shutting down connection #0" }, transcript[^3..]);
        Assert.IsFalse(server.HasUnreadBytes);
    }

    private static async Task<(TransferResult Result, List<string> Transcript)> RunAsync(ScriptedConnection server, long? maxFileSize, HttpRequestOptions? http = null)
    {
        var events = new RecordingTransferEvents();
        TransferResult result = await new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), new RecordingAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
                Output = new MemoryStream(),
                Http = http,
                Events = events,
                MaxFileSize = maxFileSize,
            });
        return (result, events.Transcript);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
