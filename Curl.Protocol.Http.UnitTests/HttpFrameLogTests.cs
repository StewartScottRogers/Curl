using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

[TestClass]
public sealed class HttpFrameLogTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void For_NoLog_GivesTheSilentLog()
    {
        Diagnostics.Arrange("log, version", "none, HTTP/2");

        HttpFrameLog frames = HttpFrameLog.For(null, "HTTP/2");

        Diagnostics.Act("is the silent log", ReferenceEquals(HttpFrameLog.Silent, frames));
        Diagnostics.Assert("is the silent log", true, ReferenceEquals(HttpFrameLog.Silent, frames));
        Assert.AreSame(HttpFrameLog.Silent, frames);
    }

    [TestMethod]
    [DataRow("HTTP/2", "http2")]
    [DataRow("HTTP/3", "http3")]
    public void For_ALog_WritesUnderTheVersionsComponent(string versionName, string component)
    {
        Diagnostics.Arrange("version", versionName);

        string actual = HttpFrameLog.For(new RecordingDiagnosticLog(), versionName).Component;

        Diagnostics.Act("component", actual);
        Diagnostics.Assert("component", component, actual);
        Assert.AreEqual(component, actual);
    }

    [TestMethod]
    public void FrameSentAndReceived_AtVerbose_WriteTheTypeStreamAndLength()
    {
        RecordingDiagnosticLog log = new();
        HttpFrameLog frames = new(log, DiagnosticLogComponents.Http2);
        Diagnostics.Arrange("frames", "HEADERS sent on 3 (21 bytes), DATA received on 3 (0 bytes)");

        frames.FrameSent("HEADERS", 3, 21);
        frames.FrameReceived("DATA", 3, 0);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert(
            "verbose",
            "HEADERS sent on stream 3, 21 bytes | DATA received on stream 3, 0 bytes",
            string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Verbose)));
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
        Diagnostics.Arrange("settings", "max concurrent streams 100, the rest default");

        new HttpFrameLog(log, DiagnosticLogComponents.Http2).SettingsReceived(settings);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert(
            "info",
            "SETTINGS received: max concurrent streams 100, initial window 65535, max frame 16384, header table 4096",
            string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.AreEqual(
            new[] { "SETTINGS received: max concurrent streams 100, initial window 65535, max frame 16384, header table 4096" },
            log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void EveryLine_BelowItsLevel_IsNotWritten()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HttpFrameLog frames = new(log, DiagnosticLogComponents.Http2);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Error);

        frames.FrameSent("DATA", 1, 1);
        frames.FrameReceived("DATA", 1, 1);
        frames.SettingsReceived(new Http2Settings());
        frames.GoAwayReceived(new Http2GoAwayPayload(1, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty));
        frames.ResetReceived(new Http2StreamResetException(1, Http2ErrorCode.Cancel));
        frames.StreamResetReceived(0, 0x10c);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public void StreamResetReceived_AtWarning_WritesTheStreamAndHexErrorCode()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        Diagnostics.Arrange("stream, error code", "4, 0x10c");

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).StreamResetReceived(4, 0x10c);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", "Warning http3 RESET_STREAM received on stream 4: error 0x10c", Logged(log));
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
        Diagnostics.Arrange("settings", "five known settings and unknown 0x21 = 7");

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).Http3SettingsReceived(settings);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert(
            "logged",
            "Info http3 SETTINGS received: QPACK_MAX_TABLE_CAPACITY 0, MAX_FIELD_SECTION_SIZE 100, QPACK_BLOCKED_STREAMS 2, ENABLE_CONNECT_PROTOCOL 1, H3_DATAGRAM 1, 0x21 7",
            Logged(log));
        Assert.AreEqual(
            (DiagnosticLogLevel.Info, "http3", "SETTINGS received: QPACK_MAX_TABLE_CAPACITY 0, MAX_FIELD_SECTION_SIZE 100, QPACK_BLOCKED_STREAMS 2, ENABLE_CONNECT_PROTOCOL 1, H3_DATAGRAM 1, 0x21 7"),
            log.Lines.Single());
    }

    [TestMethod]
    public void Http3SettingsReceived_NoSettings_WritesNone()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Info);
        Diagnostics.Arrange("settings", "none");

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).Http3SettingsReceived(new Http3SettingsFrame([]));

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("info", "SETTINGS received: none", string.Join(" | ", log.MessagesAt(DiagnosticLogLevel.Info)));
        CollectionAssert.AreEqual(new[] { "SETTINGS received: none" }, log.MessagesAt(DiagnosticLogLevel.Info));
    }

    [TestMethod]
    public void Http3GoawayReceived_WritesItsStreamIdAtWarning()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Warning);
        Diagnostics.Arrange("GOAWAY stream", 8);

        new HttpFrameLog(log, DiagnosticLogComponents.Http3).Http3GoawayReceived(8);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("logged", "Warning http3 GOAWAY received: stream 8", Logged(log));
        Assert.AreEqual((DiagnosticLogLevel.Warning, "http3", "GOAWAY received: stream 8"), log.Lines.Single());
    }

    [TestMethod]
    public void Http3SettingsAndGoaway_BelowTheirLevel_AreNotWritten()
    {
        RecordingDiagnosticLog log = new(DiagnosticLogLevel.Error);
        HttpFrameLog frames = new(log, DiagnosticLogComponents.Http3);
        Diagnostics.Arrange("log level", DiagnosticLogLevel.Error);

        frames.Http3SettingsReceived(new Http3SettingsFrame([]));
        frames.Http3GoawayReceived(0);

        Diagnostics.Act("logged", Logged(log));
        Diagnostics.Assert("line count", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }

    private static string Logged(RecordingDiagnosticLog log) =>
        log.Lines.Count == 0 ? "(nothing)" : string.Join(" | ", log.Lines.Select(line => $"{line.Level} {line.Component} {line.Message}"));
}
