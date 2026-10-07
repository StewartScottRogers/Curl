using Curl.Testing;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Creates each frame type of RFC 9113 section 6 with <see cref="Http2FrameFactory" />,
/// pins its wire bytes, reads it back through an in-memory stream and parses it with
/// <see cref="Http2FramePayloadParser" />.
/// </summary>
[TestClass]
public sealed class Http2FrameRoundTripTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Data_Unpadded_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "DATA stream 1, payload hello, end stream");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true), "000005 00 01 00000001 68656c6c6f");
        var data = Http2FramePayloadParser.ParseData(frame).ToArray();
        diagnostics.Bytes("parsed data", data);

        diagnostics.Diff("parsed data", "hello"u8.ToArray(), data);
        CollectionAssert.AreEqual("hello"u8.ToArray(), Http2FramePayloadParser.ParseData(frame).ToArray());
    }

    [TestMethod]
    public async Task Data_Padded_RoundTripsWithoutThePadding()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "DATA stream 3, payload hi, pad length 2");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateData(3, "hi"u8.ToArray(), isEndStream: false, padLength: 2), "000005 00 08 00000003 02 6869 0000");
        var data = Http2FramePayloadParser.ParseData(frame).ToArray();
        diagnostics.Bytes("parsed data", data);

        diagnostics.Diff("parsed data", "hi"u8.ToArray(), data);
        CollectionAssert.AreEqual("hi"u8.ToArray(), Http2FramePayloadParser.ParseData(frame).ToArray());
    }

    [TestMethod]
    public async Task Headers_WithPriorityAndPadding_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var priority = new Http2Priority(3, IsExclusive: true, Weight: 16);
        diagnostics.Arrange("priority", priority);

        var frame = await RoundTrip(
            diagnostics,
            Http2FrameFactory.CreateHeaders(5, new byte[] { 0x82 }, isEndStream: true, isEndHeaders: true, priority, padLength: 1),
            "000008 01 2d 00000005 01 80000003 0f 82 00");
        var payload = Http2FramePayloadParser.ParseHeaders(frame);
        diagnostics.Act("parsed priority", payload.Priority);
        diagnostics.Bytes("parsed fragment", payload.Fragment.ToArray());

        diagnostics.Assert("priority", priority, payload.Priority);
        Assert.AreEqual(priority, payload.Priority);
        CollectionAssert.AreEqual(new byte[] { 0x82 }, payload.Fragment.ToArray());
    }

    [TestMethod]
    public async Task Headers_Plain_RoundTripsWithoutPriority()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "HEADERS stream 1, fragment 8286, no priority");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateHeaders(1, new byte[] { 0x82, 0x86 }, isEndStream: false, isEndHeaders: false), "000002 01 00 00000001 8286");
        var payload = Http2FramePayloadParser.ParseHeaders(frame);
        diagnostics.Act("parsed priority", payload.Priority);
        diagnostics.Bytes("parsed fragment", payload.Fragment.ToArray());

        diagnostics.Assert("priority", null, payload.Priority);
        Assert.IsNull(payload.Priority);
        CollectionAssert.AreEqual(new byte[] { 0x82, 0x86 }, payload.Fragment.ToArray());
    }

    [TestMethod]
    public async Task Priority_NonExclusive_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var priority = new Http2Priority(0, IsExclusive: false, Weight: 256);
        diagnostics.Arrange("priority", priority);

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreatePriority(7, priority), "000005 02 00 00000007 00000000 ff");
        var parsed = Http2FramePayloadParser.ParsePriority(frame);
        diagnostics.Act("parsed priority", parsed);

        diagnostics.Assert("priority", priority, parsed);
        Assert.AreEqual(priority, Http2FramePayloadParser.ParsePriority(frame));
    }

    [TestMethod]
    public async Task RstStream_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "RST_STREAM stream 1, error code Cancel");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Cancel), "000004 03 00 00000001 00000008");
        var errorCode = Http2FramePayloadParser.ParseRstStream(frame);
        diagnostics.Act("parsed error code", errorCode);

        diagnostics.Assert("error code", Http2ErrorCode.Cancel, errorCode);
        Assert.AreEqual(Http2ErrorCode.Cancel, Http2FramePayloadParser.ParseRstStream(frame));
    }

    [TestMethod]
    public async Task Settings_RoundTripsInOrder()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Http2Setting[] settings = [new(Http2SettingIdentifier.MaxFrameSize, 32768), new((Http2SettingIdentifier)0x99, 7)];
        diagnostics.Arrange("settings", string.Join(", ", settings));

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateSettings(settings), "00000c 04 00 00000000 0005 00008000 0099 00000007");
        var parsed = Http2FramePayloadParser.ParseSettings(frame).ToArray();
        diagnostics.Act("parsed settings", string.Join(", ", parsed));

        diagnostics.Assert("settings", string.Join(", ", settings), string.Join(", ", parsed));
        CollectionAssert.AreEqual(settings, Http2FramePayloadParser.ParseSettings(frame).ToArray());
    }

    [TestMethod]
    public async Task SettingsAcknowledgement_RoundTripsEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "SETTINGS acknowledgement");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateSettingsAcknowledgement(), "000000 04 01 00000000");
        var count = Http2FramePayloadParser.ParseSettings(frame).Count;
        diagnostics.Act("parsed setting count", count);

        diagnostics.Assert("setting count", 0, count);
        Assert.AreEqual(0, Http2FramePayloadParser.ParseSettings(frame).Count);
    }

    [TestMethod]
    public async Task PushPromise_Padded_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "PUSH_PROMISE stream 1, promised stream 2, fragment 82, pad length 1");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreatePushPromise(1, 2, new byte[] { 0x82 }, isEndHeaders: true, padLength: 1), "000007 05 0c 00000001 01 00000002 82 00");
        var payload = Http2FramePayloadParser.ParsePushPromise(frame);
        diagnostics.Act("promised stream id", payload.PromisedStreamId);
        diagnostics.Bytes("parsed fragment", payload.Fragment.ToArray());

        diagnostics.Assert("promised stream id", 2, payload.PromisedStreamId);
        Assert.AreEqual(2, payload.PromisedStreamId);
        CollectionAssert.AreEqual(new byte[] { 0x82 }, payload.Fragment.ToArray());
    }

    [TestMethod]
    public async Task PushPromise_Unpadded_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "PUSH_PROMISE stream 1, promised stream 4, empty fragment");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreatePushPromise(1, 4, ReadOnlyMemory<byte>.Empty, isEndHeaders: false), "000004 05 00 00000001 00000004");
        var promisedStreamId = Http2FramePayloadParser.ParsePushPromise(frame).PromisedStreamId;
        diagnostics.Act("promised stream id", promisedStreamId);

        diagnostics.Assert("promised stream id", 4, promisedStreamId);
        Assert.AreEqual(4, Http2FramePayloadParser.ParsePushPromise(frame).PromisedStreamId);
    }

    [TestMethod]
    public async Task Ping_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "PING opaque data 0x1122334455667788");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreatePing(0x1122334455667788, isAcknowledgement: false), "000008 06 00 00000000 1122334455667788");
        var opaqueData = Http2FramePayloadParser.ParsePing(frame);
        diagnostics.Act("parsed opaque data", opaqueData);

        diagnostics.Assert("opaque data", 0x1122334455667788ul, opaqueData);
        Assert.AreEqual(0x1122334455667788ul, Http2FramePayloadParser.ParsePing(frame));
    }

    [TestMethod]
    public async Task GoAway_RoundTripsWithDebugData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "GOAWAY last stream 5, error code EnhanceYourCalm, debug data calm");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateGoAway(5, Http2ErrorCode.EnhanceYourCalm, "calm"u8.ToArray()), "00000c 07 00 00000000 00000005 0000000b 63616c6d");
        var goAway = Http2FramePayloadParser.ParseGoAway(frame);
        diagnostics.Act("last stream id", goAway.LastStreamId);
        diagnostics.Act("error code", goAway.ErrorCode);
        diagnostics.Bytes("debug data", goAway.DebugData.ToArray());

        diagnostics.Assert("last stream id", 5, goAway.LastStreamId);
        diagnostics.Assert("error code", Http2ErrorCode.EnhanceYourCalm, goAway.ErrorCode);
        Assert.AreEqual(5, goAway.LastStreamId);
        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, goAway.ErrorCode);
        CollectionAssert.AreEqual("calm"u8.ToArray(), goAway.DebugData.ToArray());
    }

    [TestMethod]
    public async Task WindowUpdate_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "WINDOW_UPDATE stream 0, increment int.MaxValue");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateWindowUpdate(0, int.MaxValue), "000004 08 00 00000000 7fffffff");
        var increment = Http2FramePayloadParser.ParseWindowUpdate(frame);
        diagnostics.Act("parsed increment", increment);

        diagnostics.Assert("increment", int.MaxValue, increment);
        Assert.AreEqual(int.MaxValue, Http2FramePayloadParser.ParseWindowUpdate(frame));
    }

    [TestMethod]
    public async Task Continuation_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "CONTINUATION stream 1, fragment 84, end headers");

        var frame = await RoundTrip(diagnostics, Http2FrameFactory.CreateContinuation(1, new byte[] { 0x84 }, isEndHeaders: true), "000001 09 04 00000001 84");
        diagnostics.Bytes("payload", frame.Payload.Span);

        diagnostics.Diff("payload", new byte[] { 0x84 }, frame.Payload.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0x84 }, frame.Payload.ToArray());
    }

    [TestMethod]
    public void CreateData_PadLengthOutOfRange_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("pad lengths", "-1, 256");

        var negative = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreateData(1, ReadOnlyMemory<byte>.Empty, isEndStream: false, padLength: -1));
        var tooLarge = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreateData(1, ReadOnlyMemory<byte>.Empty, isEndStream: false, padLength: 256));
        diagnostics.Act("exception parameters", $"{negative.ParamName}, {tooLarge.ParamName}");

        diagnostics.Assert("exception types", "ArgumentOutOfRangeException, ArgumentOutOfRangeException", $"{negative.GetType().Name}, {tooLarge.GetType().Name}");
    }

    [TestMethod]
    public void CreatePriority_WeightOutOfRange_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("weights", "0, 257");

        var zero = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreatePriority(1, new Http2Priority(0, false, 0)));
        var tooLarge = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreatePriority(1, new Http2Priority(0, false, 257)));
        diagnostics.Act("exception parameters", $"{zero.ParamName}, {tooLarge.ParamName}");

        diagnostics.Assert("exception types", "ArgumentOutOfRangeException, ArgumentOutOfRangeException", $"{zero.GetType().Name}, {tooLarge.GetType().Name}");
    }

    [TestMethod]
    public void CreateSettings_Null_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("settings", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => Http2FrameFactory.CreateSettings(null!));
        diagnostics.Act("exception parameter", exception.ParamName);

        diagnostics.Assert("exception type", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private static async Task<Http2Frame> RoundTrip(TestDiagnostics diagnostics, Http2Frame frame, string expectedHex)
    {
        var bytes = Http2FrameCodec.Serialize(frame);
        diagnostics.Act("frame", $"{frame.Type}, flags {frame.Flags}, stream {frame.StreamId}, payload length {frame.Payload.Length}");
        diagnostics.Bytes("wire bytes", bytes);
        diagnostics.Diff("wire bytes", FromHex(expectedHex), bytes);
        CollectionAssert.AreEqual(FromHex(expectedHex), bytes);
        var frames = await Http2Test.FramesIn(bytes);
        diagnostics.Assert("frames read back", 1, frames.Count);
        Assert.AreEqual(1, frames.Count);
        Http2Test.AssertFrame(frame, frames[0]);
        return frames[0];
    }
}
