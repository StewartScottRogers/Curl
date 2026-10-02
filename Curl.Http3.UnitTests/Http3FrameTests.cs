using static Curl.Http3.Http3;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins every frame type of RFC 9114 section 7.2: the bytes <see cref="Http3Frame.ToBytes" />
/// writes, the round trip through <see cref="Http3FrameReader" />, and the payload layouts
/// that are <c>H3_FRAME_ERROR</c> or <c>H3_SETTINGS_ERROR</c>.
/// </summary>
[TestClass]
public sealed class Http3FrameTests
{
    [TestMethod]
    public async Task Data_RoundTrips()
    {
        var bytes = new Http3DataFrame(FromHex("68656c6c6f")).ToBytes();

        CollectionAssert.AreEqual(FromHex("00 05 68656c6c6f"), bytes);
        var frame = (Http3DataFrame)await ReadOnlyFrameAsync(bytes);
        Assert.AreEqual(Http3FrameType.Data, frame.Type);
        CollectionAssert.AreEqual(FromHex("68656c6c6f"), frame.Payload.ToArray());
    }

    [TestMethod]
    public async Task Headers_RoundTrips()
    {
        var bytes = new Http3HeadersFrame(FromHex("0000d1")).ToBytes();

        CollectionAssert.AreEqual(FromHex("01 03 0000d1"), bytes);
        var frame = (Http3HeadersFrame)await ReadOnlyFrameAsync(bytes);
        Assert.AreEqual(Http3FrameType.Headers, frame.Type);
        CollectionAssert.AreEqual(FromHex("0000d1"), frame.EncodedFieldSection.ToArray());
    }

    [TestMethod]
    public async Task CancelPush_RoundTrips()
    {
        var bytes = new Http3CancelPushFrame(300).ToBytes();

        CollectionAssert.AreEqual(FromHex("03 02 412c"), bytes);
        var frame = (Http3CancelPushFrame)await ReadOnlyFrameAsync(bytes);
        Assert.AreEqual(Http3FrameType.CancelPush, frame.Type);
        Assert.AreEqual(300, frame.PushId);
    }

    [TestMethod]
    public async Task Settings_RoundTrips()
    {
        var bytes = new Http3SettingsFrame([new(0x06, 16384), new(0x01, 0)]).ToBytes();

        CollectionAssert.AreEqual(FromHex("04 07 06 80004000 01 00"), bytes);
        var frame = (Http3SettingsFrame)await ReadOnlyFrameAsync(bytes);
        Assert.AreEqual(Http3FrameType.Settings, frame.Type);
        CollectionAssert.AreEqual(new Http3Setting[] { new(0x06, 16384), new(0x01, 0) }, frame.Settings.ToArray());
    }

    [TestMethod]
    public async Task PushPromise_RoundTrips()
    {
        var bytes = new Http3PushPromiseFrame(7, FromHex("0000d1")).ToBytes();

        CollectionAssert.AreEqual(FromHex("05 04 07 0000d1"), bytes);
        var frame = (Http3PushPromiseFrame)await ReadOnlyFrameAsync(bytes);
        Assert.AreEqual(Http3FrameType.PushPromise, frame.Type);
        Assert.AreEqual(7, frame.PushId);
        CollectionAssert.AreEqual(FromHex("0000d1"), frame.EncodedFieldSection.ToArray());
    }

    [TestMethod]
    public async Task Goaway_RoundTrips()
    {
        var bytes = new Http3GoawayFrame(8).ToBytes();

        CollectionAssert.AreEqual(FromHex("07 01 08"), bytes);
        var frame = (Http3GoawayFrame)await ReadOnlyFrameAsync(bytes);
        Assert.AreEqual(Http3FrameType.Goaway, frame.Type);
        Assert.AreEqual(8, frame.Id);
    }

    [TestMethod]
    public async Task MaxPushId_RoundTrips()
    {
        var bytes = new Http3MaxPushIdFrame(15293).ToBytes();

        CollectionAssert.AreEqual(FromHex("0d 02 7bbd"), bytes);
        var frame = (Http3MaxPushIdFrame)await ReadOnlyFrameAsync(bytes);
        Assert.AreEqual(Http3FrameType.MaxPushId, frame.Type);
        Assert.AreEqual(15293, frame.PushId);
    }

