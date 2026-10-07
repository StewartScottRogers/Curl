using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ExecuteAsync_FramesWithTheHead_ReportsThemAsOneReadThenTheEmptyRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Bytes(Head101 + HelloAndClose);
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/p");
        diagnostics.Bytes("scripted 101 head and frames", scripted);

        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(scripted));

        string[] expected = [.. UpgradeLines, "{ 11", "{ 0", "* shutting down connection #0"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_ReportsTheFramesWithTheHeadAsOneReadThenEmptyReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        // curl -v -I (BL-788): "{ [11 bytes data]", "* Empty reply from server", then shutting down.
        var events = new RecordingTransferEvents();
        byte[] scripted = Bytes(Head101 + HelloAndClose);
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/p");
        diagnostics.Arrange("NoBody", true);
        diagnostics.Bytes("scripted 101 head and frames", scripted);
        await Handler(new ScriptedConnection(scripted), 0).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"), Output = new MemoryStream(), Events = events, NoBody = true });

        string[] expected = [.. UpgradeLines.Select(line => line.Replace("> GET ", "> HEAD ", StringComparison.Ordinal)), "{ 11", "* Empty reply from server", "* shutting down connection #0"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBodyAndNothingWithTheHead_ReportsNoRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        byte[] scripted = Bytes(Head101);
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/p");
        diagnostics.Arrange("NoBody", true);
        diagnostics.Bytes("scripted 101 head", scripted);
        await Handler(new ScriptedConnection(scripted), 0).ExecuteAsync(
            new TransferContext { Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"), Output = new MemoryStream(), Events = events, NoBody = true });

        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("contains an empty read", false, events.Transcript.Contains("{ 0"));
        diagnostics.Assert("second to last event", "* Empty reply from server", events.Transcript[^2]);
        CollectionAssert.DoesNotContain(events.Transcript, "{ 0");
        Assert.AreEqual("* Empty reply from server", events.Transcript[^2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_FramesAfterTheHead_ReportsEachRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] head = Bytes(Head101);
        byte[] text = Bytes("\x81\x05hello");
        byte[] close = Bytes("\x88\x02\x03\xe8");
        diagnostics.Arrange("connectionNumber", 2);
        diagnostics.Bytes("scripted 101 head", head);
        diagnostics.Bytes("scripted text frame", text);
        diagnostics.Bytes("scripted close frame", close);

        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(head, text, close), connectionNumber: 2);

        string[] expected = [.. UpgradeLines, "{ 7", "{ 4", "{ 0", "* shutting down connection #2"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoFrameAfterThe101_ReportsTheEmptyReadAndEmptyReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Bytes(Head101);
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/p");
        diagnostics.Bytes("scripted 101 head", scripted);

        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(scripted));

        string[] expected = [.. UpgradeLines, "{ 0", "* Empty reply from server", "* shutting down connection #0"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_Refused_ReportsTheRefusalBeforeTheBlankLineAndCloses()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Bytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n");
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/p");
        diagnostics.Bytes("scripted refusal", scripted);

        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(scripted));

        string[] expected =
        [
            .. UpgradeLines[..3],
            "< HTTP/1.1 404 Not Found\r\n",
            "< Content-Length: 0\r\n",
            "* Refused WebSocket upgrade: 404",
            "< \r\n",
            "* closing connection #0",
        ];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoReply_ReportsEmptyReplyAndCloses()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/p");
        diagnostics.Arrange("scripted reply", "nothing");
        diagnostics.Arrange("connectionNumber", 1);

        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(), connectionNumber: 1);

        string[] expected = [.. UpgradeLines[..3], "* Empty reply from server", "* closing connection #1"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    /// <summary>
    /// Each violation <see cref="WsFrameDecoder" /> detects, after a whole <c>ok</c> text frame,
    /// with the lines curl 8.21.0 writes for it under <c>-sv</c> (BL-813, measured 2026-09-29).
    /// </summary>
    /// <param name="violation">The bytes after the text frame.</param>
    /// <param name="read">The data-received event for the whole read.</param>
    /// <param name="message">The violation's message.</param>
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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Bytes(Head101 + "\u0081\u0002ok" + violation);
        diagnostics.Arrange("expected violation", message);
        diagnostics.Bytes("scripted 101 head, text frame and violation", scripted);

        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(scripted));

        string[] expected =
        [
            .. UpgradeLines,
            read,
            "* " + message,
            "* [WS] decode frame error 56",
            "* [WS] decode payload error 56",
            "* closing connection #0",
        ];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_TextMessageInterrupted_ReportsTheViolationTheTwoDecodeErrorsAndCloses()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Bytes(Head101 + "\u0001\u0002ok\u0081\u0000");
        diagnostics.Arrange("url", "ws://127.0.0.1:47932/p");
        diagnostics.Bytes("scripted 101 head and interrupted text message", scripted);

        RecordingTransferEvents events = await RunAsync(new ScriptedConnection(scripted));

        string[] expected =
        [
            .. UpgradeLines,
            "{ 6",
            "* [WS] fragmented message interrupted by new TEXT msg",
            "* [WS] decode frame error 56",
            "* [WS] decode payload error 56",
            "* closing connection #0",
        ];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Bytes(Head101 + "\x81\x02ok");
        diagnostics.Arrange("upload", "abc");
        diagnostics.Bytes("scripted 101 head and frame", scripted);

        RecordingTransferEvents events = await UploadAsync(new ScriptedConnection(scripted));

        string[] expected = [.. UpgradeLines, "{ 4", "} 9", "* upload completely sent off: 9 bytes", "{ 0", "* shutting down connection #0"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    /// <summary>
    /// With nothing after the <c>101</c> head, curl 8.21.0 sends the <c>-T</c> frame first,
    /// then ends with <c>Empty reply from server</c> (BL-813).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_UploadWithNothingAfterTheHead_ReportsTheFrameSentThenTheEmptyReply()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] scripted = Bytes(Head101);
        diagnostics.Arrange("upload", "abc");
        diagnostics.Bytes("scripted 101 head", scripted);

        RecordingTransferEvents events = await UploadAsync(new ScriptedConnection(scripted));

        string[] expected = [.. UpgradeLines, "} 9", "* upload completely sent off: 9 bytes", "{ 0", "* Empty reply from server", "* shutting down connection #0"];
        diagnostics.Act("events", Show(events.Transcript));
        diagnostics.Assert("events", Show(expected), Show(events.Transcript));
        CollectionAssert.AreEqual(expected, events.Transcript);
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

    private static string Show(IEnumerable<string> lines) =>
        string.Join(" | ", lines).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
