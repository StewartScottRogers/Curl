using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <c>--max-filesize</c> on a WebSocket transfer against curl 8.21.0, measured on 2026-10-02
/// with <c>Record-CurlExchange.ps1</c> (BL-1294): the limit counts the raw frame bytes received
/// after the <c>101</c>, only the bytes under it are decoded, and the transfer ends with exit 63,
/// <c>Exceeded the maximum allowed file size (N) with N bytes</c>, closing the connection.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerMaxFileSizeTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: xxx\r\n\r\n";

    private const string HelloThenClose = "\x81\x05hello\x88\x00";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSize3_WritesTheOnePayloadByteUnderItAndFailsWith63()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101), Bytes(HelloThenClose));
        diagnostics.Arrange("max file size", 3L);
        diagnostics.Bytes("scripted upgrade reply", Bytes(Head101));
        diagnostics.Bytes("scripted frames", Bytes(HelloThenClose));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 3, diagnostics);

        diagnostics.Act("exit code", $"{result.ExitCode} ({result.ErrorMessage})");
        diagnostics.Act("output", output);
        diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        diagnostics.Assert("error message", "Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        diagnostics.Assert("output", "h", output);
        Assert.AreEqual("h", output);
        diagnostics.Assert("download size", 3L, result.Report!.DownloadSize);
        Assert.AreEqual(3L, result.Report!.DownloadSize);
        string[] lastThree = events.Transcript.Skip(events.Transcript.Count - 3).ToArray();
        diagnostics.Assert("last transcript lines", "{ 9 | * Exceeded the maximum allowed file size (3) with 3 bytes | * closing connection #0", string.Join(" | ", lastThree));
        CollectionAssert.AreEqual(
            new[] { "{ 9", "* Exceeded the maximum allowed file size (3) with 3 bytes", "* closing connection #0" },
            events.Transcript.Skip(events.Transcript.Count - 3).ToArray());
        int requestLength = events.Transcript.Single(line => line.StartsWith("> ", StringComparison.Ordinal)).Length - 2;
        diagnostics.Assert("bytes sent", requestLength, connection.Sent.Length);
        Assert.AreEqual(events.Transcript.Single(line => line.StartsWith("> ", StringComparison.Ordinal)).Length - 2, connection.Sent.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeExactlyTheTextFrame_WritesHelloThenTheCloseFramesFirstByteFailsWith63()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101), Bytes("\x81\x05hello"), Bytes("\x88\x00"));
        diagnostics.Arrange("max file size", 7L);
        diagnostics.Bytes("scripted text frame", Bytes("\x81\x05hello"));
        diagnostics.Bytes("scripted close frame", Bytes("\x88\x00"));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 7, diagnostics);

        diagnostics.Act("exit code", $"{result.ExitCode} ({result.ErrorMessage})");
        diagnostics.Act("output", output);
        diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        diagnostics.Assert("error message", "Exceeded the maximum allowed file size (7) with 7 bytes", result.ErrorMessage);
        Assert.AreEqual("Exceeded the maximum allowed file size (7) with 7 bytes", result.ErrorMessage);
        diagnostics.Assert("output", "hello", output);
        Assert.AreEqual("hello", output);
        diagnostics.Assert("last transcript line", "* closing connection #0", events.Transcript[^1]);
        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramesWithTheHeadOverMaxFileSize_CountsThemAndFailsWith63()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101 + HelloThenClose));
        diagnostics.Arrange("max file size", 3L);
        diagnostics.Bytes("scripted reply", Bytes(Head101 + HelloThenClose));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 3, diagnostics);

        diagnostics.Act("exit code", $"{result.ExitCode} ({result.ErrorMessage})");
        diagnostics.Act("output", output);
        diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        diagnostics.Assert("error message", "Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        diagnostics.Assert("output", "h", output);
        Assert.AreEqual("h", output);
        diagnostics.Assert("transcript contains { 0", false, events.Transcript.Contains("{ 0"));
        CollectionAssert.DoesNotContain(events.Transcript, "{ 0");
    }

    [TestMethod]
    public async Task ExecuteAsync_PingUnderMaxFileSizeInACutRead_SendsNoPong()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101), Bytes("\x89\x00\x81\x01a"));
        diagnostics.Arrange("max file size", 3L);
        diagnostics.Bytes("scripted frames", Bytes("\x89\x00\x81\x01a"));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 3, diagnostics);

        diagnostics.Act("exit code", $"{result.ExitCode} ({result.ErrorMessage})");
        diagnostics.Act("output", output);
        diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        diagnostics.Assert("output", string.Empty, output);
        Assert.AreEqual(string.Empty, output);
        int requestLength = events.Transcript.Single(line => line.StartsWith("> ", StringComparison.Ordinal)).Length - 2;
        diagnostics.Assert("bytes sent", requestLength, connection.Sent.Length);
        Assert.AreEqual(events.Transcript.Single(line => line.StartsWith("> ", StringComparison.Ordinal)).Length - 2, connection.Sent.Length);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(9L)]
    [DataRow(null)]
    public async Task ExecuteAsync_MaxFileSizeNoneOrAtTheStream_WritesHelloAndSucceeds(long? maxFileSize)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var connection = new ScriptedConnection(Bytes(Head101), Bytes(HelloThenClose));
        diagnostics.Arrange("max file size", maxFileSize);
        diagnostics.Bytes("scripted frames", Bytes(HelloThenClose));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, maxFileSize, diagnostics);

        diagnostics.Act("exit code", $"{result.ExitCode} ({result.ErrorMessage})");
        diagnostics.Act("output", output);
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        diagnostics.Assert("output", "hello", output);
        Assert.AreEqual("hello", output);
        diagnostics.Assert("download size", 9L, result.Report!.DownloadSize);
        Assert.AreEqual(9L, result.Report!.DownloadSize);
        diagnostics.Assert("last transcript line", "* shutting down connection #0", events.Transcript[^1]);
        Assert.AreEqual("* shutting down connection #0", events.Transcript[^1]);
    }

    private static async Task<(TransferResult Result, string Output, RecordingTransferEvents Events)> RunAsync(ScriptedConnection connection, long? maxFileSize, TestDiagnostics diagnostics)
    {
        var events = new RecordingTransferEvents();
        var output = new MemoryStream();
        var handler = new WsProtocolHandler(
            new RecordingConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)),
            new RecordingAuthenticator(),
            new FixedRandomSource());
        TransferResult result;
        using (diagnostics.Phase("frame exchange"))
        {
            result = await handler.ExecuteAsync(
                new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"), Output = output, Events = events, MaxFileSize = maxFileSize });
        }

        return (result, Encoding.Latin1.GetString(output.ToArray()), events);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
