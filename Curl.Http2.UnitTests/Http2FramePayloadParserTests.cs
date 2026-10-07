using Curl.Testing;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2FramePayloadParser" /> rejects each malformed frame of RFC 9113
/// section 6 with the error code that section names.
/// </summary>
[TestClass]
public sealed class Http2FramePayloadParserTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ParseData_OnStreamZero_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Data, 0, 0)));

        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, 0, 0)));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, exception.ErrorCode);
        diagnostics.Assert("message", "HTTP/2 ProtocolError: Data frame on stream 0.", exception.Message);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 ProtocolError: Data frame on stream 0.", exception.Message);
    }

    [TestMethod]
    public void ParseData_PaddedWithNoPadLength_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParseData_PaddingAsLongAsThePayload_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, 2, 0)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, 2, 0)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParseData_PaddingFillingAllButThePadLength_LeavesNoData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, 1, 0)));

        var length = Http2FramePayloadParser.ParseData(Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, 1, 0)).Length;
        diagnostics.Act("data length", length);

        diagnostics.Assert("data length", 0, length);
        Assert.AreEqual(0, length);
    }

    [TestMethod]
    public void ParseHeaders_OnStreamZero_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Headers, 0, 0)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseHeaders(Frame(Http2FrameType.Headers, 0, 0)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParseHeaders_PriorityFlagWithoutTheFields_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Headers, Http2FrameFlags.Priority, 1, 0, 0, 0, 0)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseHeaders(Frame(Http2FrameType.Headers, Http2FrameFlags.Priority, 1, 0, 0, 0, 0)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParseHeaders_DependingOnItself_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Headers, Http2FrameFlags.Priority, 1, 0, 0, 0, 1, 15)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseHeaders(Frame(Http2FrameType.Headers, Http2FrameFlags.Priority, 1, 0, 0, 0, 1, 15)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParsePriority_OnStreamZero_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Priority, 0, 0, 0, 0, 0, 1, 15)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParsePriority(Frame(Http2FrameType.Priority, 0, 0, 0, 0, 0, 1, 15)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParsePriority_NotFiveBytes_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Priority, 0, 1, 0, 0, 0, 0)));

        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParsePriority(Frame(Http2FrameType.Priority, 0, 1, 0, 0, 0, 0)));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, exception.ErrorCode);
        diagnostics.Assert("message", "HTTP/2 FrameSizeError: Priority frame with a 4-byte payload.", exception.Message);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, exception.ErrorCode);
        Assert.AreEqual("HTTP/2 FrameSizeError: Priority frame with a 4-byte payload.", exception.Message);
    }

    [TestMethod]
    public void ParsePriority_DependingOnItself_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Priority, 0, 3, 0, 0, 0, 3, 15)));

        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParsePriority(Frame(Http2FrameType.Priority, 0, 3, 0, 0, 0, 3, 15)));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("message", "HTTP/2 ProtocolError: stream 3 depends on itself.", exception.Message);
        Assert.AreEqual("HTTP/2 ProtocolError: stream 3 depends on itself.", exception.Message);
    }

    [TestMethod]
    public void ParseRstStream_OnStreamZero_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.RstStream, 0, 0, 0, 0, 0, 8)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseRstStream(Frame(Http2FrameType.RstStream, 0, 0, 0, 0, 0, 8)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParseRstStream_NotFourBytes_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.RstStream, 0, 1, 0, 0, 8)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseRstStream(Frame(Http2FrameType.RstStream, 0, 1, 0, 0, 8)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParseSettings_OnAStream_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Settings, 0, 1)));

        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParseSettings(Frame(Http2FrameType.Settings, 0, 1)));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("message", "HTTP/2 ProtocolError: Settings frame on stream 1.", exception.Message);
        Assert.AreEqual("HTTP/2 ProtocolError: Settings frame on stream 1.", exception.Message);
    }

    [TestMethod]
    public void ParseSettings_AcknowledgementWithAPayload_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Settings, Http2FrameFlags.Acknowledgement, 0, 0, 1, 0, 0, 0, 0)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseSettings(Frame(Http2FrameType.Settings, Http2FrameFlags.Acknowledgement, 0, 0, 1, 0, 0, 0, 0)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParseSettings_LengthNotAMultipleOfSix_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Settings, 0, 0, 0, 1, 0, 0, 0)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseSettings(Frame(Http2FrameType.Settings, 0, 0, 0, 1, 0, 0, 0)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParsePushPromise_OnStreamZero_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.PushPromise, 0, 0, 0, 0, 0, 2)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParsePushPromise(Frame(Http2FrameType.PushPromise, 0, 0, 0, 0, 0, 2)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParsePushPromise_WithoutAPromisedStream_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.PushPromise, 0, 1, 0, 0, 2)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParsePushPromise(Frame(Http2FrameType.PushPromise, 0, 1, 0, 0, 2)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParsePing_OnAStream_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Ping, 0, 1, new byte[8])));

        var error = ErrorOf(() => Http2FramePayloadParser.ParsePing(Frame(Http2FrameType.Ping, 0, 1, new byte[8])));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParsePing_NotEightBytes_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.Ping, 0, 0, new byte[7])));

        var error = ErrorOf(() => Http2FramePayloadParser.ParsePing(Frame(Http2FrameType.Ping, 0, 0, new byte[7])));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParseGoAway_OnAStream_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.GoAway, 0, 1, new byte[8])));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseGoAway(Frame(Http2FrameType.GoAway, 0, 1, new byte[8])));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.ProtocolError, error);
        Assert.AreEqual(Http2ErrorCode.ProtocolError, error);
    }

    [TestMethod]
    public void ParseGoAway_ShorterThanEightBytes_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.GoAway, 0, 0, new byte[7])));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseGoAway(Frame(Http2FrameType.GoAway, 0, 0, new byte[7])));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParseGoAway_UnknownErrorCode_KeepsTheRawValue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.GoAway, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff)));

        var errorCode = Http2FramePayloadParser.ParseGoAway(Frame(Http2FrameType.GoAway, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff)).ErrorCode;
        diagnostics.Act("error code", (int)errorCode);

        diagnostics.Assert("error code", 0xff, (int)errorCode);
        Assert.AreEqual((Http2ErrorCode)0xff, errorCode);
    }

    [TestMethod]
    public void ParseWindowUpdate_NotFourBytes_IsAFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.WindowUpdate, 0, 0, 0, 0, 1)));

        var error = ErrorOf(() => Http2FramePayloadParser.ParseWindowUpdate(Frame(Http2FrameType.WindowUpdate, 0, 0, 0, 0, 1)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error);
    }

    [TestMethod]
    public void ParseWindowUpdate_ZeroIncrement_IsAProtocolError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", Describe(Frame(Http2FrameType.WindowUpdate, 0, 1, 0x80, 0, 0, 0)));

        var exception = Assert.ThrowsExactly<Http2ProtocolException>(() => Http2FramePayloadParser.ParseWindowUpdate(Frame(Http2FrameType.WindowUpdate, 0, 1, 0x80, 0, 0, 0)));
        diagnostics.Act("error code", exception.ErrorCode);
        diagnostics.Act("message", exception.Message);

        diagnostics.Assert("message", "HTTP/2 ProtocolError: WINDOW_UPDATE on stream 1 has an increment of 0.", exception.Message);
        Assert.AreEqual("HTTP/2 ProtocolError: WINDOW_UPDATE on stream 1 has an increment of 0.", exception.Message);
    }

    [TestMethod]
    public void Parse_NullFrame_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", "null passed to ParseData, ParsePing and ParseWindowUpdate");

        var dataException = Assert.ThrowsExactly<ArgumentNullException>(() => Http2FramePayloadParser.ParseData(null!));
        var pingException = Assert.ThrowsExactly<ArgumentNullException>(() => Http2FramePayloadParser.ParsePing(null!));
        var windowUpdateException = Assert.ThrowsExactly<ArgumentNullException>(() => Http2FramePayloadParser.ParseWindowUpdate(null!));
        diagnostics.Act("exception types", string.Join(", ", dataException.GetType().Name, pingException.GetType().Name, windowUpdateException.GetType().Name));

        diagnostics.Assert("exception types", string.Join(", ", nameof(ArgumentNullException), nameof(ArgumentNullException), nameof(ArgumentNullException)), string.Join(", ", dataException.GetType().Name, pingException.GetType().Name, windowUpdateException.GetType().Name));
    }

    private static Http2Frame Frame(Http2FrameType type, byte flags, int streamId, params byte[] payload) => new(type, flags, streamId, payload);

    private static string Describe(Http2Frame frame) =>
        $"{frame.Type}, flags {frame.Flags}, stream {frame.StreamId}, payload {Convert.ToHexString(frame.Payload.Span)}";
}
