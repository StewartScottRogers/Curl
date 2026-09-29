using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the events a WebSocket transfer reports for <c>-v</c> and <c>--trace</c> against
/// curl 8.21.0, measured on 2026-09-28 with <c>Record-CurlExchange.ps1</c> (BL-584 Notes): the
/// upgrade request as one header event, the reply head one line at a time, the two
/// <c>Received 101</c> lines, every read as data received (the empty one that ends the
/// transfer included) and the closing line.
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerEventTests
{
    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n";

    private const string HelloAndClose = "\x81\x05hello\x88\x02\x03\xe8";

    private const string Request =
        "GET /p HTTP/1.1\r\n" +
        "Host: 127.0.0.1:47932\r\n" +
        "User-Agent: curl/8.21.0\r\n" +
        "Accept: */*\r\n" +
        "Upgrade: websocket\r\n" +
        "Sec-WebSocket-Version: 13\r\n" +
        "Sec-WebSocket-Key: " + FixedRandomSource.Key + "\r\n" +
        "Connection: Upgrade\r\n" +
        "\r\n";

    private static readonly string[] UpgradeLines =
    [
        "* using HTTP/1.x",
        "> " + Request,
        "* Request completely sent off",
        "< HTTP/1.1 101 Switching Protocols\r\n",
        "< Upgrade: websocket\r\n",
        "< Connection: Upgrade\r\n",
        "< Sec-WebSocket-Accept: x\r\n",
        "< \r\n",
        "* Received 101, Switching to WebSocket",
        "* [WS] Received 101, switch to WebSocket",
    ];

    [TestMethod]
    public async Task ExecuteAsync_FramesWithTheHead_ReportsThemAsOneReadThenTheEmptyRead()
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(Bytes(Head101 + HelloAndClose)));

        CollectionAssert.AreEqual(
            (string[])[.. UpgradeLines, "{ 11", "{ 0", "* shutting down connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramesAfterTheHead_ReportsEachRead()
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(Bytes(Head101), Bytes("\x81\x05hello"), Bytes("\x88\x02\x03\xe8")), connectionNumber: 2);

        CollectionAssert.AreEqual(
            (string[])[.. UpgradeLines, "{ 7", "{ 4", "{ 0", "* shutting down connection #2"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFrameAfterThe101_ReportsTheEmptyReadAndEmptyReply()
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(Bytes(Head101)));

        CollectionAssert.AreEqual(
            (string[])[.. UpgradeLines, "{ 0", "* Empty reply from server", "* shutting down connection #0"],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_Refused_ReportsTheRefusalBeforeTheBlankLineAndCloses()
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(Bytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n")));

        CollectionAssert.AreEqual(
            (string[])
            [
                .. UpgradeLines[..3],
                "< HTTP/1.1 404 Not Found\r\n",
                "< Content-Length: 0\r\n",
                "* Refused WebSocket upgrade: 404",
                "< \r\n",
                "* closing connection #0",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoReply_ReportsEmptyReplyAndCloses()
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(), connectionNumber: 1);

        CollectionAssert.AreEqual(
            (string[])[.. UpgradeLines[..3], "* Empty reply from server", "* closing connection #1"],
            events.Transcript);
    }

    /// <summary>
    /// Each violation <see cref="WsFrameDecoder" /> detects, after a whole <c>ok</c> text frame,
    /// with the lines curl 8.21.0 writes for it under <c>-sv</c> (BL-813, measured 2026-09-29).
    /// </summary>
    [TestMethod]
    [DataRow("\u000f\u0000", "{ 6", "[WS] invalid opcode: 0f")]
    [DataRow("Á\u0000", "{ 6", "[WS] invalid reserved bits: c1")]
    [DataRow("\u0080\u0000", "{ 6", "[WS] no ongoing fragmented message to resume")]
    [DataRow("\u0009\u0000", "{ 6", "[WS] invalid fragmented PING frame")]
    [DataRow("\u0081\u0080abcd", "{ 10", "[WS] masked input frame")]
    [DataRow("\u0089~\u0000\u0080", "{ 8", "[WS] received PING frame is too big")]
    [DataRow("\u0082\u007f\u0080\u0000\u0000\u0000\u0000\u0000\u0000\u0000", "{ 14", "[WS] frame length longer than 63 bits not supported")]
    public async Task ExecuteAsync_FrameViolation_ReportsTheViolationTheTwoDecodeErrorsAndCloses(string violation, string read, string message)
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(Bytes(Head101 + "\u0081\u0002ok" + violation)));

        CollectionAssert.AreEqual(
            (string[])
            [
                .. UpgradeLines,
                read,
                "* " + message,
                "* [WS] decode frame error 56",
                "* [WS] decode payload error 56",
                "* closing connection #0",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextMessageInterrupted_ReportsTheViolationTheTwoDecodeErrorsAndCloses()
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(Bytes(Head101 + "\u0001\u0002ok\u0081\u0000")));

        CollectionAssert.AreEqual(
            (string[])
            [
                .. UpgradeLines,
                "{ 6",
                "* [WS] fragmented message interrupted by new TEXT msg",
                "* [WS] decode frame error 56",
                "* [WS] decode payload error 56",
                "* closing connection #0",
            ],
            events.Transcript);
    }

    /// <summary>
    /// curl 8.21.0 writes the frame that came with the <c>101</c> head before it sends the
    /// <c>-T</c> frame, measured against a server holding the connection open (BL-813):
    /// <c>Recv data, 4 bytes</c>, <c>Send data, 10 bytes</c>,
    /// <c>upload completely sent off: 10 bytes</c>, <c>Recv data, 0 bytes</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWithAFrameAfterTheHead_ReportsTheFrameReadBeforeTheFrameSent()
    {
        RecordingTransferEvents events = await UploadAsync(new ScriptedConnection(Bytes(Head101 + "\x81\x02ok")));

        CollectionAssert.AreEqual(
            (string[])[.. UpgradeLines, "{ 4", "} 9", "* upload completely sent off: 9 bytes", "{ 0", "* shutting down connection #0"],
            events.Transcript);
    }

    /// <summary>
    /// With nothing after the <c>101</c> head, curl 8.21.0 sends the <c>-T</c> frame first,
    /// then ends with <c>Empty reply from server</c> (BL-813).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWithNothingAfterTheHead_ReportsTheFrameSentThenTheEmptyReply()
    {
        RecordingTransferEvents events = await UploadAsync(new ScriptedConnection(Bytes(Head101)));

        CollectionAssert.AreEqual(
            (string[])[.. UpgradeLines, "} 9", "* upload completely sent off: 9 bytes", "{ 0", "* Empty reply from server", "* shutting down connection #0"],
            events.Transcript);
    }

    private static async Task<RecordingTransferEvents> UploadAsync(ScriptedConnection connection)
    {
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Bytes("abc")),
            Events = events,
        };

        await Handler(connection, 0).ExecuteAsync(context);
        return events;
    }

    private static async Task<RecordingTransferEvents> RunAsync(ScriptedConnection connection, long connectionNumber = 0)
    {
        var events = new RecordingTransferEvents();
        await Handler(connection, connectionNumber).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"), Output = new MemoryStream(), Events = events });
        return events;
    }

    private static WsProtocolHandler Handler(ScriptedConnection connection, long connectionNumber) =>
        new(
            new RecordingConnector(ConnectResult.Connected(connection, null, connectionNumber: connectionNumber)),
            new RecordingAuthenticator(),
            new FixedRandomSource());

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
