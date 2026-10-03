using System.Text;
using Curl.Http2;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins how <see cref="Http2FrameTrace" /> describes each frame type as curl 8.21.0's <c>fr_print</c>
/// does, and the <c>[HTTP/2]</c> lines it reports for frames sent and received (BL-1167, ADR-0373).
/// </summary>
[TestClass]
public sealed class Http2FrameTraceTests
{
    public static IEnumerable<object[]> Frames =>
    [
        [Http2FrameFactory.CreateData(1, new byte[577], isEndStream: false), "FRAME[DATA, len=577, eos=0, padlen=0]"],
        [Http2FrameFactory.CreateData(1, new byte[3], isEndStream: true, padLength: 4), "FRAME[DATA, len=8, eos=1, padlen=5]"],
        [Http2FrameFactory.CreateHeaders(1, new byte[28], isEndStream: true, isEndHeaders: true), "FRAME[HEADERS, len=28, hend=1, eos=1]"],
        [Http2FrameFactory.CreateHeaders(3, new byte[9], isEndStream: false, isEndHeaders: false), "FRAME[HEADERS, len=9, hend=0, eos=0]"],
        [Http2FrameFactory.CreatePriority(1, new Http2Priority(0, false, 16)), "FRAME[PRIORITY, len=5, flags=0]"],
        [Http2FrameFactory.CreateRstStream(1, Http2ErrorCode.Cancel), "FRAME[RST_STREAM, len=4, flags=0, error=8]"],
        [Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.MaxConcurrentStreams, 100)]), "FRAME[SETTINGS, len=6]"],
        [Http2FrameFactory.CreateSettingsAcknowledgement(), "FRAME[SETTINGS, ack=1]"],
        [Http2FrameFactory.CreatePushPromise(1, 2, new byte[4], isEndHeaders: true), "FRAME[PUSH_PROMISE, len=8, hend=1]"],
        [Http2FrameFactory.CreatePing(7, isAcknowledgement: false), "FRAME[PING, len=8, ack=0]"],
        [Http2FrameFactory.CreatePing(7, isAcknowledgement: true), "FRAME[PING, len=8, ack=1]"],
        [Http2FrameFactory.CreateGoAway(0, Http2ErrorCode.NoError, "shutdown\0"u8.ToArray()), "FRAME[GOAWAY, error=0, reason='shutdown', last_stream=0]"],
        [Http2FrameFactory.CreateGoAway(5, Http2ErrorCode.ProtocolError, ReadOnlyMemory<byte>.Empty), "FRAME[GOAWAY, error=1, reason='', last_stream=5]"],
        [Http2FrameFactory.CreateGoAway(1, Http2ErrorCode.InternalError, Encoding.ASCII.GetBytes(new string('r', 200))), $"FRAME[GOAWAY, error=2, reason='{new string('r', 127)}', last_stream=1]"],
        [Http2FrameFactory.CreateWindowUpdate(0, 1048510465), "FRAME[WINDOW_UPDATE, incr=1048510465]"],
        [new Http2Frame((Http2FrameType)0x20, 3, 0, new byte[2]), "FRAME[32, len=2, flags=3]"],
        [new Http2Frame(Http2FrameType.Data, Http2FrameFlags.Padded, 1, ReadOnlyMemory<byte>.Empty), "FRAME[DATA, len=0, eos=0, padlen=0]"],
    ];

    [TestMethod]
    [DynamicData(nameof(Frames))]
    public void Describe_DescribesTheFrameAsCurlDoes(Http2Frame frame, string expected) =>
        Assert.AreEqual(expected, Http2FrameTrace.Describe(frame));

    [TestMethod]
    public void FrameSent_ReportsTheFrameOnItsStream()
    {
        RecordingTransferEvents events = new();

        new Http2FrameTrace(events).FrameSent(Http2FrameFactory.CreateWindowUpdate(1, 10420225));

        CollectionAssert.AreEqual(new[] { "[HTTP/2] [1] -> FRAME[WINDOW_UPDATE, incr=10420225]" }, events.Info);
    }

    [TestMethod]
    public void FrameSentAndReceived_Continuation_ReportsNothing()
    {
        RecordingTransferEvents events = new();
        Http2FrameTrace trace = new(events);

        trace.FrameSent(Http2FrameFactory.CreateContinuation(1, new byte[3], isEndHeaders: true));
        trace.FrameReceived(Http2FrameFactory.CreateContinuation(1, new byte[3], isEndHeaders: true));

        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public void FrameReceived_Settings_ReportsTheFrameThenTheServersConcurrencyAndPush()
    {
        RecordingTransferEvents events = new();
        Http2FrameTrace trace = new(events);

        trace.FrameReceived(Http2FrameFactory.CreateSettings(
        [
            new(Http2SettingIdentifier.MaxConcurrentStreams, 100),
            new(Http2SettingIdentifier.InitialWindowSize, 65536),
            new(Http2SettingIdentifier.MaxHeaderListSize, 262144),
        ]));
        trace.FrameReceived(Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.EnablePush, 0)]));
        trace.FrameReceived(Http2FrameFactory.CreateSettingsAcknowledgement());

        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTP/2] [0] <- FRAME[SETTINGS, len=18]",
                "[HTTP/2] [0] MAX_CONCURRENT_STREAMS: 100",
                "[HTTP/2] [0] ENABLE_PUSH: TRUE",
                "[HTTP/2] [0] <- FRAME[SETTINGS, len=6]",
                "[HTTP/2] [0] MAX_CONCURRENT_STREAMS: 100",
                "[HTTP/2] [0] ENABLE_PUSH: false",
                "[HTTP/2] [0] <- FRAME[SETTINGS, ack=1]",
            },
            events.Info);
    }

    [TestMethod]
    public void SessionCreatedAndStreamClosed_ReportCurlsLines()
    {
        RecordingTransferEvents events = new();
        Http2FrameTrace trace = new(events);

        trace.SessionCreated();
        trace.StreamClosed(3);

        CollectionAssert.AreEqual(new[] { "[HTTP/2] [0] created h2 session", "[HTTP/2] [3] CLOSED" }, events.Info);
    }

    [TestMethod]
    public void UpgradeStartedAndSessionCreatedByUpgrade_ReportCurlsH2cLines()
    {
        RecordingTransferEvents events = new();
        Http2FrameTrace trace = new(events);

        trace.UpgradeStarted();
        trace.SessionCreatedByUpgrade();

        string[] expected =
        [
            "[HTTP/2] added",
            "[HTTP/2] upgrading connection to HTTP/2",
            "[HTTP/2] created session via Upgrade",
            "[HTTP/2] [0] created h2 session (via h1 upgrade)",
        ];
        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    [DataRow("HTTP/2 200 \r\n", "[HTTP/2] [1] status: HTTP/2 200", DisplayName = "status line")]
    [DataRow("content-length: 2\r\n", "[HTTP/2] [1] header: content-length: 2", DisplayName = "header")]
    [DataRow("x-a: b c\n", "[HTTP/2] [1] header: x-a: b c", DisplayName = "header ended by a line feed")]
    public void ResponseLineReported_EchoesTheLineAsCurlDoes(string line, string expected)
    {
        RecordingTransferEvents events = new();

        new Http2FrameTrace(events).ResponseLineReported(1, Encoding.Latin1.GetBytes(line));

        CollectionAssert.AreEqual(new[] { expected }, events.Info);
    }

    [TestMethod]
    public void ResponseLineReported_EmptyLine_ReportsNothing()
    {
        RecordingTransferEvents events = new();

        new Http2FrameTrace(events).ResponseLineReported(1, "\r\n"u8);

        Assert.IsEmpty(events.Info);
    }
}
