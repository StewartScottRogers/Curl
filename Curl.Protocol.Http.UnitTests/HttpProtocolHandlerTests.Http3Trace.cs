using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the <c>[HTTP/3]</c> stream lines an HTTP/3 exchange writes when the handler traces HTTP/3
/// streams (<c>-v --trace-config http/3</c>, BL-1168, ADR-0375), placed among the <c>&lt;</c> and
/// <c>{</c> lines as curl 8.18.0's ngtcp2 build placed them against cloudflare-quic.com (BL-1168
/// Notes), and that it writes none of them otherwise.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_Http3GetTracingStreams_WritesEachHeadsEndEachDataAndTheCloseInCurlsPlaces()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("103", ("link", "</a>")), Http3Head("200", ("content-length", "5")), Http3Data("hel"), Http3Data("lo")), 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await new HttpProtocolHandler(QuicConnector(new FakeMultiplexedConnection(stream)), new SilentAuthenticator()) { TracesHttp3Streams = true }
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), new MemoryStream(), events: events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        int interim = events.Events.IndexOf("< HTTP/3 103 \r\n");
        CollectionAssert.AreEqual(
            new[]
            {
                "< HTTP/3 103 \r\n",
                "* [HTTP/3] [0] status: HTTP/3 103 \r\n",
                "* [HTTP/3] [0] header: link: </a>",
                "< link: </a>\r\n",
                "< \r\n",
                "* [HTTP/3] [0] end_headers, status=103",
                "< HTTP/3 200 \r\n",
                "* [HTTP/3] [0] status: HTTP/3 200 \r\n",
                "* [HTTP/3] [0] header: content-length: 5",
                "< content-length: 5\r\n",
                "< \r\n",
                "* [HTTP/3] [0] end_headers, status=200",
                "{ hel",
                "* [HTTP/3] [0] DATA len=3",
                "* [HTTP/3] [0] ACK 3/3 bytes of DATA",
                "{ lo",
                "* [HTTP/3] [0] DATA len=2",
                "* [HTTP/3] [0] ACK 2/2 bytes of DATA",
                "* [HTTP/3] [0] CLOSED",
                "* [HTTP/3] [0] quic close(app_error=256) -> 0",
            },
            events.Events.Skip(interim).Take(20).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3GetTracingStreams_WritesTheIdleTimeoutBeforeOpenedAndTheEndLinesBeforeLeftIntact()
    {
        // curl 8.18.0's ngtcp2 build against cloudflare-quic.com (BL-1208 Notes).
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "1")), Http3Data("x")), 65536);
        FakeMultiplexedConnection quic = new(stream) { PeerIdleTimeout = TimeSpan.FromMilliseconds(180000), BidirectionalStreamLimit = 100 };
        RecordingTransferEvents events = new();

        TransferResult result = await new HttpProtocolHandler(QuicConnector(quic), new SilentAuthenticator()) { TracesHttp3Streams = true }
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), new MemoryStream(), events: events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        int idle = events.Info.IndexOf("[HTTP/3] peer idle timeout is 180000ms, set keep-alive to 90000 ms.");
        Assert.IsGreaterThan(events.Info.IndexOf("using HTTP/3"), idle, string.Join('\n', events.Info));
        StringAssert.StartsWith(events.Info[idle + 1], "[HTTP/3] [0] OPENED stream for ");
        int done = events.Info.IndexOf("[HTTP/3] [0] easy handle is done");
        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTP/3] [0] quic close(app_error=256) -> 0",
                "[HTTP/3] [0] easy handle is done",
                "[HTTP/3] no active streams, unset keep-alive",
                "[HTTP/3] query conn[0]: MAX_CONCURRENT -> 99 (0 in use)",
            },
            events.Info.Skip(done - 1).Take(4).ToArray(),
            string.Join('\n', events.Info));
        StringAssert.StartsWith(events.Info[done + 3], "Connection #0 to host ");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3GetTracingStreamsWithoutPeerIdleTimeoutOrStreamLimit_WritesNeitherLine()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200"), Http3Data("x")), 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await new HttpProtocolHandler(QuicConnector(new FakeMultiplexedConnection(stream)), new SilentAuthenticator()) { TracesHttp3Streams = true }
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), new MemoryStream(), events: events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsFalse(events.Info.Any(line => line.Contains("peer idle timeout", StringComparison.Ordinal) || line.Contains("MAX_CONCURRENT", StringComparison.Ordinal)), string.Join('\n', events.Info));
        CollectionAssert.Contains(events.Info, "[HTTP/3] [0] easy handle is done");
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3HeadRequestTracingStreams_WritesTheHeadsEndAndTheClose()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200", ("content-length", "5"))), 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await new HttpProtocolHandler(QuicConnector(new FakeMultiplexedConnection(stream)), new SilentAuthenticator()) { TracesHttp3Streams = true }
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), new MemoryStream(), events: events, noBody: true));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "[HTTP/3] [0] status: HTTP/3 200 \r\n", "[HTTP/3] [0] header: content-length: 5", "[HTTP/3] [0] end_headers, status=200", "[HTTP/3] [0] CLOSED", "[HTTP/3] [0] quic close(app_error=256) -> 0", "[HTTP/3] [0] easy handle is done" },
            events.Info.Where(line => line.StartsWith("[HTTP/3] [0] ", StringComparison.Ordinal) && !line.StartsWith("[HTTP/3] [0] [", StringComparison.Ordinal) && !line.Contains("OPENED", StringComparison.Ordinal)).ToArray(),
            string.Join('\n', events.Events));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http3GetNotTracingStreams_WritesOnlyTheOpenedStreamLines()
    {
        FakeMultiplexedStream stream = new(0, Http3Response(Http3Head("200"), Http3Data("x")), 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await new HttpProtocolHandler(QuicConnector(new FakeMultiplexedConnection(stream)), new SilentAuthenticator()) { TracesHttp2Frames = true }
            .ExecuteAsync(Http3Context("https://example.com/", new MemoryStream(), new MemoryStream(), events: events));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] lines = [.. events.Info.Where(line => line.StartsWith("[HTTP/3]", StringComparison.Ordinal))];
        Assert.IsTrue(lines.All(line => line.StartsWith("[HTTP/3] [0] OPENED", StringComparison.Ordinal) || line.StartsWith("[HTTP/3] [0] [", StringComparison.Ordinal)), string.Join('\n', lines));
        Assert.IsFalse(new HttpProtocolHandler(QueueConnector.For(), new SilentAuthenticator()).TracesHttp3Streams);
    }
}
