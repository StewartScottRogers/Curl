using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;
using Curl.Testing;

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

    /// <summary>The transcript of the transfer the test ran, kept for its DIFF and ASSERT lines.</summary>
    private List<string> transcript = [];

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ExecuteAsync_SubscribeReceivingOnePublishThenClose_ReportsCurlsTraceInOrder()
    {
        // Measured: CONNACK, SUBACK, one PUBLISH of "hello" to t, then the listener closed; exit 56.
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback), Hex(Publish));

        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            DiffTranscript(ConnectedAndSubscribed()
                .Concat(PublishReceived())
                .Concat([State(0), "* Connection disconnected", "* shutting down connection #0"])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_SubscribeEndedByDisconnect_ReportsGotDisconnect()
    {
        // Measured: SUBACK, PUBLISH and DISCONNECT in one read; exit 0.
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback + Publish + "E0 00"));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            DiffTranscript(ConnectedAndSubscribed()
                .Concat(PublishReceived())
                .Concat([State(0), Received("E0"), Received("00"), "* Got DISCONNECT", "* shutting down connection #0"])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_Publish_ReportsThePublishAndDisconnectSentAsHeaders()
    {
        // Measured: -d payload mqtt://host/t.
        Run run = await RunTransferAsync("mqtt://h/t", Encoding.ASCII.GetBytes("payload"), Hex(Connack));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            DiffTranscript(ConnectAccepted()
                .Concat([Sent("30 0A 00 01 74 70 61 79 6C 6F 61 64"), Sent("E0 00"), "* shutting down connection #0"])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackRefused_ReportsTheBodyTheMessageThenShuttingDown()
    {
        // Measured: CONNACK 20 02 00 05; exit 8.
        Run run = await RunAsync("mqtt://h/t", Hex("20 02 00 05"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            DiffTranscript(ConnectSent()
                .Concat(
                [
                    State(0), Received("20"), Received("02"), State(2), Received("00 05"),
                    "* Expected 0000 but got 0005", "* shutting down connection #0",
                ])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnackOfTheWrongLength_ReportsTheMessageAfterItsState()
    {
        // Measured: CONNACK 20 03 00 00 00; exit 8.
        Run run = await RunAsync("mqtt://h/t", Hex("20 03 00 00 00"));

        CollectionAssert.AreEqual(
            DiffTranscript(ConnectSent()
                .Concat(
                [
                    State(0), Received("20"), Received("03"), State(2),
                    "* CONNACK expected Remaining Length 2, got 3", "* shutting down connection #0",
                ])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PingResponseThenPublishCutShort_ReportsServerDisconnectedWithoutAMessage()
    {
        // Measured: SUBACK, PINGRESP, then a PUBLISH claiming 8 bytes of which 4 came; exit 18.
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback + "D0 00 30 08 00 01 74 68"));

        Assert.AreEqual(CurlExitCode.PartialFile, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            DiffTranscript(ConnectedAndSubscribed()
                .Concat(
                [
                    State(0), Received("D0"), Received("00"), "* Received ping response.",
                    State(0), Received("30"), Received("08"), State(5), "* Remaining length: 8 bytes",
                    "<= " + Latin1("00 01 74 68"), State(6), "* server disconnected",
                    "* shutting down connection #0",
                ])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishPayloadReadPending_ReportsAgainOnceBeforeThePayload()
    {
        // Measured (BL-1434): the fixed header 30 0B, a gap, then the whole body; curl wrote
        // "EEEE AAAAGAIN" once, after "Remaining length: 11 bytes" and before the data, and
        // the run that read the data wrote "mqtt_doing: state [6]" first (BL-1442).
        Run run = await RunHeldAsync(
            "mqtt://h/t", null, [3], Hex(Connack), Hex(Suback), Hex("30 0B"), Hex("00 01 74 68 65 6C 6C 6F 77 6F 72"));

        CollectionAssert.AreEqual(
            DiffTranscript(ConnectedAndSubscribed()
                .Concat(
                [
                    State(0), Received("30"), Received("0B"), State(5), "* Remaining length: 11 bytes",
                    "* EEEE AAAAGAIN", State(6), "<= " + Latin1("00 01 74 68 65 6C 6C 6F 77 6F 72"),
                    State(0), "* Connection disconnected", "* shutting down connection #0",
                ])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishPayloadInTwoPendingParts_ReportsEachPartAsItArrivesWithTheRemainState()
    {
        // Measured (BL-1434, BL-1442): 30 0B, a gap, 00 01 74 68 65 6C, a gap, 6C 6F 77 6F 72;
        // curl wrote each part as it arrived, with "mqtt_doing: state [6]" before each run
        // after the first and "EEEE AAAAGAIN" for each run that found nothing waiting.
        Run run = await RunHeldAsync(
            "mqtt://h/t", null, [3, 4], Hex(Connack), Hex(Suback), Hex("30 0B"), Hex("00 01 74 68 65 6C"), Hex("6C 6F 77 6F 72"));

        CollectionAssert.AreEqual(
            DiffTranscript(ConnectedAndSubscribed()
                .Concat(
                [
                    State(0), Received("30"), Received("0B"), State(5), "* Remaining length: 11 bytes",
                    "* EEEE AAAAGAIN", State(6), "<= " + Latin1("00 01 74 68 65 6C"),
                    State(6), "* EEEE AAAAGAIN", State(6), "<= " + Latin1("6C 6F 77 6F 72"),
                    State(0), "* Connection disconnected", "* shutting down connection #0",
                ])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishPayloadBufferedWithItsHeader_ReportsNoAgain()
    {
        // Measured (BL-1434): the PUBLISH in one segment; curl wrote no "EEEE AAAAGAIN".
        Run run = await RunHeldAsync("mqtt://h/t", null, [3], Hex(Connack), Hex(Suback + Publish), Hex("E0 00"));

        Diagnostics.Assert("\"EEEE AAAAGAIN\" lines", 0, run.Transcript.Count(line => line == "* EEEE AAAAGAIN"));
        Diagnostics.Assert("\"Got DISCONNECT\" lines", 1, run.Transcript.Count(line => line == "* Got DISCONNECT"));
        CollectionAssert.DoesNotContain(run.Transcript, "* EEEE AAAAGAIN");
        CollectionAssert.Contains(run.Transcript, "* Got DISCONNECT");
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishClosedBeforeAnyBody_ReportsServerDisconnectedAtOnce()
    {
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex(Suback + "30 08"));

        CollectionAssert.AreEqual(
            AssertTranscriptEnds(new[] { State(5), "* Remaining length: 8 bytes", "* server disconnected", "* shutting down connection #0" }),
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
            DiffTranscript(ConnectedAndSubscribed()
                .Concat(
                [
                    State(0), Received("40"), Received("00"),
                    State(0), Received("30"), Received("02"),
                    State(0), Received("00"), Received("01"),
                    State(7), "* State not handled yet", "* shutting down connection #0",
                ])
                .ToArray()),
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
        Diagnostics.Act("index of the remaining length line", remaining);
        Diagnostics.Assert(
            "the 3 lines after it",
            $"{4096 + 3} characters, {MqttDiagnostics.Escape(State(6))}, {907 + 3} characters",
            string.Join(", ", transcript.Skip(remaining + 1).Take(3).Select((line, index) => index == 1 ? MqttDiagnostics.Escape(line) : $"{line.Length} characters")));
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

        TransferResult result = await ExecuteAsync(connection, context, 4);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        CollectionAssert.AreEqual(
            AssertTranscriptEnds(new[] { "<= " + Latin1("00 01 74 68 65 6C 6C 6F"), "* " + result.ErrorMessage, "* closing connection #4" }),
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NoTopic_ReportsTheMessageThenShuttingDown()
    {
        // Measured: mqtt://host/ printed "* No MQTT topic found. Forgot to URL encode it?", exit 3.
        Run run = await RunAsync("mqtt://h/", Hex(Connack));

        CollectionAssert.AreEqual(
            DiffTranscript(ConnectAccepted()
                .Concat(["* No MQTT topic found. Forgot to URL encode it?", "* shutting down connection #0"])
                .ToArray()),
            run.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UnexpectedPacketAfterSubscribe_ReportsOnlyShuttingDown()
    {
        Run run = await RunAsync("mqtt://h/t", Hex(Connack), Hex("40 02 00 01"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        CollectionAssert.AreEqual(
            AssertTranscriptEnds(new[] { State(3), "* shutting down connection #0" }),
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

        TransferResult result = await ExecuteAsync(connection, context, 0);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        CollectionAssert.AreEqual(
            AssertTranscriptEnds(new[] { State(5), "* Remaining length: 10 bytes", "* Maximum file size exceeded", "* shutting down connection #0" }),
            events.Transcript.TakeLast(4).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SubscribeUnderNoBodyReceivingPublish_IsWeirdServerReplyWithNothingWritten()
    {
        // Measured on curl 8.21.0 (BL-1310): -sv -I, SUBACK and a PUBLISH of "hi" to t in one read; exit 8.
        ScriptedConnection connection = new(Hex(Connack), Hex(Suback + "30 05 00 01 74 68 69"));
        TranscriptTransferEvents events = new();
        RecordingStream output = new();
        TransferContext context = new() { Url = CurlUrl.Parse("mqtt://h/t"), Output = output, Events = events, NoBody = true };

        TransferResult result = await ExecuteAsync(connection, context, 0);

        Assert.AreEqual(new TransferResult(CurlExitCode.WeirdServerReply, 0, "Weird server reply"), result);
        Assert.IsEmpty(output.Writes);
        CollectionAssert.AreEqual(
            DiffTranscript(ConnectedAndSubscribed()
                .Concat([State(0), Received("30"), Received("05"), State(5), "* Remaining length: 5 bytes", "<= " + Latin1("00 01 74 68 69"), "* shutting down connection #0"])
                .ToArray()),
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishOverMaxFileSizeUnderNoBody_IsStillFilesizeExceeded()
    {
        // curl 8.21.0's mqtt_read_publish checks --max-filesize before any body is written (BL-1310).
        ScriptedConnection connection = new(Hex(Connack), Hex(Suback), Hex("30 0A 00 03 74 2F 78 68 65 6C 6C 6F"));
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt://h/t/x"),
            Output = new RecordingStream(),
            Events = events,
            MaxFileSize = 9,
            NoBody = true,
        };

        TransferResult result = await ExecuteAsync(connection, context, 0);

        Assert.AreEqual(new TransferResult(CurlExitCode.FilesizeExceeded, 0, "Maximum file size exceeded"), result);
        CollectionAssert.AreEqual(
            AssertTranscriptEnds(new[] { "* Remaining length: 10 bytes", "* Maximum file size exceeded", "* shutting down connection #0" }),
            events.Transcript.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishUnderNoBody_SendsThePublishAndSucceeds()
    {
        // Only a subscribe receives a body, so -I leaves a publish (-d) as it is (BL-1310).
        ScriptedConnection connection = new(Hex(Connack));
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("mqtt://h/t"),
            Output = new RecordingStream(),
            PostData = Encoding.ASCII.GetBytes("payload"),
            Events = events,
            NoBody = true,
        };

        TransferResult result = await ExecuteAsync(connection, context, 0);

        Assert.AreEqual(TransferResult.Success(0), result);
        CollectionAssert.AreEqual(
            DiffTranscript(ConnectAccepted()
                .Concat([Sent("30 0A 00 01 74 70 61 79 6C 6F 61 64"), Sent("E0 00"), "* shutting down connection #0"])
                .ToArray()),
            events.Transcript);
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

    /// <summary>
    /// Runs the transfer on connection number <paramref name="connectionNumber" />, writing its
    /// URL, options and scripted reads as ARRANGE lines and its result, bytes sent and
    /// transcript as ACT lines, and keeping the transcript for <see cref="DiffTranscript" />.
    /// </summary>
    private async Task<TransferResult> ExecuteAsync(ScriptedConnection connection, TransferContext context, long connectionNumber)
    {
        Diagnostics.ArrangeContext(context);
        Diagnostics.ArrangeReads(connection.Reads);
        if (connection.HeldReads.Count > 0)
        {
            Diagnostics.Arrange("reads held a turn", string.Join(", ", connection.HeldReads));
        }

        TransferResult result = await Handler(connection, connectionNumber).ExecuteAsync(context);

        transcript = ((TranscriptTransferEvents)context.Events).Transcript;
        Diagnostics.ActResult(result);
        Diagnostics.ActPackets("sent", connection.Written);
        Diagnostics.ActTranscript(transcript);
        return result;
    }

    /// <summary>Writes a DIFF line comparing the whole transcript with <paramref name="expected" />, and returns it.</summary>
    private string[] DiffTranscript(string[] expected)
    {
        Diagnostics.Diff("transcript", string.Join("\n", expected), string.Join("\n", transcript));
        return expected;
    }

    /// <summary>Writes an ASSERT line comparing the transcript's last lines with <paramref name="expected" />, and returns it.</summary>
    private string[] AssertTranscriptEnds(string[] expected)
    {
        Diagnostics.Assert(
            $"last {expected.Length} transcript lines",
            MqttDiagnostics.Lines(expected),
            MqttDiagnostics.Lines(transcript.TakeLast(expected.Length)));
        return expected;
    }

    private Task<Run> RunAsync(string url, params byte[][] reads) => RunTransferAsync(url, null, reads);

    private Task<Run> RunTransferAsync(string url, byte[]? postData, params byte[][] reads) =>
        RunHeldAsync(url, postData, [], reads);

    private async Task<Run> RunHeldAsync(string url, byte[]? postData, int[] heldReads, params byte[][] reads)
    {
        ScriptedConnection connection = new(reads);
        connection.HeldReads.UnionWith(heldReads);
        TranscriptTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(url),
            Output = new RecordingStream(),
            PostData = postData is null ? (ReadOnlyMemory<byte>?)null : postData,
            Events = events,
        };

        TransferResult result = await ExecuteAsync(connection, context, 0);
        return new Run(result, events.Transcript);
    }

    private sealed record Run(TransferResult Result, List<string> Transcript);
}
