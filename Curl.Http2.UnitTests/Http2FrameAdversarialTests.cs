using Curl.Testing;
using static Curl.Http2.Http2Test;

namespace Curl.Http2;

/// <summary>
/// Adversarial black-box attacks on the frame layer (BL-1499, by the method in
/// Documentation/Wiki/Adversarial-Testing.md): <see cref="Http2FrameCodec" /> at the frame
/// size limit and one past it, cut at every offset and fed one byte per read;
/// <see cref="Http2FramePayloadParser" /> with every fixed-length frame one byte short and
/// one long, on the wrong stream and with reserved bits set; and the
/// <see cref="Http2Settings" /> and <see cref="Http2FlowControlWindow" /> limits on both
/// sides. The oracle is RFC 9113 and each member's documented refusals.
/// </summary>
[TestClass]
public sealed class Http2FrameAdversarialTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ReadAsync_PayloadExactlyAtMaximumFrameSize_ReadsTheFrame()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = Wire(new Http2Frame(Http2FrameType.Data, 0, 1, new byte[Http2FrameCodec.DefaultMaximumFrameSize]));
        diagnostics.Arrange("payload length", Http2FrameCodec.DefaultMaximumFrameSize);

        var frame = await Http2FrameCodec.ReadAsync(new MemoryStream(wire), Http2FrameCodec.DefaultMaximumFrameSize, None);
        diagnostics.Act("read length", frame?.Payload.Length);

        diagnostics.Assert("read length", Http2FrameCodec.DefaultMaximumFrameSize, frame?.Payload.Length);
        Assert.AreEqual(Http2FrameCodec.DefaultMaximumFrameSize, frame?.Payload.Length);
    }

    [TestMethod]
    public async Task ReadAsync_PayloadOnePastMaximumFrameSize_RefusesWithFrameSizeError()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = Wire(new Http2Frame(Http2FrameType.Data, 0, 1, new byte[Http2FrameCodec.DefaultMaximumFrameSize + 1]));
        diagnostics.Arrange("payload length", Http2FrameCodec.DefaultMaximumFrameSize + 1);

        var error = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => Http2FrameCodec.ReadAsync(new MemoryStream(wire), Http2FrameCodec.DefaultMaximumFrameSize, None));
        diagnostics.Act("error code", error.ErrorCode);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error.ErrorCode);
    }

    [TestMethod]
    public async Task ReadAsync_HeaderClaimingLargestLengthWithNoPayload_RefusesBeforeReadingThePayload()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] header = [0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01];
        diagnostics.Bytes("header", header);

        var error = await Assert.ThrowsExactlyAsync<Http2ProtocolException>(() => Http2FrameCodec.ReadAsync(new MemoryStream(header), Http2FrameCodec.DefaultMaximumFrameSize, None));
        diagnostics.Act("error code", error.ErrorCode);

        diagnostics.Assert("error code", Http2ErrorCode.FrameSizeError, error.ErrorCode);
        Assert.AreEqual(Http2ErrorCode.FrameSizeError, error.ErrorCode);
    }

    [TestMethod]
    public async Task ReadAsync_FrameCutAtEveryOffset_ThrowsEndOfStream()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var wire = Wire(Http2FrameFactory.CreatePing(0x0102030405060708, isAcknowledgement: false));
        diagnostics.Bytes("frame", wire);

        for (var length = 1; length < wire.Length; length++)
        {
            _ = await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => Http2FrameCodec.ReadAsync(new MemoryStream(wire, 0, length), Http2FrameCodec.DefaultMaximumFrameSize, None), $"cut at {length}");
        }

        diagnostics.Act("offsets tried", wire.Length - 1);
        diagnostics.Assert("every cut refused", true, true);
    }

    [TestMethod]
    public async Task ReadAsync_OneBytePerRead_ReadsTheSameFrames()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Http2Frame[] sent = [Http2FrameFactory.CreateData(1, new byte[300], isEndStream: true), Http2FrameFactory.CreatePing(7, isAcknowledgement: true)];
        var peer = new PeerStream(Wire(sent), readSize: 1);
        diagnostics.Arrange("read size", 1);

        var first = await Http2FrameCodec.ReadAsync(peer, Http2FrameCodec.DefaultMaximumFrameSize, None);
        var second = await Http2FrameCodec.ReadAsync(peer, Http2FrameCodec.DefaultMaximumFrameSize, None);
        var end = await Http2FrameCodec.ReadAsync(peer, Http2FrameCodec.DefaultMaximumFrameSize, None);
        diagnostics.Act("frames", $"{first?.Type}, {second?.Type}, {end?.Type}");

        diagnostics.Assert("end", null, end);
        AssertFrame(sent[0], first!);
        AssertFrame(sent[1], second!);
        Assert.IsNull(end);
    }

    [TestMethod]
    public async Task ReadAsync_ReservedStreamBitSet_IgnoresTheBit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] wire = [0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x03];
        diagnostics.Bytes("frame", wire);

        var frame = await Http2FrameCodec.ReadAsync(new MemoryStream(wire), Http2FrameCodec.DefaultMaximumFrameSize, None);
        diagnostics.Act("stream", frame?.StreamId);

        diagnostics.Assert("stream", 3, frame?.StreamId);
        Assert.AreEqual(3, frame?.StreamId);
    }

    [TestMethod]
    public async Task ReadAsync_UnknownFrameType_ReadsItUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var sent = new Http2Frame((Http2FrameType)0xFA, 0xFF, 9, new byte[] { 1, 2, 3 });
        diagnostics.Arrange("type", 0xFA);

        var frame = await Http2FrameCodec.ReadAsync(new MemoryStream(Wire(sent)), Http2FrameCodec.DefaultMaximumFrameSize, None);
        diagnostics.Act("type", frame?.Type);

        diagnostics.Assert("type", sent.Type, frame?.Type);
        AssertFrame(sent, frame!);
    }

    [TestMethod]
    public async Task ReadAsync_CancelledBeforeTheCall_ThrowsCancellation()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        diagnostics.Arrange("token", "cancelled");

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => Http2FrameCodec.ReadAsync(new MemoryStream(Wire(Http2FrameFactory.CreateSettingsAcknowledgement())), Http2FrameCodec.DefaultMaximumFrameSize, cancellation.Token));
        diagnostics.Act("exception", error.GetType().Name);

        diagnostics.Assert("token", cancellation.Token, error.CancellationToken);
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
    }

    [TestMethod]
    [DataRow(0x80000000u, DisplayName = "reserved bit only")]
    [DataRow(0x00000000u, DisplayName = "zero")]
    public void ParseWindowUpdate_IncrementOfZeroOnceReservedBitIsIgnored_RefusesWithProtocolError(uint increment)
    {
        AssertRefused(WindowUpdate(1, increment), Http2FramePayloadParser.ParseWindowUpdate, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    [DataRow(0x7FFFFFFFu, DisplayName = "2^31 - 1")]
    [DataRow(0xFFFFFFFFu, DisplayName = "2^31 - 1 with the reserved bit")]
    public void ParseWindowUpdate_LargestIncrement_ReturnsTwoToTheThirtyFirstMinusOne(uint increment)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("increment field", increment.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));

        var parsed = Http2FramePayloadParser.ParseWindowUpdate(WindowUpdate(0, increment));
        diagnostics.Act("increment", parsed);

        diagnostics.Assert("increment", int.MaxValue, parsed);
        Assert.AreEqual(int.MaxValue, parsed);
    }

    [TestMethod]
    [DataRow(Http2FrameType.WindowUpdate, 0, 3)]
    [DataRow(Http2FrameType.WindowUpdate, 0, 5)]
    [DataRow(Http2FrameType.RstStream, 1, 3)]
    [DataRow(Http2FrameType.RstStream, 1, 5)]
    [DataRow(Http2FrameType.Priority, 1, 4)]
    [DataRow(Http2FrameType.Priority, 1, 6)]
    [DataRow(Http2FrameType.Ping, 0, 7)]
    [DataRow(Http2FrameType.Ping, 0, 9)]
    [DataRow(Http2FrameType.Settings, 0, 5)]
    [DataRow(Http2FrameType.Settings, 0, 7)]
    [DataRow(Http2FrameType.GoAway, 0, 7)]
    [DataRow(Http2FrameType.PushPromise, 1, 3)]
    public void Parse_FixedLengthFrameOneByteShortOrLong_RefusesWithFrameSizeError(Http2FrameType type, int streamId, int length)
    {
        AssertRefused(new Http2Frame(type, 0, streamId, new byte[length]), ParserFor(type), Http2ErrorCode.FrameSizeError);
    }

    [TestMethod]
    [DataRow(Http2FrameType.Data, 0)]
    [DataRow(Http2FrameType.Headers, 0)]
    [DataRow(Http2FrameType.Priority, 0)]
    [DataRow(Http2FrameType.RstStream, 0)]
    [DataRow(Http2FrameType.PushPromise, 0)]
    [DataRow(Http2FrameType.Settings, 1)]
    [DataRow(Http2FrameType.Ping, 1)]
    [DataRow(Http2FrameType.GoAway, 1)]
    public void Parse_FrameOnTheWrongStream_RefusesWithProtocolError(Http2FrameType type, int streamId)
    {
        var length = type switch
        {
            Http2FrameType.Priority => 5,
            Http2FrameType.RstStream or Http2FrameType.PushPromise => 4,
            Http2FrameType.Ping or Http2FrameType.GoAway => 8,
            _ => 0,
        };
        AssertRefused(new Http2Frame(type, 0, streamId, new byte[length]), ParserFor(type), Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public void ParseSettings_AcknowledgementWithOneEntry_RefusesWithFrameSizeError()
    {
        AssertRefused(new Http2Frame(Http2FrameType.Settings, Http2FrameFlags.Acknowledgement, 0, new byte[6]), Http2FramePayloadParser.ParseSettings, Http2ErrorCode.FrameSizeError);
    }

    [TestMethod]
    [DataRow(Http2FrameType.Data)]
    [DataRow(Http2FrameType.Headers)]
    [DataRow(Http2FrameType.PushPromise)]
    public void Parse_PaddedFlagWithEmptyPayload_RefusesWithProtocolError(Http2FrameType type)
    {
        AssertRefused(new Http2Frame(type, Http2FrameFlags.Padded, 1, ReadOnlyMemory<byte>.Empty), ParserFor(type), Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public void ParseData_PadLengthEqualToPayloadLength_RefusesWithProtocolError()
    {
        AssertRefused(new Http2Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, new byte[] { 3, 0, 0 }), Http2FramePayloadParser.ParseData, Http2ErrorCode.ProtocolError);
    }

    [TestMethod]
    public void ParseData_PadLengthOneBelowPayloadLength_ReturnsEmptyData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var frame = new Http2Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, PaddedWithFullPadLength());
        diagnostics.Arrange("pad length", 255);

        var data = Http2FramePayloadParser.ParseData(frame);
        diagnostics.Act("data length", data.Length);

        diagnostics.Assert("data length", 0, data.Length);
        Assert.AreEqual(0, data.Length);
    }

    [TestMethod]
    public void ParseHeaders_PaddedPriorityFrameTooShortForPriorityOncePaddingIsRemoved_RefusesWithFrameSizeError()
    {
        var frame = new Http2Frame(Http2FrameType.Headers, Http2FrameFlags.Padded | Http2FrameFlags.Priority, 1, new byte[] { 2, 0, 0, 0, 0, 9, 9 });
        AssertRefused(frame, Http2FramePayloadParser.ParseHeaders, Http2ErrorCode.FrameSizeError);
    }

    [TestMethod]
    public void ParsePushPromise_PaddedFrameTooShortForPromisedStreamOncePaddingIsRemoved_RefusesWithFrameSizeError()
    {
        var frame = new Http2Frame(Http2FrameType.PushPromise, Http2FrameFlags.Padded, 1, new byte[] { 1, 0, 0, 2, 9 });
        AssertRefused(frame, Http2FramePayloadParser.ParsePushPromise, Http2ErrorCode.FrameSizeError);
    }

    [TestMethod]
    public void ParseGoAway_ReservedBitInLastStream_IgnoresTheBit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var frame = new Http2Frame(Http2FrameType.GoAway, 0, 0, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0 });
        diagnostics.Bytes("payload", frame.Payload.Span);

        var goAway = Http2FramePayloadParser.ParseGoAway(frame);
        diagnostics.Act("last stream", goAway.LastStreamId);

        diagnostics.Assert("last stream", int.MaxValue, goAway.LastStreamId);
        Assert.AreEqual(int.MaxValue, goAway.LastStreamId);
        Assert.AreEqual(0, goAway.DebugData.Length);
    }

    [TestMethod]
    public void Parse_NullFrame_ThrowsArgumentNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Http2FrameType[] types = [Http2FrameType.Data, Http2FrameType.Headers, Http2FrameType.Priority, Http2FrameType.RstStream, Http2FrameType.Settings, Http2FrameType.PushPromise, Http2FrameType.Ping, Http2FrameType.GoAway, Http2FrameType.WindowUpdate];
        diagnostics.Arrange("parsers", types.Length);

        foreach (var type in types)
        {
            _ = Assert.ThrowsExactly<ArgumentNullException>(() => ParserFor(type)(null!), type.ToString());
        }

        diagnostics.Act("parsers tried", types.Length);
        diagnostics.Assert("every parser refused null", true, true);
        _ = Assert.ThrowsExactly<ArgumentNullException>(() => Http2FrameCodec.Serialize(null!));
    }

    [TestMethod]
    [DataRow(Http2SettingIdentifier.MaxFrameSize, 16383u, Http2ErrorCode.ProtocolError)]
    [DataRow(Http2SettingIdentifier.MaxFrameSize, 16777216u, Http2ErrorCode.ProtocolError)]
    [DataRow(Http2SettingIdentifier.MaxFrameSize, uint.MaxValue, Http2ErrorCode.ProtocolError)]
    [DataRow(Http2SettingIdentifier.InitialWindowSize, 2147483648u, Http2ErrorCode.FlowControlError)]
    [DataRow(Http2SettingIdentifier.InitialWindowSize, uint.MaxValue, Http2ErrorCode.FlowControlError)]
    [DataRow(Http2SettingIdentifier.EnablePush, 2u, Http2ErrorCode.ProtocolError)]
    public void SettingsApply_ValueJustOutsideItsRange_RefusesAndKeepsThePreviousValue(Http2SettingIdentifier identifier, uint value, Http2ErrorCode expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = new Http2Settings();
        diagnostics.Arrange("setting", $"{identifier} = {value}");

        var error = ErrorOf(() => settings.Apply(new Http2Setting(identifier, value)));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", expected, error);
        Assert.AreEqual(expected, error);
        Assert.AreEqual(Http2FrameCodec.DefaultMaximumFrameSize, settings.MaxFrameSize);
        Assert.AreEqual(Http2Settings.DefaultInitialWindowSize, settings.InitialWindowSize);
        Assert.IsTrue(settings.IsPushEnabled);
    }

    [TestMethod]
    public void SettingsApply_ValuesExactlyAtTheirLimitsAndAnUnknownIdentifier_AreAccepted()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var settings = new Http2Settings();
        diagnostics.Arrange("settings", "MAX_FRAME_SIZE 16384 then 2^24 - 1, INITIAL_WINDOW_SIZE 2^31 - 1, identifier 0xFFFF");

        settings.Apply(new Http2Setting(Http2SettingIdentifier.MaxFrameSize, 16384));
        settings.Apply(new Http2Setting(Http2SettingIdentifier.MaxFrameSize, 16777215));
        settings.Apply(new Http2Setting(Http2SettingIdentifier.InitialWindowSize, int.MaxValue));
        settings.Apply(new Http2Setting((Http2SettingIdentifier)0xFFFF, uint.MaxValue));
        diagnostics.Act("max frame size", settings.MaxFrameSize);

        diagnostics.Assert("max frame size", 16777215, settings.MaxFrameSize);
        Assert.AreEqual(16777215, settings.MaxFrameSize);
        Assert.AreEqual(int.MaxValue, settings.InitialWindowSize);
    }

    [TestMethod]
    public void FlowControlWindow_GrownExactlyToTheMaximumThenByOne_RefusesTheSecondAndKeepsTheSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(Http2Settings.DefaultInitialWindowSize);
        diagnostics.Arrange("size", window.Size);

        var reachedMaximum = window.TryAdjust(Http2FlowControlWindow.MaximumSize - Http2Settings.DefaultInitialWindowSize);
        var passedMaximum = window.TryAdjust(1);
        diagnostics.Act("adjusted", $"{reachedMaximum}, {passedMaximum}");

        diagnostics.Assert("size", (long)int.MaxValue, window.Size);
        Assert.IsTrue(reachedMaximum);
        Assert.IsFalse(passedMaximum);
        Assert.AreEqual(int.MaxValue, window.Size);
    }

    [TestMethod]
    public void FlowControlWindow_ConsumeExactlyItsSizeThenOneMore_EmptiesItThenRefuses()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(100);
        diagnostics.Arrange("size", 100);

        var tookAll = window.TryConsume(100);
        var tookOneMore = window.TryConsume(1);
        var tookNothing = window.TryConsume(0);
        diagnostics.Act("consumed", $"{tookAll}, {tookOneMore}, {tookNothing}");

        diagnostics.Assert("size", 0L, window.Size);
        Assert.IsTrue(tookAll);
        Assert.IsFalse(tookOneMore);
        Assert.IsTrue(tookNothing);
        Assert.AreEqual(0, window.Size);
        _ = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => window.TryConsume(-1));
    }

    [TestMethod]
    public void FlowControlWindow_DrivenNegative_ReportsNothingAvailableAndRefusesAnyConsumption()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(0);
        diagnostics.Arrange("size", 0);

        var shrank = window.TryAdjust(-int.MaxValue);
        diagnostics.Act("size", window.Size);

        diagnostics.Assert("available", 0, window.Available);
        Assert.IsTrue(shrank);
        Assert.AreEqual(-int.MaxValue, window.Size);
        Assert.AreEqual(0, window.Available);
        Assert.IsFalse(window.TryConsume(1));
    }

    /// <summary>A pad length of 255 followed by 255 bytes of padding and no data.</summary>
    private static byte[] PaddedWithFullPadLength()
    {
        var payload = new byte[256];
        payload[0] = 255;
        return payload;
    }

    private static Http2Frame WindowUpdate(int streamId, uint increment) =>
        new(Http2FrameType.WindowUpdate, 0, streamId, new byte[] { (byte)(increment >> 24), (byte)(increment >> 16), (byte)(increment >> 8), (byte)increment });

    private static Action<Http2Frame> ParserFor(Http2FrameType type) => type switch
    {
        Http2FrameType.Data => frame => Http2FramePayloadParser.ParseData(frame),
        Http2FrameType.Headers => frame => Http2FramePayloadParser.ParseHeaders(frame),
        Http2FrameType.Priority => frame => Http2FramePayloadParser.ParsePriority(frame),
        Http2FrameType.RstStream => frame => Http2FramePayloadParser.ParseRstStream(frame),
        Http2FrameType.Settings => frame => Http2FramePayloadParser.ParseSettings(frame),
        Http2FrameType.PushPromise => frame => Http2FramePayloadParser.ParsePushPromise(frame),
        Http2FrameType.Ping => frame => Http2FramePayloadParser.ParsePing(frame),
        Http2FrameType.GoAway => frame => Http2FramePayloadParser.ParseGoAway(frame),
        _ => frame => Http2FramePayloadParser.ParseWindowUpdate(frame),
    };

    private void AssertRefused<T>(Http2Frame frame, Func<Http2Frame, T> parse, Http2ErrorCode expected) =>
        AssertRefused(frame, parsed => { _ = parse(parsed); }, expected);

    private void AssertRefused(Http2Frame frame, Action<Http2Frame> parse, Http2ErrorCode expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("frame", $"{frame.Type} flags {frame.Flags} stream {frame.StreamId} length {frame.Payload.Length}");

        var error = ErrorOf(() => parse(frame));
        diagnostics.Act("error code", error);

        diagnostics.Assert("error code", expected, error);
        Assert.AreEqual(expected, error);
    }
}
