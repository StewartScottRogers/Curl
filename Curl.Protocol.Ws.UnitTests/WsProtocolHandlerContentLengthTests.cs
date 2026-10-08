using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_200WithOverflowingContentLength_WritesTheOverflowLineBeforeItAndRefusesWith22()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Reply = "HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello";
        diagnostics.Arrange("maxFileSize", "none");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(Reply));

        Run run = await RunAsync(Reply, maxFileSize: null);

        string[] expected =
        [
            "< HTTP/1.1 200 OK\r\n",
            "* Overflow Content-Length: value",
            "< Content-Length: 99999999999999999999\r\n",
            "* Refused WebSocket upgrade: 200",
            "< \r\n",
            "* closing connection #0",
        ];
        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Act("reply transcript", Show(run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        diagnostics.Assert("error message", "Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
        diagnostics.Assert("reply transcript", Show(expected), Show(run.ReplyTranscript));
        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
        CollectionAssert.AreEqual(expected, run.ReplyTranscript);
    }

    [TestMethod]
    public async Task ExecuteAsync_200WithOverflowingContentLengthUnderMaxFileSize_FailsWith63BeforeTheHeaderLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Reply = "HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello";
        diagnostics.Arrange("maxFileSize", 2);
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(Reply));

        Run run = await RunAsync(Reply, maxFileSize: 2);

        string[] expected = ["< HTTP/1.1 200 OK\r\n", "* Maximum file size exceeded", "* closing connection #0"];
        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Act("reply transcript", Show(run.ReplyTranscript));
        diagnostics.Act("header output", Show([run.HeaderOutput]));
        diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, run.Result.ExitCode);
        diagnostics.Assert("error message", "Maximum file size exceeded", run.Result.ErrorMessage);
        diagnostics.Assert("reply transcript", Show(expected), Show(run.ReplyTranscript));
        diagnostics.Assert("header output", Show(["HTTP/1.1 200 OK\r\n"]), Show([run.HeaderOutput]));
        diagnostics.Assert("header size", 17L, run.Result.Report!.HeaderSize);
        diagnostics.Assert("response code", 200, run.Result.Report.ResponseCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, run.Result.ExitCode);
        Assert.AreEqual("Maximum file size exceeded", run.Result.ErrorMessage);
        CollectionAssert.AreEqual(expected, run.ReplyTranscript);
        Assert.AreEqual("HTTP/1.1 200 OK\r\n", run.HeaderOutput);
        Assert.AreEqual(17L, run.Result.Report!.HeaderSize);
        Assert.AreEqual(200, run.Result.Report.ResponseCode);
    }

    [TestMethod]
    [DataRow(200, "OK")]
    [DataRow(401, "Unauthorized")]
    public async Task ExecuteAsync_ContentLengthNotANumber_FailsWith8BeforeTheHeaderLine(int status, string reason)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string reply = $"HTTP/1.1 {status} {reason}\r\nContent-Length: abc\r\n\r\n";
        diagnostics.Arrange("status line", $"{status} {reason}");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run = await RunAsync(reply, maxFileSize: null);

        string[] expected = [$"< HTTP/1.1 {status} {reason}\r\n", "* Invalid Content-Length: value", "* closing connection #0"];
        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Act("reply transcript", Show(run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("error message", "Invalid Content-Length: value", run.Result.ErrorMessage);
        diagnostics.Assert("reply transcript", Show(expected), Show(run.ReplyTranscript));
        diagnostics.Assert("header output", Show([$"HTTP/1.1 {status} {reason}\r\n"]), Show([run.HeaderOutput]));
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", run.Result.ErrorMessage);
        CollectionAssert.AreEqual(expected, run.ReplyTranscript);
        Assert.AreEqual($"HTTP/1.1 {status} {reason}\r\n", run.HeaderOutput);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondContentLengthDisagrees_FailsWith8BeforeTheSecondHeaderLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Reply = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Length: 6\r\n\r\n";
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(Reply));
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");

        Run run = await RunAsync(Reply, maxFileSize: null);

        string[] expected = ["< HTTP/1.1 200 OK\r\n", "< Content-Length: 5\r\n", "* Invalid Content-Length: value", "* closing connection #0"];
        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Act("reply transcript", Show(run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("reply transcript", Show(expected), Show(run.ReplyTranscript));
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(expected, run.ReplyTranscript);
    }

    [TestMethod]
    [DataRow("2,3")]
    [DataRow("2, ")]
    [DataRow("+2")]
    [DataRow("")]
    public async Task ExecuteAsync_ContentLengthListNotOfEqualNumbers_FailsWith8(string value)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string reply = $"HTTP/1.1 200 OK\r\ncontent-length: {value}\r\n\r\n";
        diagnostics.Arrange("content-length value", value);
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run = await RunAsync(reply, maxFileSize: null);

        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("error message", "Invalid Content-Length: value", run.Result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", run.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("2, 2", null)]
    [DataRow("5", 2L)]
    [DataRow("\t7 ,7", 0L)]
    public async Task ExecuteAsync_ContentLengthOfEqualNumbers_RefusesWith22WithoutASizeCheck(string value, long? maxFileSize)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string reply = $"HTTP/1.1 200 OK\r\nContent-Length: {value}\r\nContent-Length: {value.Trim()}\r\n\r\n";
        diagnostics.Arrange("content-length value", value);
        diagnostics.Arrange("maxFileSize", maxFileSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run = await RunAsync(reply, maxFileSize);

        bool overflowed = run.ReplyTranscript.Contains("* " + WsContentLength.OverflowValue);
        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Act("reply transcript", Show(run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        diagnostics.Assert("error message", "Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
        diagnostics.Assert("overflow line written", false, overflowed);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
        CollectionAssert.DoesNotContain(run.ReplyTranscript, "* " + WsContentLength.OverflowValue);
    }

    [TestMethod]
    public async Task ExecuteAsync_OverflowThenBadContentLength_WritesTheOverflowLineThenFailsWith8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Reply = "HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\nContent-Length: x\r\n\r\n";
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(Reply));
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");

        Run run = await RunAsync(Reply, maxFileSize: null);

        string[] expected =
        [
            "< HTTP/1.1 200 OK\r\n",
            "* Overflow Content-Length: value",
            "< Content-Length: 99999999999999999999\r\n",
            "* Invalid Content-Length: value",
            "* closing connection #0",
        ];
        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Act("reply transcript", Show(run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("reply transcript", Show(expected), Show(run.ReplyTranscript));
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(expected, run.ReplyTranscript);
    }

    [TestMethod]
    public async Task ExecuteAsync_IgnoreContentLength_DoesNotCheckItAndRefusesWith22()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string Reply = "HTTP/1.1 200 OK\r\nContent-Length: abc\r\n\r\n";
        diagnostics.Arrange("IgnoreContentLength", true);
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(Reply));

        Run run = await RunAsync(Reply, maxFileSize: null, new Curl.Protocol.Abstractions.HttpRequestOptions { IgnoreContentLength = true });

        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        diagnostics.Assert("error message", "Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        Assert.AreEqual("Refused WebSocket upgrade: 200", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithContentLengthNotANumber_SwitchesToWebSocket()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string reply = Head101.Replace("\r\n\r\n", "\r\nContent-Length: abc\r\n\r\n", StringComparison.Ordinal) + HelloThenClose;
        diagnostics.Bytes("scripted 101 head with a bad Content-Length, then frames", Encoding.Latin1.GetBytes(reply));
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");

        Run run = await RunAsync(reply, maxFileSize: null);

        diagnostics.Act("result", Describe(run.Result));
        diagnostics.Act("reply transcript", Show(run.ReplyTranscript));
        diagnostics.Act("output", run.Output);
        diagnostics.Assert("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        diagnostics.Assert("output", "hello", run.Output);
        diagnostics.Assert("transcript contains the header", true, run.ReplyTranscript.Contains("< Content-Length: abc\r\n"));
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual("hello", run.Output);
        CollectionAssert.Contains(run.ReplyTranscript, "< Content-Length: abc\r\n");
    }

    private static string Show(IEnumerable<string> lines) =>
        string.Join(" | ", lines).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Describe(TransferResult result) => $"{result.ExitCode} ({(int)result.ExitCode}): {result.ErrorMessage}";

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
