using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2FramePayloadParser" /> rejects each malformed frame of RFC 9113
/// section 6 with the error code that section names.
/// </summary>
[TestClass]
public sealed class Http2FramePayloadParserTests
{
    [TestMethod]
    public void ParseData_OnStreamZero_IsAProtocolError()
    {
        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, 0, 0)));

        Assert.AreEqual(Http2ErrorCode.ProtocolError, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 ProtocolError: Data frame on stream 0.", exception.Message);
    }

    [TestMethod]
    public void ParseData_PaddedWithNoPadLength_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1))));

    [TestMethod]
    public void ParseData_PaddingAsLongAsThePayload_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, 2, 0))));

    [TestMethod]
    public void ParseData_PaddingFillingAllButThePadLength_LeavesNoData() =>
        Assert.AreEqual(0, Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, 1, 0)).Length);

    [TestMethod]
    public void ParseHeaders_OnStreamZero_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParseHeaders(Frame(Http2FrameType.Headers, 0, 0))));

    [TestMethod]
    public void ParseHeaders_PriorityFlagWithoutTheFields_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParseHeaders(Frame(Http2FrameType.Headers, Http2FrameFlags.Priority, 1, 0, 0, 0, 0))));

    [TestMethod]
    public void ParseHeaders_DependingOnItself_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParseHeaders(Frame(Http2FrameType.Headers, Http2FrameFlags.Priority, 1, 0, 0, 0, 1, 15))));

    [TestMethod]
    public void ParsePriority_OnStreamZero_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParsePriority(Frame(Http2FrameType.Priority, 0, 0, 0, 0, 0, 1, 15))));

    [TestMethod]
    public void ParsePriority_NotFiveBytes_IsAFrameSizeError()
    {
        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParsePriority(Frame(Http2FrameType.Priority, 0, 1, 0, 0, 0, 0)));

        Assert.AreEqual(Http2ErrorCode.FrameSizeError, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 FrameSizeError: Priority frame with a 4-byte payload.", exception.Message);
    }

    [TestMethod]
    public void ParsePriority_DependingOnItself_IsAProtocolError()
    {
        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParsePriority(Frame(Http2FrameType.Priority, 0, 3, 0, 0, 0, 3, 15)));

        Assert.AreEqual("HTTP/2 ProtocolError: stream 3 depends on itself.", exception.Message);
    }

    [TestMethod]
    public void ParseRstStream_OnStreamZero_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParseRstStream(Frame(Http2FrameType.RstStream, 0, 0, 0, 0, 0, 8))));

    [TestMethod]
    public void ParseRstStream_NotFourBytes_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParseRstStream(Frame(Http2FrameType.RstStream, 0, 1, 0, 0, 8))));

    [TestMethod]
    public void ParseSettings_OnAStream_IsAProtocolError()
    {
        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParseSettings(Frame(Http2FrameType.Settings, 0, 1)));

        Assert.AreEqual("HTTP/2 ProtocolError: Settings frame on stream 1.", exception.Message);
    }

    [TestMethod]
    public void ParseSettings_AcknowledgementWithAPayload_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParseSettings(Frame(Http2FrameType.Settings, Http2FrameFlags.Acknowledgement, 0, 0, 1, 0, 0, 0, 0))));

    [TestMethod]
    public void ParseSettings_LengthNotAMultipleOfSix_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParseSettings(Frame(Http2FrameType.Settings, 0, 0, 0, 1, 0, 0, 0))));

    [TestMethod]
    public void ParsePushPromise_OnStreamZero_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParsePushPromise(Frame(Http2FrameType.PushPromise, 0, 0, 0, 0, 0, 2))));

    [TestMethod]
    public void ParsePushPromise_WithoutAPromisedStream_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParsePushPromise(Frame(Http2FrameType.PushPromise, 0, 1, 0, 0, 2))));

    [TestMethod]
    public void ParsePing_OnAStream_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParsePing(Frame(Http2FrameType.Ping, 0, 1, new byte[8]))));

    [TestMethod]
    public void ParsePing_NotEightBytes_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParsePing(Frame(Http2FrameType.Ping, 0, 0, new byte[7]))));

    [TestMethod]
    public void ParseGoAway_OnAStream_IsAProtocolError() =>
        Assert.AreEqual(Http2ErrorCode.ProtocolError, ErrorOf(() => Http2FramePayloadParser.ParseGoAway(Frame(Http2FrameType.GoAway, 0, 1, new byte[8]))));

    [TestMethod]
    public void ParseGoAway_ShorterThanEightBytes_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParseGoAway(Frame(Http2FrameType.GoAway, 0, 0, new byte[7]))));

    [TestMethod]
    public void ParseGoAway_UnknownErrorCode_KeepsTheRawValue() =>
        Assert.AreEqual((Http2ErrorCode)0xff, Http2FramePayloadParser.ParseGoAway(Frame(Http2FrameType.GoAway, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff)).ErrorCode);

    [TestMethod]
    public void ParseWindowUpdate_NotFourBytes_IsAFrameSizeError() =>
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, ErrorOf(() => Http2FramePayloadParser.ParseWindowUpdate(Frame(Http2FrameType.WindowUpdate, 0, 0, 0, 0, 1))));

    [TestMethod]
    public void ParseWindowUpdate_ZeroIncrement_IsAProtocolError()
    {
        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParseWindowUpdate(Frame(Http2FrameType.WindowUpdate, 0, 1, 0x80, 0, 0, 0)));

        Assert.AreEqual("HTTP/2 ProtocolError: WINDOW_UPDATE on stream 1 has an increment of 0.", exception.Message);
    }

    [TestMethod]
    public void Parse_NullFrame_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Http2FramePayloadParser.ParseData(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Http2FramePayloadParser.ParsePing(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => Http2FramePayloadParser.ParseWindowUpdate(null!));
    }

    private static Http2Frame Frame(Http2FrameType type, byte flags, int streamId, params byte[] payload) => new(type, flags, streamId, payload);
}
