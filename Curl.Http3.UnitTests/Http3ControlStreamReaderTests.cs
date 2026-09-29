using static Curl.Http3.Http3;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3ControlStreamReader" /> against RFC 9114 sections 6.2.1 and 7.2:
/// <c>SETTINGS</c> first and once, <c>GOAWAY</c> IDs, the frames a client may not receive
/// on the control stream, and the stream closing.
/// </summary>
[TestClass]
public sealed class Http3ControlStreamReaderTests
{
    private const string ServerSettings = "04 04 01 00 07 00";

    [TestMethod]
    public async Task ReadFrameAsync_SettingsThenGoaways_AreRecorded()
    {
        // A grease frame (0x21) between frames is skipped.
        Http3ControlStreamReader reader = new(StreamOf(ServerSettings + " 21 01 00 07 01 08 07 01 04 07 01 04"));
        Assert.IsNull(reader.PeerSettings);

        var settings = await reader.ReadFrameAsync(CancellationToken.None);
        Assert.AreSame(settings, reader.PeerSettings);
        Assert.IsNull(reader.GoawayStreamId);

        await reader.ReadFrameAsync(CancellationToken.None);
        Assert.AreEqual(8, reader.GoawayStreamId);
        await reader.ReadFrameAsync(CancellationToken.None);
        Assert.AreEqual(4, reader.GoawayStreamId);
        var repeated = (Http3GoawayFrame)await reader.ReadFrameAsync(CancellationToken.None);
        Assert.AreEqual(4, repeated.Id);
    }

    [TestMethod]
    [DataRow("07 01 00", DisplayName = "GOAWAY")]
    [DataRow("00 00", DisplayName = "DATA")]
    public async Task ReadFrameAsync_FirstFrameNotSettings_IsMissingSettings(string hex) =>
        Assert.AreEqual(Http3ErrorCode.MissingSettings, await ErrorOfFirstFramesAsync(hex, 1));

    [TestMethod]
    [DataRow(ServerSettings, DisplayName = "a second SETTINGS")]
    [DataRow("00 01 61", DisplayName = "DATA")]
    [DataRow("01 02 0000", DisplayName = "HEADERS")]
    [DataRow("05 03 00 0000", DisplayName = "PUSH_PROMISE")]
    [DataRow("0d 01 00", DisplayName = "MAX_PUSH_ID")]
    public async Task ReadFrameAsync_FrameNotAllowedOnTheControlStream_IsFrameUnexpected(string hex) =>
        Assert.AreEqual(Http3ErrorCode.FrameUnexpected, await ErrorOfFirstFramesAsync(ServerSettings + " " + hex, 2));

    [TestMethod]
    [DataRow("07 01 02", DisplayName = "GOAWAY naming a server-initiated stream")]
    [DataRow("07 01 01", DisplayName = "GOAWAY naming a unidirectional stream")]
    [DataRow("07 01 04 07 01 08", DisplayName = "GOAWAY naming a larger stream than before")]
    [DataRow("03 01 00", DisplayName = "CANCEL_PUSH without MAX_PUSH_ID")]
    public async Task ReadFrameAsync_InvalidId_IsIdError(string hex) =>
        Assert.AreEqual(Http3ErrorCode.IdError, await ErrorOfFirstFramesAsync(ServerSettings + " " + hex, 3));

    [TestMethod]
    [DataRow("", DisplayName = "before SETTINGS")]
    [DataRow("04 04 01", DisplayName = "inside SETTINGS")]
    [DataRow(ServerSettings, DisplayName = "after SETTINGS")]
    [DataRow(ServerSettings + " 07", DisplayName = "inside a frame after SETTINGS")]
    public async Task ReadFrameAsync_StreamEnding_IsClosedCriticalStream(string hex) =>
        Assert.AreEqual(Http3ErrorCode.ClosedCriticalStream, await ErrorOfFirstFramesAsync(hex, 2));

    [TestMethod]
    public async Task ReadFrameAsync_InvalidFrameBeforeTheEnd_KeepsItsError() =>
        Assert.AreEqual(Http3ErrorCode.SettingsError, await ErrorOfFirstFramesAsync("04 02 02 00 07 01 00", 1));

    private static Task<Http3ErrorCode> ErrorOfFirstFramesAsync(string hex, int frameCount) =>
        ErrorOfAsync(async () =>
        {
            Http3ControlStreamReader reader = new(StreamOf(hex));
            for (var index = 0; index < frameCount; index++)
            {
                await reader.ReadFrameAsync(CancellationToken.None);
            }
        });
}