    [TestMethod]
    public void ToBytes_IdOutsideTheRange_IsRejected() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Http3GoawayFrame(-1).ToBytes());

    [TestMethod]
    public void Settings_NullList_IsRejected() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new Http3SettingsFrame(null!));

    [TestMethod]
    public void GetValueOrDefault_GivesTheValueOrTheDefault()
    {
        Http3SettingsFrame frame = new([new(0x01, 4096), new(0x07, 16)]);

        Assert.AreEqual(16, frame.GetValueOrDefault(Http3SettingIdentifier.QpackBlockedStreams, 0));
        Assert.AreEqual(-1, frame.GetValueOrDefault(Http3SettingIdentifier.MaximumFieldSectionSize, -1));
    }

    [TestMethod]
    public async Task Settings_GreaseIdentifiers_AreLeftOut()
    {
        // 0x21 and 0x1f * 2 + 0x21 = 0x5f are reserved (RFC 9114 section 7.2.4.1); 0x33 is SETTINGS_H3_DATAGRAM and kept.
        var frame = (Http3SettingsFrame)await ReadOnlyFrameAsync(FromHex("04 09 21 05 06 10 405f 00 33 01"));

        CollectionAssert.AreEqual(new Http3Setting[] { new(0x06, 16), new(0x33, 1) }, frame.Settings.ToArray());
    }

    [TestMethod]
    [DataRow("04 04 01 00 01 05", DisplayName = "an identifier twice")]
    [DataRow("04 02 02 00", DisplayName = "HTTP/2 SETTINGS_ENABLE_PUSH")]
    [DataRow("04 02 05 00", DisplayName = "HTTP/2 SETTINGS_MAX_FRAME_SIZE")]
    [DataRow("04 02 08 02", DisplayName = "SETTINGS_ENABLE_CONNECT_PROTOCOL 2")]
    [DataRow("04 03 08 40 40", DisplayName = "SETTINGS_ENABLE_CONNECT_PROTOCOL 64")]
    [DataRow("04 02 33 02", DisplayName = "SETTINGS_H3_DATAGRAM 2")]
    [DataRow("04 09 33 c0 00 00 00 00 00 01 00", DisplayName = "SETTINGS_H3_DATAGRAM 256 in an eight-byte integer")]
    public async Task Settings_ForbiddenIdentifier_IsSettingsError(string hex) =>
        Assert.AreEqual(Http3ErrorCode.SettingsError, await ErrorOfAsync(() => ReadAllFramesAsync(StreamOf(hex))));

    [TestMethod]
    public async Task Settings_ZeroOrOneSettingsOfZeroAndOne_AreKept()
    {
        var frame = (Http3SettingsFrame)await ReadOnlyFrameAsync(FromHex("04 04 08 01 33 00"));

        CollectionAssert.AreEqual(
            new Http3Setting[] { new(Http3SettingIdentifier.EnableConnectProtocol, 1), new(Http3SettingIdentifier.H3Datagram, 0) },
            frame.Settings.ToArray());
    }

    [TestMethod]
    public async Task Settings_ZeroOrOneSettingsOfOneAndZero_AreKept()
    {
        var frame = (Http3SettingsFrame)await ReadOnlyFrameAsync(FromHex("04 04 08 00 33 01"));

        CollectionAssert.AreEqual(
            new Http3Setting[] { new(Http3SettingIdentifier.EnableConnectProtocol, 0), new(Http3SettingIdentifier.H3Datagram, 1) },
            frame.Settings.ToArray());
    }

    [TestMethod]
    [DataRow("04 01 06", DisplayName = "SETTINGS identifier without a value")]
    [DataRow("04 02 06 40", DisplayName = "SETTINGS value cut short")]
    [DataRow("03 00", DisplayName = "CANCEL_PUSH empty")]
    [DataRow("03 02 00 00", DisplayName = "CANCEL_PUSH with a byte too many")]
    [DataRow("07 01 40", DisplayName = "GOAWAY cut short")]
    [DataRow("0d 02 01 01", DisplayName = "MAX_PUSH_ID with a byte too many")]
    [DataRow("05 00", DisplayName = "PUSH_PROMISE without a push ID")]
    public async Task Payload_NotHoldingItsFields_IsFrameError(string hex) =>
        Assert.AreEqual(Http3ErrorCode.FrameError, await ErrorOfAsync(() => ReadAllFramesAsync(StreamOf(hex))));

    private static async Task<Http3Frame> ReadOnlyFrameAsync(byte[] bytes)
    {
        var frames = await ReadAllFramesAsync(new MemoryStream(bytes));
        Assert.HasCount(1, frames);
        return frames[0];
    }
}
