using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins how a reply refusing the upgrade has its <c>Content-Length</c> checked, against curl
/// 8.21.0 measured on 2026-10-03 with <c>Record-CurlExchange.ps1</c>, <c>-sv ws://...</c> (BL-1404):
/// a bad or disagreeing value fails with exit 8 before its header line, a number too large for
/// 64 bits writes <c>Overflow Content-Length: value</c> before its line, or fails with exit 63
/// under <c>--max-filesize</c>, and a <c>101</c> is not checked.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerContentLengthTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: xxx\r\n\r\n";

    private const string HelloThenClose = "\x81\x05hello\x88\x00";

    [TestMethod]
    public async Task ExecuteAsync_200WithOverflowingContentLength_WritesTheOverflowLineBeforeItAndRefusesWith22()
    {
        Run run = await RunAsync("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello", maxFileSize: null);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "< HTTP/1.1 200 OK\r\n",
                "* Overflow Content-Length: value",
                "< Content-Length: 99999999999999999999\r\n",
                "* Refused WebSocket upgrade: 200",
                "< \r\n",
                "* closing connection #0",
            },
            run.ReplyTranscript);
    }

    [TestMethod]
    public async Task ExecuteAsync_200WithOverflowingContentLengthUnderMaxFileSize_FailsWith63BeforeTheHeaderLine()
    {
        Run run = await RunAsync("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello", maxFileSize: 2);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, run.Result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", run.Result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 200 OK\r\n", "* Maximum file size exceeded", "* closing connection #0" },
            run.ReplyTranscript);
        Assert.AreEqual("HTTP/1.1 200 OK\r\n", run.HeaderOutput);
        Assert.AreEqual(17L, run.Result.Report!.HeaderSize);
        Assert.AreEqual(200, run.Result.Report.ResponseCode);
    }

    [TestMethod]
    [DataRow(200, "OK")]
    [DataRow(401, "Unauthorized")]
    public async Task ExecuteAsync_ContentLengthNotANumber_FailsWith8BeforeTheHeaderLine(int status, string reason)
    {
        Run run = await RunAsync($"HTTP/1.1 {status} {reason}\r\nContent-Length: abc\r\n\r\n", maxFileSize: null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", run.Result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[] { $"< HTTP/1.1 {status} {reason}\r\n", "* Invalid Content-Length: value", "* closing connection #0" },
            run.ReplyTranscript);
        Assert.AreEqual($"HTTP/1.1 {status} {reason}\r\n", run.HeaderOutput);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondContentLengthDisagrees_FailsWith8BeforeTheSecondHeaderLine()
    {
        Run run = await RunAsync("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 6\r\n\r\n", maxFileSize: null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 200 OK\r\n", "< Content-Length: 5\r\n", "* Invalid Content-Length: value", "* closing connection #0" },
            run.ReplyTranscript);
    }

    [TestMethod]
    [DataRow("2,3")]
    [DataRow("2, ")]
    [DataRow("+2")]
    [DataRow("")]
    public async Task ExecuteAsync_ContentLengthListNotOfEqualNumbers_FailsWith8(string value)
    {
        Run run = await RunAsync($"HTTP/1.1 200 OK\r\ncontent-length: {value}\r\n\r\n", maxFileSize: null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", run.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("2, 2", null)]
    [DataRow("5", 2L)]
    [DataRow("\t7 ,7", 0L)]
    public async Task ExecuteAsync_ContentLengthOfEqualNumbers_RefusesWith22WithoutASizeCheck(string value, long? maxFileSize)
    {
        Run run = await RunAsync($"HTTP/1.1 200 OK\r\nContent-Length: {value}\r\nContent-Length: {value.Trim()}\r\n\r\n", maxFileSize);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
        CollectionAssert.DoesNotContain(run.ReplyTranscript, "* " + WsContentLength.OverflowValue);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverflowThenBadContentLength_WritesTheOverflowLineThenFailsWith8()
    {
        Run run = await RunAsync("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\nContent-Length: x\r\n\r\n", maxFileSize: null);

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "< HTTP/1.1 200 OK\r\n",
                "* Overflow Content-Length: value",
                "< Content-Length: 99999999999999999999\r\n",
                "* Invalid Content-Length: value",
                "* closing connection #0",
            },
            run.ReplyTranscript);
    }

    [TestMethod]
    public async Task ExecuteAsync_IgnoreContentLength_DoesNotCheckItAndRefusesWith22()
    {
        Run run = await RunAsync("HTTP/1.1 200 OK\r\nContent-Length: abc\r\n\r\n", maxFileSize: null, new Curl.Protocol.Abstractions.HttpRequestOptions { IgnoreContentLength = true });

        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithContentLengthNotANumber_SwitchesToWebSocket()
    {
        Run run = await RunAsync(
            Head101.Replace("\r\n\r\n", "\r\nContent-Length: abc\r\n\r\n", StringComparison.Ordinal) + HelloThenClose,
            maxFileSize: null);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual("hello", run.Output);
        CollectionAssert.Contains(run.ReplyTranscript, "< Content-Length: abc\r\n");
    }

    private static async Task<Run> RunAsync(string reply, long? maxFileSize, Curl.Protocol.Abstractions.HttpRequestOptions? http = null)
    {
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(reply));
        var events = new RecordingTransferEvents();
        var output = new MemoryStream();
        var headerOutput = new MemoryStream();
        var handler = new WsProtocolHandler(
            new RecordingConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)),
            new RecordingAuthenticator(),
            new FixedRandomSource());
        TransferResult result = await handler.ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47932/a"),
            Output = output,
            HeaderOutput = headerOutput,
            Events = events,
            MaxFileSize = maxFileSize,
            Http = http,
        });
        string[] replyTranscript = [.. events.Transcript.SkipWhile(line => !line.StartsWith("< ", StringComparison.Ordinal))];
        return new Run(result, replyTranscript, Encoding.Latin1.GetString(headerOutput.ToArray()), Encoding.Latin1.GetString(output.ToArray()));
    }

    private sealed record Run(TransferResult Result, string[] ReplyTranscript, string HeaderOutput, string Output);
}
