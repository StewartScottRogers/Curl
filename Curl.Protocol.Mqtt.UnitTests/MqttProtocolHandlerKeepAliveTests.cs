using System.Diagnostics;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Mqtt.Fakes;

namespace Curl.Protocol.Mqtt;

/// <summary>
/// Pins the PINGREQ an <c>mqtt://</c> transfer sends once it has been idle more than 60
/// seconds, as curl 8.21.0's <c>mqtt_ping</c> does with the default
/// <c>CURLOPT_UPKEEP_INTERVAL_MS</c> of 60000 (BL-1116): <c>C0 00</c> and the <c>-v</c> line
/// <c>mqtt_ping: sent ping request.</c>, no second one before a PINGRESP, and the 60 seconds
/// counted from the last byte sent or received.
/// </summary>
[TestClass]
public sealed class MqttProtocolHandlerKeepAliveTests
{
    private const string Connect = "10 18 00 04 4D 51 54 54 04 02 00 3C 00 0C 63 75 72 6C 50 42 61 64 4B 34 45 33";

    private const string Subscribe = "82 06 00 01 00 01 74 00";

    private const string PingRequest = "C0 00";

    private const string PingSentLine = "* mqtt_ping: sent ping request.";

    private static readonly TimeSpan PingDue = TimeSpan.FromMilliseconds(60001);

    [TestMethod]
    public async Task ExecuteAsync_IdleAfterSuback_SendsPingRequestOnlyPastSixtySeconds()
    {
        Subscription subscription = await SubscribeAsync();

        subscription.Clock.Advance(TimeSpan.FromSeconds(60));
        CollectionAssert.AreEqual(Hex(Connect + Subscribe), subscription.Connection.Written);
        CollectionAssert.DoesNotContain(subscription.Events.Transcript, PingSentLine);

        subscription.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => subscription.Connection.Written.Length == Hex(Connect + Subscribe + PingRequest).Length);

        CollectionAssert.AreEqual(Hex(Connect + Subscribe + PingRequest), subscription.Connection.Written);
        CollectionAssert.AreEqual(
            new[] { "> " + Latin1(PingRequest), PingSentLine, "* mqtt_doing: state [0]" },
            subscription.Events.Transcript.TakeLast(3).ToArray());
        await subscription.CloseAsync();
    }

    [TestMethod]
    public async Task ExecuteAsync_PingRequestOutstanding_SendsNoSecondUntilPingResponseThenSixtySecondsMore()
    {
        Subscription subscription = await SubscribeAsync();
        subscription.Clock.Advance(PingDue);
        await WaitUntilAsync(() => subscription.Connection.Written.Length == Hex(Connect + Subscribe + PingRequest).Length);

        subscription.Clock.Advance(TimeSpan.FromSeconds(300));
        Assert.IsNull(subscription.Clock.NextTimerDueAt);
        CollectionAssert.AreEqual(Hex(Connect + Subscribe + PingRequest), subscription.Connection.Written);

        subscription.Connection.Send(Hex("D0 00"));
        await subscription.WaitForPingDueAsync();
        subscription.Clock.Advance(TimeSpan.FromSeconds(60));
        CollectionAssert.AreEqual(Hex(Connect + Subscribe + PingRequest), subscription.Connection.Written);
        subscription.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => subscription.Connection.Written.Length == Hex(Connect + Subscribe + PingRequest + PingRequest).Length);

        CollectionAssert.AreEqual(Hex(Connect + Subscribe + PingRequest + PingRequest), subscription.Connection.Written);
        Assert.AreEqual(2, subscription.Events.Transcript.Count(line => line == PingSentLine));
        await subscription.CloseAsync();
    }

    [TestMethod]
    public async Task ExecuteAsync_PublishArrivingAtFiftyNineSeconds_RestartsTheSixtySecondCount()
    {
        Subscription subscription = await SubscribeAsync();
        subscription.Clock.Advance(TimeSpan.FromSeconds(59));

        subscription.Connection.Send(Hex("30 08 00 01 74 68 65 6C 6C 6F"));
        await subscription.WaitForPingDueAsync();
        subscription.Clock.Advance(TimeSpan.FromSeconds(60));
        CollectionAssert.AreEqual(Hex(Connect + Subscribe), subscription.Connection.Written);
        subscription.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntilAsync(() => subscription.Connection.Written.Length == Hex(Connect + Subscribe + PingRequest).Length);

        CollectionAssert.AreEqual(Hex(Connect + Subscribe + PingRequest), subscription.Connection.Written);
        TransferResult result = await subscription.CloseAsync();
        CollectionAssert.AreEqual(Hex("00 01 74 68 65 6C 6C 6F"), subscription.Output.ToArray());
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_Publish_EndsWithDisconnectAndNeverPings()
    {
        ManualTimeProvider clock = new();
        GatedConnection connection = new();
        connection.Send(Hex("20 02 00 00"));

        TransferResult result = await Handler(connection).ExecuteAsync(Context(clock, new TranscriptTransferEvents(), new RecordingStream(), "payload"u8.ToArray()));
        clock.Advance(TimeSpan.FromSeconds(300));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(clock.NextTimerDueAt);
        CollectionAssert.AreEqual(Hex(Connect + "30 0A 00 01 74 70 61 79 6C 6F 61 64 E0 00"), connection.Written);
    }

    /// <summary>Starts a subscribe to <c>t</c> and waits until it is subscribed and idle.</summary>
    private static async Task<Subscription> SubscribeAsync()
    {
        ManualTimeProvider clock = new();
        GatedConnection connection = new();
        TranscriptTransferEvents events = new();
        RecordingStream output = new();
        connection.Send(Hex("20 02 00 00"));
        connection.Send(Hex("90 03 00 01 00"));
        Task<TransferResult> transfer = Handler(connection).ExecuteAsync(Context(clock, events, output, null)).AsTask();
        Subscription subscription = new(clock, connection, events, output, transfer);
        await subscription.WaitForPingDueAsync();
        return subscription;
    }

    private static TransferContext Context(ManualTimeProvider clock, TranscriptTransferEvents events, RecordingStream output, byte[]? postData) => new()
    {
        Url = CurlUrl.Parse("mqtt://h/t"),
        Output = output,
        PostData = postData is null ? (ReadOnlyMemory<byte>?)null : postData,
        Events = events,
        TimeProvider = clock,
    };

    private static MqttProtocolHandler Handler(IConnection connection) =>
        new(FakeConnector.For(connection), () => "PBadK4E3");

    /// <summary>Waits, on the real clock and for at most ten seconds, until <paramref name="condition" /> holds.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.IsTrue(waited.Elapsed < TimeSpan.FromSeconds(10), "The transfer never reached the awaited point.");
            await Task.Delay(1);
        }
    }

    private static string Latin1(string hex) => Encoding.Latin1.GetString(Hex(hex));

    private static byte[] Hex(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));

    /// <summary>A subscribe transfer in progress, with the clock and peer a test drives it by.</summary>
    private sealed record Subscription(
        ManualTimeProvider Clock,
        GatedConnection Connection,
        TranscriptTransferEvents Events,
        RecordingStream Output,
        Task<TransferResult> Transfer)
    {
        /// <summary>Waits until the transfer is idle with its PINGREQ due 60.001 s from now.</summary>
        public Task WaitForPingDueAsync() => WaitUntilAsync(() => Clock.NextTimerDueAt == Clock.Now + PingDue);

        /// <summary>Closes the peer's side and returns how the transfer ended.</summary>
        public Task<TransferResult> CloseAsync()
        {
            Connection.Send([]);
            return Transfer;
        }
    }
}
