using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ws.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins the <c>--trace-config ws</c> lines a WebSocket transfer writes among its <c>-v</c> events
/// against curl 8.21.0 (Schannel), measured on 2026-10-02 with <c>Record-CurlExchange.ps1</c>
/// against a <c>101</c> head followed in the same read by the frames each test names (BL-1164 Notes).
/// </summary>
[TestClass]
public sealed class WsProtocolHandlerFrameTraceTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string Url = "ws://127.0.0.1:47932/p";

    private const string Head101 =
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: x\r\n\r\n";

    private static readonly string[] SwitchLines =
    [
        "* Received 101, Switching to WebSocket",
        "* [WS] WS, using chunk size 65535",
        "* [WS] Received 101, switch to WebSocket",
    ];

    [TestMethod]
    public async Task ExecuteAsync_TextThenEmptyClose_WritesTheMeasuredLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101 + "\x81\x02hi\x88\x00"));

        string[] expected =
            [
                .. SwitchLines,
                "{ 6",
                "* [WS] decoded decoded [TEXT payload=0/2]",
                "* [WS] passed 2 bytes payload, 0 remain",
                "* [WS] decoded passing [TEXT payload=2/2]",
                "* [WS] decoded decoded [CLOSE payload=0/0]",
                "* [WS] websocket established, callback mode",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_BinaryThenCloseWithACode_WritesTheMeasuredLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101 + "\x82\u0003abc\x88\x02\x03\xe8"));

        string[] expected =
            [
                .. SwitchLines,
                "{ 9",
                "* [WS] decoded decoded [BIN payload=0/3]",
                "* [WS] passed 3 bytes payload, 0 remain",
                "* [WS] decoded passing [BIN payload=3/3]",
                "* [WS] decoded decoded [CLOSE payload=0/2]",
                "* [WS] passed 2 bytes payload, 0 remain",
                "* [WS] decoded passing [CLOSE payload=2/2]",
                "* [WS] websocket established, callback mode",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_FragmentedText_NamesTheNonFinalFragmentAndTheContinuation()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101 + "\u0001\u0001a\x80\u0001b"));

        string[] expected =
            [
                .. SwitchLines,
                "{ 6",
                "* [WS] decoded decoded [TEXT NON-FINAL payload=0/1]",
                "* [WS] passed 1 bytes payload, 0 remain",
                "* [WS] decoded passing [TEXT NON-FINAL payload=1/1]",
                "* [WS] decoded decoded [CONT payload=0/1]",
                "* [WS] passed 1 bytes payload, 0 remain",
                "* [WS] decoded passing [CONT payload=1/1]",
                "* [WS] websocket established, callback mode",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoPingsInOneRead_WritesAnAutoPongForEachAndTheOnePongSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101 + "\x89\x01p\x89\x02qq"));

        string[] expected =
            [
                .. SwitchLines,
                "{ 7",
                "* [WS] decoded decoded [PING payload=0/1]",
                "* [WS] auto PONG to [PING payload=0/1]",
                "* [WS] passed 1 bytes payload, 0 remain",
                "* [WS] decoded passing [PING payload=1/1]",
                "* [WS] decoded decoded [PING payload=0/2]",
                "* [WS] auto PONG to [PING payload=0/2]",
                "* [WS] passed 2 bytes payload, 0 remain",
                "* [WS] decoded passing [PING payload=2/2]",
                "* [WS] WS-ENC: sending [PONG payload=0/2]",
                "* [WS] WS-ENC: buffered [PONG payload=2/2]",
                "* [WS] flushed 8 bytes",
                "* [WS] websocket established, callback mode",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPingThenText_WritesTheEmptyPongLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101 + "\x89\x00\x81\x01z"));

        string[] expected =
            [
                .. SwitchLines,
                "{ 5",
                "* [WS] decoded decoded [PING payload=0/0]",
                "* [WS] auto PONG to [PING payload=0/0]",
                "* [WS] decoded decoded [TEXT payload=0/1]",
                "* [WS] passed 1 bytes payload, 0 remain",
                "* [WS] decoded passing [TEXT payload=1/1]",
                "* [WS] WS-ENC: sending [PONG payload=0/0]",
                "* [WS] WS-ENC: buffered [PONG payload=0/0]",
                "* [WS] flushed 6 bytes",
                "* [WS] websocket established, callback mode",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_NothingAfterThe101_WritesEstablishedBeforeTheEmptyRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101));

        string[] expected =
            [
                .. SwitchLines,
                "* [WS] websocket established, callback mode",
                "{ 0",
                "* Empty reply from server",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_WritesTheReaderLineAndTheEncodedFrameBeforeItIsSent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        diagnostics.Arrange("upload", "hello");
        diagnostics.Bytes("server reply", Bytes(Head101 + "\x81\x02hi"));
        await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02hi"))).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Bytes("hello")),
            Events = events,
        });

        string[] expected =
            [
                .. SwitchLines,
                "* [WS] UPLOAD set, add ws-encode reader",
                "{ 4",
                "* [WS] decoded decoded [TEXT payload=0/2]",
                "* [WS] passed 2 bytes payload, 0 remain",
                "* [WS] decoded passing [TEXT payload=2/2]",
                "* [WS] websocket established, callback mode",
                "* [WS] WS-ENC: sending [BIN payload=0/5]",
                "* [WS] WS-ENC: buffered [BIN payload=5/5]",
                "} 11",
                "* upload completely sent off: 11 bytes",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_WritesEstablishedAfterTheUndecodedRead()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        diagnostics.Arrange("no body", true);
        diagnostics.Bytes("server reply", Bytes(Head101 + "\x81\x02hi"));
        await Handler(new ScriptedConnection(Bytes(Head101 + "\x81\x02hi"))).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"),
            Output = new MemoryStream(),
            Events = events,
            NoBody = true,
        });

        string[] expected =
            [
                .. SwitchLines,
                "{ 4",
                "* [WS] websocket established, callback mode",
                "* Empty reply from server",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    /// <summary>
    /// A frame whose head ends a read is written as passing with none of its payload passed, as
    /// curl 8.21.0's decoder falls through from the head to the payload; the payload's later reads
    /// each write what they passed.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_PayloadSplitAcrossReads_WritesEachRunPassed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101), Bytes("\x81\x03"), Bytes("a"), Bytes("bc"));

        string[] expected =
            [
                .. SwitchLines,
                "* [WS] websocket established, callback mode",
                "{ 2",
                "* [WS] decoded decoded [TEXT payload=0/3]",
                "* [WS] decoded passing [TEXT payload=0/3]",
                "{ 1",
                "* [WS] passed 1 bytes payload, 2 remain",
                "* [WS] decoded passing [TEXT payload=1/3]",
                "{ 2",
                "* [WS] passed 2 bytes payload, 0 remain",
                "* [WS] decoded passing [TEXT payload=3/3]",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadSplitAcrossReads_WritesTheFrameOnceItsHeadIsWhole()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RecordingTransferEvents events = await RunAsync(diagnostics, Bytes(Head101), Bytes("\x81"), Bytes("\x01z"));

        string[] expected =
            [
                .. SwitchLines,
                "* [WS] websocket established, callback mode",
                "{ 1",
                "{ 2",
                "* [WS] decoded decoded [TEXT payload=0/1]",
                "* [WS] passed 1 bytes payload, 0 remain",
                "* [WS] decoded passing [TEXT payload=1/1]",
                "{ 0",
                "* shutting down connection #0",
            ];
        string[] actual = AfterTheHead(events);
        Report(diagnostics, expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotTraced_WritesNoTraceLines()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var events = new RecordingTransferEvents();
        diagnostics.Arrange("frame tracing", false);
        diagnostics.Bytes("server reply", Bytes(Head101 + "\x89\x01p\x81\x02hi\x88\x00"));
        await Handler(new ScriptedConnection(Bytes(Head101 + "\x89\x01p\x81\x02hi\x88\x00")), tracesFrames: false).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("ws://127.0.0.1:47932/p"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Bytes("hello")),
            Events = events,
        });

        string[] wsLines = events.Transcript.Where(line => line.StartsWith("* [WS] ", StringComparison.Ordinal)).ToArray();
        Report(diagnostics, ["* [WS] Received 101, switch to WebSocket"], wsLines);
        CollectionAssert.AreEqual(
            (string[])["* [WS] Received 101, switch to WebSocket"],
            events.Transcript.Where(line => line.StartsWith("* [WS] ", StringComparison.Ordinal)).ToArray());
    }

    private static string[] AfterTheHead(RecordingTransferEvents events) =>
        [.. events.Transcript.SkipWhile(line => line != SwitchLines[0])];

    private static void ArrangeServer(TestDiagnostics diagnostics, params byte[][] reads)
    {
        diagnostics.Arrange("url", Url);
        diagnostics.Arrange("server reads", reads.Length);
        for (int index = 0; index < reads.Length; index++)
        {
            diagnostics.Bytes("server read " + (index + 1), reads[index]);
        }
    }

    private static void Report(TestDiagnostics diagnostics, string[] expected, string[] actual)
    {
        diagnostics.Act("trace lines", string.Join(" | ", actual));
        diagnostics.Assert("trace lines", string.Join(" | ", expected), string.Join(" | ", actual));
    }

    private static async Task<RecordingTransferEvents> RunAsync(TestDiagnostics diagnostics, params byte[][] reads)
    {
        ArrangeServer(diagnostics, reads);
        var events = new RecordingTransferEvents();
        using (diagnostics.Phase("frame exchange"))
        {
            await Handler(new ScriptedConnection(reads)).ExecuteAsync(
                new TransferContext { Url = CurlUrl.Parse(Url), Output = new MemoryStream(), Events = events });
        }

        return events;
    }

    private static WsProtocolHandler Handler(ScriptedConnection connection, bool tracesFrames = true) =>
        new(
            new RecordingConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)),
            new RecordingAuthenticator(),
            new FixedRandomSource())
        {
            TracesFrames = tracesFrames,
        };

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
