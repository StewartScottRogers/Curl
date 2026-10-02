using Curl.Http2;
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
}
