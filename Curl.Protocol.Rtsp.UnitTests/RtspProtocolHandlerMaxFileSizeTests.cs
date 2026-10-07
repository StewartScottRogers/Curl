using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Rtsp.Fakes;
using Curl.Testing;
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

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthOverMaxFileSize_FailsWith63BeforeTheBodyAndClosesTheConnection()
    {
        ScriptedConnection server = Server(Bytes(Head), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, 3);

        Diagnostics.AssertExitCode(CurlExitCode.FilesizeExceeded, result);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage);
        string[] expected =
        [
            "> " + Request,
            "* Request completely sent off",
            "< RTSP/1.0 200 OK\r\n",
            "< CSeq: 1\r\n",
            "< Content-Length: 5\r\n",
            "* Maximum file size exceeded",
            "< \r\n",
            "* closing connection #0",
        ];
        Diagnostics.AssertLines("transcript", expected, transcript);
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
        Diagnostics.Assert("unread bytes, reusable", "True, False", $"{server.HasUnreadBytes}, {server.IsMarkedReusable}");
        Assert.IsTrue(server.HasUnreadBytes);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverflowingContentLengthWithMaxFileSize_FailsWith63AsTheLineIsRead()
    {
        ScriptedConnection server = Server(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999\r\n\r\n"), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, 3);

        Diagnostics.AssertExitCode(CurlExitCode.FilesizeExceeded, result);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage);
        Diagnostics.AssertLines("last 3 transcript lines", ["< CSeq: 1\r\n", "* Maximum file size exceeded", "* closing connection #0"], transcript[^3..]);
        CollectionAssert.AreEqual(
            new[] { "< CSeq: 1\r\n", "* Maximum file size exceeded", "* closing connection #0" },
            transcript[^3..]);
        Diagnostics.Assert("unread bytes, reusable", "True, False", $"{server.HasUnreadBytes}, {server.IsMarkedReusable}");
        Assert.IsTrue(server.HasUnreadBytes);
        Assert.IsFalse(server.IsMarkedReusable);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverflowingContentLengthWithMaxFileSize0_LeavesNoBodyAsBefore()
    {
        ScriptedConnection server = Server(Bytes("RTSP/1.0 200 OK\r\nCSeq: 1\r\nContent-Length: 99999999999999999999\r\n\r\n"));

        (TransferResult result, _) = await RunAsync(server, 0);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 0L, result.BytesTransferred);
        Assert.AreEqual(0, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOn404OverMaxFileSize_ReportsTheFailRefusalOnly()
    {
        ScriptedConnection server = Server(Bytes("RTSP/1.0 404 NF\r\nCSeq: 1\r\nContent-Length: 5\r\n\r\n"), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, 3, new HttpRequestOptions { Fail = HttpFailMode.Fail });

        Diagnostics.AssertExitCode(CurlExitCode.HttpReturnedError, result);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Diagnostics.AssertLines(
            "last 4 transcript lines",
            ["< Content-Length: 5\r\n", "* The requested URL returned error: 404", "< \r\n", "* closing connection #0"],
            transcript[^4..]);
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
        ScriptedConnection server = Server(Bytes(Head), Bytes("hello"));

        (TransferResult result, List<string> transcript) = await RunAsync(server, maxFileSize);

        Diagnostics.AssertExitCode(CurlExitCode.Ok, result);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 5L, result.BytesTransferred);
        Assert.AreEqual(5, result.BytesTransferred);
        Diagnostics.AssertLines("last 3 transcript lines", ["< \r\n", "{ 5", "* shutting down connection #0"], transcript[^3..]);
        CollectionAssert.AreEqual(new[] { "< \r\n", "{ 5", "* shutting down connection #0" }, transcript[^3..]);
        Assert.IsFalse(server.HasUnreadBytes);
    }

    private async Task<(TransferResult Result, List<string> Transcript)> RunAsync(ScriptedConnection server, long? maxFileSize, HttpRequestOptions? http = null)
    {
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("rtsp://127.0.0.1:47950/media"),
            Output = new MemoryStream(),
            Http = http,
            Events = events,
            MaxFileSize = maxFileSize,
        };
        Diagnostics.ArrangeContext(context);
        if (maxFileSize is null)
        {
            Diagnostics.Arrange("max file size", "(none)");
        }

        TransferResult result = await new RtspProtocolHandler(new RecordingConnector(ConnectResult.Connected(server)), new RecordingAuthenticator())
            .ExecuteAsync(context);
        Diagnostics.ActResult(result);
        Diagnostics.ActSent(server.Sent);
        Diagnostics.ActTranscript(events.Transcript);
        return (result, events.Transcript);
    }

    private ScriptedConnection Server(params byte[][] reads)
    {
        Diagnostics.ArrangeReads(reads);
        return new ScriptedConnection(reads);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
