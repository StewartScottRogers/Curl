using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class HttpFrameLogTests
{
    [TestMethod]
    public void For_NoLog_GivesTheSilentLog() =>
        Assert.AreSame(HttpFrameLog.Silent, HttpFrameLog.For(null, "HTTP/2"));

    [TestMethod]
    [DataRow("HTTP/2", "http2")]
    [DataRow("HTTP/3", "http3")]
    public void For_ALog_WritesUnderTheVersionsComponent(string versionName, string component) =>
        Assert.AreEqual(component, HttpFrameLog.For(new RecordingDiagnosticLog(), versionName).Component);

    [TestMethod]
    public void FrameSentAndReceived_AtVerbose_WriteTheTypeStreamAndLength()
    {
        RecordingDiagnosticLog log = new();
        HttpFrameLog frames = new(log, DiagnosticLogComponents.Http2);

        frames.FrameSent("HEADERS", 3, 21);
        frames.FrameReceived("DATA", 3, 0);

        CollectionAssert.AreEqual(
            new[] { "HEADERS sent on stream 3, 21 bytes", "DATA received on stream 3, 0 bytes" },
            log.MessagesAt(DiagnosticLogLevel.Verbose));
    }

    [TestMethod]
    public void SettingsReceived_WithAConcurrencyLimit_WritesItAtInfo()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        Http2Settings settings = new();
        settings.Apply(new Http2Setting(Http2SettingIdentifier.MaxConcurrentStreams, 100));

        new HttpFrameLog(log, DiagnosticLogComponents.Http2).SettingsReceived(settings);

        CollectionAssert.AreEqual(
            new[] { "SETTINGS received: max concurrent streams 100, initial window 65535, max frame 16384, header table 4096" },
            log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void EveryLine_BelowItsLevel_IsNotWritten()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HttpFrameLog frames = new(log, DiagnosticLogComponents.Http2);

        frames.FrameSent("DATA", 1, 1);
        frames.FrameReceived("DATA", 1, 1);
        frames.SettingsReceived(new Http2Settings());
        frames.GoAwayReceived(new Http2GoAwayPayload(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        frames.ResetReceived(new Http2StreamResetException(1, Http2ErrorCode.Cancel));
        frames.StreamResetReceived(0, 0x10c);

        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public void StreamResetReceived_AtWarning_WritesTheStreamAndHexErrorCode()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).StreamResetReceived(4, 0x10c);

        Assert.AreEqual((DiagnosticLogLevel.Warning, "http3", "RESET_STREAM received on stream 4: error 0x10c"), log.Lines.Single());
    }

    [TestMethod]
    public void Http3SettingsReceived_EveryKnownSettingAndAnUnknownOne_WritesEachByNameOrHexIdentifierAtInfo()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        Http3SettingsFrame settings = new(
        [
            new Http3Setting(Http3SettingIdentifier.QpackMaximumTableCapacity, 0),
            new Http3Setting(Http3SettingIdentifier.MaximumFieldSectionSize, 100),
            new Http3Setting(Http3SettingIdentifier.QpackBlockedStreams, 2),
            new Http3Setting(Http3SettingIdentifier.EnableConnectProtocol, 1),
            new Http3Setting(Http3SettingIdentifier.H3Datagram, 1),
            new Http3Setting(0x21, 7),
        ]);

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).Http3SettingsReceived(settings);

        Assert.AreEqual(
            (DiagnosticLogLevel.Info, "http3", "SETTINGS received: QPACK_MAX_TABLE_CAPACITY 0, MAX_FIELD_SECTION_SIZE 100, QPACK_BLOCKED_STREAMS 2, ENABLE_CONNECT_PROTOCOL 1, H3_DATAGRAM 1, 0x21 7"),
            log.Lines.Single());
    }

    [TestMethod]
    public void Http3SettingsReceived_NoSettings_WritesNone()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).Http3SettingsReceived(new Http3SettingsFrame([]));

        CollectionAssert.AreEqual(new[] { "SETTINGS received: none" }, log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void Http3GoawayReceived_WritesItsStreamIdAtWarning()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).Http3GoawayReceived(8);

        Assert.AreEqual((DiagnosticLogLevel.Warning, "http3", "GOAWAY received: stream 8"), log.Lines.Single());
    }

    [TestMethod]
    public void Http3SettingsAndGoaway_BelowTheirLevel_AreNotWritten()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HttpFrameLog frames = new(log, DiagnosticLogComponents.Http3);

        frames.Http3SettingsReceived(new Http3SettingsFrame([]));
        frames.Http3GoawayReceived(0);

        Assert.IsEmpty(log.Lines);
    }
}
