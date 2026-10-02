using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Pins what an <c>mqtt://</c> transfer reports to <see cref="ITransferEvents" /> after
/// connecting, against curl 8.21.0's <c>--trace-ascii -</c>, measured on 2026-09-29 against a
/// loopback listener (captures in BL-935's Notes): the client identifier, each packet sent
/// as a header block, each fixed header byte and CONNACK or SUBACK body as header blocks
/// received, each PUBLISH body as data, curl's <c>mqtt_doing: state [N]</c> lines, and the
/// line the connection ends with.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerTransferEventsTests
{
    private const string FixedSuffix = "PBadK4E3";

    private const string Connect = "10 18 00 04 4D 51 54 54 04 02 00 3C 00 0C 63 75 72 6C 50 42 61 64 4B 34 45 33";

    private const string Subscribe = "82 06 00 01 00 01 74 00";

    private const string Connack = "20 02 00 00";

    private const string Suback = "90 03 00 01 00";

    /// <summary>A PUBLISH to topic <c>t</c> of <c>hello</c>.</summary>
    private const string Publish = "30 08 00 01 74 68 65 6C 6C 6F";

    [TestMethod]
    public async Task ExecuteAsync_SubscribeReceivingOnePublishThenClose_ReportsCurlsTraceInOrder()
    {
        // Measured: CONNACK, SUBACK, one PUBLISH of "hello" to t, then the listener closed; exit 56.
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback), Hex(Publish));

        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            ConnectedAndSubscribed()
                .Concat(PublishReceived())
                .Concat([State(0), "* Connection disconnected", "* shutting down connection #0"])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubscribeEndedByDisconnect_ReportsGotDisconnect()
    {
        // Measured: SUBACK, PUBLISH and DISCONNECT in one read; exit 0.
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback + Publish + "E0 00"));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            ConnectedAndSubscribed()
                .Concat(PublishReceived())
                .Concat([State(0), Received("E0"), Received("00"), "* Got DISCONNECT", "* shutting down connection #0"])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_Publish_ReportsThePublishAndDisconnectSentAsHeaders()
    {
        // Measured: -d payload mqtt://host/t.
        Run run = await RunTransferAsync("mqtt://h/t", Encoding.ASCII.GetBytes("payload"), Hex(Connack));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            ConnectAccepted()
                .Concat([Sent("30 0A 00 01 74 70 61 79 6C 6F 61 64"), Sent("E0 00"), "* shutting down connection #0"])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackRefused_ReportsTheBodyTheMessageThenShuttingDown()
    {
        // Measured: CONNACK 20 02 00 05; exit 8.
        Run run = await RunAsync("mqtt://h/t", Hex("20 02 00 05"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            ConnectSent()
                .Concat(
                [
                    State(0), Received("20"), Received("02"), State(2), Received("00 05"),
                    "* Expected 0000 but got 0005", "* shutting down connection #0",
                ])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackOfTheWrongLength_ReportsTheMessageAfterItsState()
    {
        // Measured: CONNACK 20 03 00 00 00; exit 8.
        Run run = await RunAsync("mqtt://h/t", Hex("20 03 00 00 00"));

        CollectionAssert.AreEqual(
            ConnectSent()
                .Concat(
                [
                    State(0), Received("20"), Received("03"), State(2),
                    "* CONNACK expected Remaining Length 2, got 3", "* shutting down connection #0",
                ])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PingResponseThenPublishCutShort_ReportsServerDisconnectedWithoutAMessage()
    {
        // Measured: SUBACK, PINGRESP, then a PUBLISH claiming 8 bytes of which 4 came; exit 18.
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback + "D0 00 30 08 00 01 74 68"));

        Assert.AreEqual(CurlExitCode.PartialFile, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            ConnectedAndSubscribed()
                .Concat(
                [
                    State(0), Received("D0"), Received("00"), "* Received ping response.",
                    State(0), Received("30"), Received("08"), State(5), "* Remaining length: 8 bytes",
                    "<= " + Latin1("00 01 74 68"), State(6), "* server disconnected",
                    "* shutting down connection #0",
                ])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishClosedBeforeAnyBody_ReportsServerDisconnectedAtOnce()
    {
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback + "30 08"));

        CollectionAssert.AreEqual(
            new[] { State(5), "* Remaining length: 8 bytes", "* server disconnected", "* shutting down connection #0" },
            run.Transcript.TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyPacketThenBodies_ReportsStateNotHandled()
    {
        // Measured: SUBACK, then 40 00, 30 02 00 01, 20 05 ...: curl read 00 01 as a fixed
        // header and ended with "State not handled yet", exit 0.
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback + "40 00 30 02 00 01 20 05 61 62 63 64 65"));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            ConnectedAndSubscribed()
                .Concat(
                [
                    State(0), Received("40"), Received("00"),
                    State(0), Received("30"), Received("02"),
                    State(0), Received("00"), Received("01"),
                    State(7), "* State not handled yet", "* shutting down connection #0",
                ])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishOverOneRead_ReportsEachSliceWithTheRemainState()
    {
        byte[] payload = new byte[5000];
        byte[] publish = [0x30, 0x8B, 0x27, 0x00, 0x01, 0x74, .. payload];

        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback), publish[..4096], publish[4096..]);

        string[] transcript = [.. run.Transcript];
        int remaining = Array.IndexOf(transcript, "* Remaining length: 5003 bytes");
        Assert.AreEqual(4096 + 3, transcript[remaining + 1].Length);
        Assert.AreEqual(State(6), transcript[remaining + 2]);
        Assert.AreEqual(907 + 3, transcript[remaining + 3].Length);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputRefusesTheWrite_ReportsTheBlockTheMessageThenClosing()
    {
        // Measured: an output that could not be opened traced the block, curl's message and
        // "* closing connection #0", and exited 23.
        ScriptedConnection connection = new(Hex(Connack), Hex(Suback), Hex(Publish));
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt://h/t"),
            Output = new RecordingStream { FailWrites = true },
            Events = events,
        };

        TransferResult result = await Handler(connection, 4).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "<= " + Latin1("00 01 74 68 65 6C 6C 6F"), "* " + result.ErrorMessage, "* closing connection #4" },
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoTopic_ReportsTheMessageThenShuttingDown()
    {
        // Measured: mqtt://host/ printed "* No MQTT topic found. Forgot to URL encode it?", exit 3.
        Run run = await RunAsync("mqtt://h/", Hex(Connack));

        CollectionAssert.AreEqual(
            ConnectAccepted()
                .Concat(["* No MQTT topic found. Forgot to URL encode it?", "* shutting down connection #0"])
                .ToArray(),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnexpectedPacketAfterSubscribe_ReportsOnlyShuttingDown()
    {
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex("40 02 00 01"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { State(3), "* shutting down connection #0" },
            run.Transcript.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishOverMaxFileSize_ReportsRemainingLengthThenMaximumFileSizeExceeded()
    {
        // Measured on curl 8.21.0 (BL-1115): -v --max-filesize 9 against a 10-byte PUBLISH to t/x.
        ScriptedConnection connection = new(Hex(Connack), Hex(Suback), Hex("30 0A 00 03 74 2F 78 68 65 6C 6C 6F"));
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt://h/t/x"),
            Output = new RecordingStream(),
            Events = events,
            MaxFileSize = 9,
        };

        TransferResult result = await Handler(connection, 0).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { State(5), "* Remaining length: 10 bytes", "* Maximum file size exceeded", "* shutting down connection #0" },
            events.Transcript.TakeLast(4).ToArray());
    }

    /// <summary>What every transfer reports up to and including the CONNECT sent.</summary>
    private static string[] ConnectSent() =>
        ["* Using client id 'curl" + FixedSuffix + "'", Sent(Connect), State(0)];

    /// <summary>What a transfer reports up to and including an accepted CONNACK's body.</summary>
    private static string[] ConnectAccepted() =>
        [.. ConnectSent(), State(0), Received("20"), Received("02"), State(2), Received("00 00")];

    /// <summary>What a subscribe reports up to and including the SUBACK's body.</summary>
    private static string[] ConnectedAndSubscribed() =>
        [.. ConnectAccepted(), Sent(Subscribe), State(0), Received("90"), Received("03"), State(3), Received("00 01 00")];

    /// <summary>What the measured PUBLISH of <c>hello</c> to <c>t</c> reports.</summary>
    private static string[] PublishReceived() =>
        [State(0), Received("30"), Received("08"), State(5), "* Remaining length: 8 bytes", "<= " + Latin1("00 01 74 68 65 6C 6C 6F")];

    private static string State(int state) => $"* mqtt_doing: state [{state}]";

    private static string Sent(string hex) => "> " + Latin1(hex);

    private static string Received(string hex) => "< " + Latin1(hex);

    private static string Latin1(string hex) => Encoding.Latin1.GetString(Hex(hex));

    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    private static MqttProtocolHandler Handler(IConnection connection, long connectionNumber) =>
        new(new FakeConnector(ConnectResult.Connected(connection, null, connectionNumber: connectionNumber)), () => FixedSuffix);

    private static Task<Run> RunAsync(string url, params byte[][] reads) => RunTransferAsync(url, null, reads);

    private static async Task<Run> RunTransferAsync(string url, byte[]? postData, params byte[][] reads)
    {
        ScriptedConnection connection = new(reads);
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = new RecordingStream(),
            PostData = postData is null ? (ReadOnlyMemory<byte>?)null : postData,
            Events = events,
        };

        TransferResult result = await Handler(connection, 0).ExecuteAsync(context);
        return new Run(result, events.Transcript);
    }

    private sealed record Run(TransferResult Result, List<string> Transcript);
}
