using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;

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

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSize3_WritesTheOnePayloadByteUnderItAndFailsWith63()
    {
        var connection = new ScriptedConnection(Bytes(Head101), Bytes(HelloThenClose));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 3);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        Assert.AreEqual("h", output);
        Assert.AreEqual(3L, result.Report!.DownloadSize);
        CollectionAssert.AreEqual(
            new[] { "{ 9", "* Exceeded the maximum allowed file size (3) with 3 bytes", "* closing connection #0" },
            events.Transcript.Skip(events.Transcript.Count - 3).ToArray());
        Assert.AreEqual(events.Transcript.Single(line => line.StartsWith("> ", StringComparison.Ordinal)).Length - 2, connection.Sent.Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeExactlyTheTextFrame_WritesHelloThenTheCloseFramesFirstByteFailsWith63()
    {
        var connection = new ScriptedConnection(Bytes(Head101), Bytes("\x81\x05hello"), Bytes("\x88\x00"));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 7);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (7) with 7 bytes", result.ErrorMessage);
        Assert.AreEqual("hello", output);
        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramesWithTheHeadOverMaxFileSize_CountsThemAndFailsWith63()
    {
        var connection = new ScriptedConnection(Bytes(Head101 + HelloThenClose));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 3);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        Assert.AreEqual("h", output);
        CollectionAssert.DoesNotContain(events.Transcript, "{ 0");
    }

    [TestMethod]
    public async Task ExecuteAsync_PingUnderMaxFileSizeInACutRead_SendsNoPong()
    {
        var connection = new ScriptedConnection(Bytes(Head101), Bytes("\x89\x00\x81\x01a"));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, 3);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(events.Transcript.Single(line => line.StartsWith("> ", StringComparison.Ordinal)).Length - 2, connection.Sent.Length);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(9L)]
    [DataRow(null)]
    public async Task ExecuteAsync_MaxFileSizeNoneOrAtTheStream_WritesHelloAndSucceeds(long? maxFileSize)
    {
        var connection = new ScriptedConnection(Bytes(Head101), Bytes(HelloThenClose));

        (TransferResult result, string output, RecordingTransferEvents events) = await RunAsync(connection, maxFileSize);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", output);
        Assert.AreEqual(9L, result.Report!.DownloadSize);
        Assert.AreEqual("* shutting down connection #0", events.Transcript[^1]);
    }

    private static async Task<(TransferResult Result, string Output, RecordingTransferEvents Events)> RunAsync(ScriptedConnection connection, long? maxFileSize)
    {
        var events = new RecordingTransferEvents();
        var output = new MemoryStream();
        var handler = new WsProtocolHandler(
            new RecordingConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)),
            new RecordingAuthenticator(),
            new FixedRandomSource());
        TransferResult result = await handler.ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"), Output = output, Events = events, MaxFileSize = maxFileSize });
        return (result, Encoding.Latin1.GetString(output.ToArray()), events);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
