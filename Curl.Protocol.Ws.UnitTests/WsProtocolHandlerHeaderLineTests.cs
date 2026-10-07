using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins how an upgrade reply's header lines are refused, against curl 8.21.0 measured on
/// 2026-10-03 with <c>Record-CurlExchange.ps1</c>, <c>-sv ws://...</c> (BL-1405): a line with no
/// colon, a NUL byte or a stray carriage return, or a second different <c>Location</c>, fails with
/// exit 8 before the line is written, in a <c>101</c> as in any other head.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerHeaderLineTests
{
    private const string HelloThenClose = "\x81\x05hello\x88\x00";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("BadHeader\r\n", "Header without colon")]
    [DataRow("X-A: a\0b\r\n", "Nul byte in header")]
    [DataRow("X-A: a\rb\r\n", "Carriage return found in header")]
    public async Task ExecuteAsync_200WithRefusedHeaderLine_FailsWith8BeforeTheLine(string line, string message)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string reply = $"HTTP/1.1 200 OK\r\n{line}Content-Length: 0\r\n\r\n";
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run;
        using (diagnostics.Phase("upgrade"))
        {
            run = await RunAsync(reply);
        }

        diagnostics.Act("exit code", $"{run.Result.ExitCode} ({run.Result.ErrorMessage})");
        diagnostics.Act("reply transcript", string.Join(" | ", run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("error message", message, run.Result.ErrorMessage);
        Assert.AreEqual(message, run.Result.ErrorMessage);
        string[] expectedTranscript = ["< HTTP/1.1 200 OK\r\n", "* " + message, "* closing connection #0"];
        diagnostics.Assert("reply transcript", string.Join(" | ", expectedTranscript), string.Join(" | ", run.ReplyTranscript));
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 200 OK\r\n", "* " + message, "* closing connection #0" },
            run.ReplyTranscript);
        diagnostics.Diff("header output", "HTTP/1.1 200 OK\r\n", run.HeaderOutput);
        Assert.AreEqual("HTTP/1.1 200 OK\r\n", run.HeaderOutput);
        diagnostics.Assert("header size", 17L, run.Result.Report!.HeaderSize);
        Assert.AreEqual(17L, run.Result.Report!.HeaderSize);
    }

    [TestMethod]
    public async Task ExecuteAsync_302WithTwoDifferentLocations_FailsWith8BeforeTheSecond()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string reply = "HTTP/1.1 302 Found\r\nLocation: /x\r\nLocation: /y\r\nContent-Length: 0\r\n\r\n";
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run;
        using (diagnostics.Phase("upgrade"))
        {
            run = await RunAsync(reply);
        }

        diagnostics.Act("exit code", $"{run.Result.ExitCode} ({run.Result.ErrorMessage})");
        diagnostics.Act("reply transcript", string.Join(" | ", run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("error message", "Multiple Location headers", run.Result.ErrorMessage);
        Assert.AreEqual("Multiple Location headers", run.Result.ErrorMessage);
        string[] expectedTranscript = ["< HTTP/1.1 302 Found\r\n", "< Location: /x\r\n", "* Multiple Location headers", "* closing connection #0"];
        diagnostics.Assert("reply transcript", string.Join(" | ", expectedTranscript), string.Join(" | ", run.ReplyTranscript));
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 302 Found\r\n", "< Location: /x\r\n", "* Multiple Location headers", "* closing connection #0" },
            run.ReplyTranscript);
        diagnostics.Diff("header output", "HTTP/1.1 302 Found\r\nLocation: /x\r\n", run.HeaderOutput);
        Assert.AreEqual("HTTP/1.1 302 Found\r\nLocation: /x\r\n", run.HeaderOutput);
    }

    [TestMethod]
    public async Task ExecuteAsync_302WithTwoEqualLocations_RefusesWith22()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string reply = "HTTP/1.1 302 Found\r\nLocation: /x\r\nlocation:\t/x \r\nContent-Length: 0\r\n\r\n";
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run = await RunAsync(reply);

        diagnostics.Act("exit code", $"{run.Result.ExitCode} ({run.Result.ErrorMessage})");
        diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, run.Result.ExitCode);
        diagnostics.Assert("error message", "Refused WebSocket upgrade: 302", run.Result.ErrorMessage);
        Assert.AreEqual("Refused WebSocket upgrade: 302", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_101WithHeaderLineWithoutColon_FailsWith8BeforeTheLine()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string reply = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nBadHeader\r\nConnection: Upgrade\r\n\r\n" + HelloThenClose;
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run;
        using (diagnostics.Phase("upgrade"))
        {
            run = await RunAsync(reply);
        }

        diagnostics.Act("exit code", $"{run.Result.ExitCode} ({run.Result.ErrorMessage})");
        diagnostics.Act("reply transcript", string.Join(" | ", run.ReplyTranscript));
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("error message", "Header without colon", run.Result.ErrorMessage);
        Assert.AreEqual("Header without colon", run.Result.ErrorMessage);
        string[] expectedTranscript = ["< HTTP/1.1 101 Switching Protocols\r\n", "< Upgrade: websocket\r\n", "* Header without colon", "* closing connection #0"];
        diagnostics.Assert("reply transcript", string.Join(" | ", expectedTranscript), string.Join(" | ", run.ReplyTranscript));
        CollectionAssert.AreEqual(
            new[] { "< HTTP/1.1 101 Switching Protocols\r\n", "< Upgrade: websocket\r\n", "* Header without colon", "* closing connection #0" },
            run.ReplyTranscript);
        diagnostics.Diff("output", string.Empty, run.Output);
        Assert.AreEqual(string.Empty, run.Output);
    }

    [TestMethod]
    public async Task ExecuteAsync_BadContentLengthBeforeRefusedLine_FailsWithTheContentLengthMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string reply = "HTTP/1.1 200 OK\r\nContent-Length: x\r\nBadHeader\r\n\r\n";
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run = await RunAsync(reply);

        diagnostics.Act("exit code", $"{run.Result.ExitCode} ({run.Result.ErrorMessage})");
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("error message", "Invalid Content-Length: value", run.Result.ErrorMessage);
        Assert.AreEqual("Invalid Content-Length: value", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_RefusedLineBeforeBadContentLength_FailsWithTheHeaderLineMessage()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string reply = "HTTP/1.1 200 OK\r\nBadHeader\r\nContent-Length: x\r\n\r\n";
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/a");
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));

        Run run = await RunAsync(reply);

        diagnostics.Act("exit code", $"{run.Result.ExitCode} ({run.Result.ErrorMessage})");
        diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        diagnostics.Assert("error message", "Header without colon", run.Result.ErrorMessage);
        Assert.AreEqual("Header without colon", run.Result.ErrorMessage);
    }

    private static async Task<Run> RunAsync(string reply)
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
        });
        string[] replyTranscript = [.. events.Transcript.SkipWhile(line => !line.StartsWith("< ", StringComparison.Ordinal))];
        return new Run(result, replyTranscript, Encoding.Latin1.GetString(headerOutput.ToArray()), Encoding.Latin1.GetString(output.ToArray()));
    }

    private sealed record Run(TransferResult Result, string[] ReplyTranscript, string HeaderOutput, string Output);
}
