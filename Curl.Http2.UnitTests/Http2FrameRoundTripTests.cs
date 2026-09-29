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
    [TestMethod]
    public async Task Data_Unpadded_RoundTrips()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true), "000005 00 01 00000001 68656c6c6f");

        CollectionAssert.AreEqual("hello"u8.ToArray(), Http2FramePayloadParser.ParseData(frame).ToArray());
    }

    [TestMethod]
    public async Task Data_Padded_RoundTripsWithoutThePadding()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateData(3, "hi"u8.ToArray(), isEndStream: false, padLength: 2), "000005 00 08 00000003 02 6869 0000");

        CollectionAssert.AreEqual("hi"u8.ToArray(), Http2FramePayloadParser.ParseData(frame).ToArray());
    }

    [TestMethod]
    public async Task Headers_WithPriorityAndPadding_RoundTrips()
    {
        var priority = new Http2Priority(3, IsExclusive: true, Weight: 16);

        var frame = await RoundTrip(
            Http2FrameFactory.CreateHeaders(5, new byte[] { 0x82 }, isEndStream: true, isEndHeaders: true, priority, padLength: 1),
            "000008 01 2d 00000005 01 80000003 0f 82 00");
        var payload = Http2FramePayloadParser.ParseHeaders(frame);

        Assert.AreEqual(priority, payload.Priority);
        CollectionAssert.AreEqual(new byte[] { 0x82 }, payload.Fragment.ToArray());
    }

    [TestMethod]
    public async Task Headers_Plain_RoundTripsWithoutPriority()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateHeaders(1, new byte[] { 0x82, 0x86 }, isEndStream: false, isEndHeaders: false), "000002 01 00 00000001 8286");
        var payload = Http2FramePayloadParser.ParseHeaders(frame);

        Assert.IsNull(payload.Priority);
        CollectionAssert.AreEqual(new byte[] { 0x82, 0x86 }, payload.Fragment.ToArray());
    }

    [TestMethod]
    public async Task Priority_NonExclusive_RoundTrips()
    {
        var priority = new Http2Priority(0, IsExclusive: false, Weight: 256);

        var frame = await RoundTrip(Http2FrameFactory.CreatePriority(7, priority), "000005 02 00 00000007 00000000 ff");

        Assert.AreEqual(priority, Http2FramePayloadParser.ParsePriority(frame));
    }

    [TestMethod]
    public async Task RstStream_RoundTrips()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Cancel), "000004 03 00 00000001 00000008");

        Assert.AreEqual(Http2ErrorCode.Cancel, Http2FramePayloadParser.ParseRstStream(frame));
    }

    [TestMethod]
    public async Task Settings_RoundTripsInOrder()
    {
        Http2Setting[] settings = [new(Http2SettingIdentifier.MaxFrameSize, 32768), new((Http2SettingIdentifier)0x99, 7)];

        var frame = await RoundTrip(Http2FrameFactory.CreateSettings(settings), "00000c 04 00 00000000 0005 00008000 0099 00000007");

        CollectionAssert.AreEqual(settings, Http2FramePayloadParser.ParseSettings(frame).ToArray());
    }

    [TestMethod]
    public async Task SettingsAcknowledgement_RoundTripsEmpty()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateSettingsAcknowledgement(), "000000 04 01 00000000");

        Assert.AreEqual(0, Http2FramePayloadParser.ParseSettings(frame).Count);
    }

    [TestMethod]
    public async Task PushPromise_Padded_RoundTrips()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreatePushPromise(1, 2, new byte[] { 0x82 }, isEndHeaders: true, padLength: 1), "000007 05 0c 00000001 01 00000002 82 00");
        var payload = Http2FramePayloadParser.ParsePushPromise(frame);

        Assert.AreEqual(2, payload.PromisedStreamId);
        CollectionAssert.AreEqual(new byte[] { 0x82 }, payload.Fragment.ToArray());
    }

    [TestMethod]
    public async Task PushPromise_Unpadded_RoundTrips()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreatePushPromise(1, 4, ReadOnlyMemory<byte>.Empty, isEndHeaders: false), "000004 05 00 00000001 00000004");

        Assert.AreEqual(4, Http2FramePayloadParser.ParsePushPromise(frame).PromisedStreamId);
    }

    [TestMethod]
    public async Task Ping_RoundTrips()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreatePing(0x1122334455667788, isAcknowledgement: false), "000008 06 00 00000000 1122334455667788");

        Assert.AreEqual(0x1122334455667788ul, Http2FramePayloadParser.ParsePing(frame));
    }

    [TestMethod]
    public async Task GoAway_RoundTripsWithDebugData()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateGoAway(5, Http2ErrorCode.EnhanceYourCalm, "calm"u8.ToArray()), "00000c 07 00 00000000 00000005 0000000b 63616c6d");
        var goAway = Http2FramePayloadParser.ParseGoAway(frame);

        Assert.AreEqual(5, goAway.LastStreamId);
        Assert.AreEqual(Http2ErrorCode.EnhanceYourCalm, goAway.ErrorCode);
        CollectionAssert.AreEqual("calm"u8.ToArray(), goAway.DebugData.ToArray());
    }

    [TestMethod]
    public async Task WindowUpdate_RoundTrips()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateWindowUpdate(0, int.MaxValue), "000004 08 00 00000000 7fffffff");

        Assert.AreEqual(int.MaxValue, Http2FramePayloadParser.ParseWindowUpdate(frame));
    }

    [TestMethod]
    public async Task Continuation_RoundTrips()
    {
        var frame = await RoundTrip(Http2FrameFactory.CreateContinuation(1, new byte[] { 0x84 }, isEndHeaders: true), "000001 09 04 00000001 84");

        CollectionAssert.AreEqual(new byte[] { 0x84 }, frame.Payload.ToArray());
    }

    [TestMethod]
    public void CreateData_PadLengthOutOfRange_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreateData(1, ReadOnlyMemory<byte>.Empty, isEndStream: false, padLength: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreateData(1, ReadOnlyMemory<byte>.Empty, isEndStream: false, padLength: 256));
    }

    [TestMethod]
    public void CreatePriority_WeightOutOfRange_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreatePriority(1, new Http2Priority(0, false, 0)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Http2FrameFactory.CreatePriority(1, new Http2Priority(0, false, 257)));
    }

    [TestMethod]
    public void CreateSettings_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => Http2FrameFactory.CreateSettings(null!));

    private static async Task<Http2Frame> RoundTrip(Http2Frame frame, string expectedHex)
    {
        var bytes = Http2FrameCodec.Serialize(frame);
        CollectionAssert.AreEqual(FromHex(expectedHex), bytes);
        var frames = await Http2Test.FramesIn(bytes);
        Assert.AreEqual(1, frames.Count);
        Http2Test.AssertFrame(frame, frames[0]);
        return frames[0];
    }
}
