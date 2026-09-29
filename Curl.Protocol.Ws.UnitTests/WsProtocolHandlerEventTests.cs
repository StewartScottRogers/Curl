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

    [TestMethod]
    public async Task ExecuteAsync_FrameViolation_ReportsTheFailureAndCloses()
    {
        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(Bytes(Head101 + "\x81\x85\x01\x02\x03\x04")));

        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
        Assert.AreEqual("{ 6", events.Transcript[^3]);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTheFrameSentAndTheUploadLine()
    {
        var events = new RecordingTransferEvents();
        var connection = new ScriptedConnection(Bytes(Head101 + "\x81\x02ok"));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Bytes("abc")),
            Events = events,
        };

        await Handler(connection, 0).ExecuteAsync(context);

        CollectionAssert.AreEqual(
            (string[])[.. UpgradeLines, "} 9", "* upload completely sent off: 9 bytes", "{ 4", "{ 0", "* shutting down connection #0"],
            events.Transcript);
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
