using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins the <c>[HTTP/2]</c> lines an HTTP/2 exchange writes when the handler traces HTTP/2 frames
/// (<c>-v --trace-config http/2</c>, BL-1167, ADR-0373), in the order curl 8.18.0's nghttp2 build
/// wrote them against example.com (BL-1167 Notes), and that it writes none of them otherwise.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_Http2GetTracingFrames_WritesTheSessionsFramesInCurlsOrder()
    {
        HpackEncoder server = new();
        byte[] headerBlock = server.Encode([new(":status", "200"), new("content-length", "5")]);
        byte[] response = Http2Response(
            Http2FrameFactory.CreateHeaders(1, headerBlock, isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "hello"u8.ToArray(), isEndStream: true));

        List<string> lines = await Http2TraceLinesAsync(new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator()) { TracesHttp2Frames = true });

        string[] frameLines = [.. lines.Where(line => !line.Contains("] [:", StringComparison.Ordinal) && !line.Contains("[user-agent:", StringComparison.Ordinal) && !line.Contains("[accept:", StringComparison.Ordinal))];
        int sentHeaders = Array.FindIndex(frameLines, line => line.StartsWith("[HTTP/2] [1] -> FRAME[HEADERS, len=", StringComparison.Ordinal));
        string[] expected =
        [
            "[HTTP/2] [0] created h2 session",
            "[HTTP/2] [0] -> FRAME[SETTINGS, len=18]",
            "[HTTP/2] [0] -> FRAME[WINDOW_UPDATE, incr=1048510465]",
            "[HTTP/2] [1] OPENED stream for http://127.0.0.1:18922/a?token=q",
            frameLines[sentHeaders],
            "[HTTP/2] [1] -> FRAME[WINDOW_UPDATE, incr=10420225]",
            "[HTTP/2] [1] -> FRAME[WINDOW_UPDATE, incr=10420225]",
            "[HTTP/2] [0] <- FRAME[SETTINGS, len=0]",
            "[HTTP/2] [0] MAX_CONCURRENT_STREAMS: -1",
            "[HTTP/2] [0] ENABLE_PUSH: TRUE",
            "[HTTP/2] [0] -> FRAME[SETTINGS, ack=1]",
            "[HTTP/2] [0] <- FRAME[SETTINGS, ack=1]",
            $"[HTTP/2] [1] <- FRAME[HEADERS, len={headerBlock.Length}, hend=1, eos=0]",
            "[HTTP/2] [1] <- FRAME[DATA, len=5, eos=1, padlen=0]",
            "[HTTP/2] [1] CLOSED",
        ];
        CollectionAssert.AreEqual(expected, frameLines, string.Join('\n', frameLines));
        StringAssert.EndsWith(frameLines[sentHeaders], ", hend=1, eos=1]");
        Assert.IsTrue(lines.IndexOf("[HTTP/2] [1] [:method: GET]") < lines.IndexOf(frameLines[sentHeaders]));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GetNotTracingFrames_WritesOnlyTheOpenedStreamLines()
    {
        List<string> lines = await Http2TraceLinesAsync(Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536))));

        Assert.IsTrue(lines.All(line => line.StartsWith("[HTTP/2] [1] OPENED", StringComparison.Ordinal) || line.StartsWith("[HTTP/2] [1] [", StringComparison.Ordinal)), string.Join('\n', lines));
        Assert.IsFalse(new HttpProtocolHandler(QueueConnector.For(), new SilentAuthenticator()).TracesHttp2Frames);
    }

    private static async Task<List<string>> Http2TraceLinesAsync(HttpProtocolHandler handler)
    {
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(LogUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge },
            Events = events,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };

        TransferResult result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return [.. events.Info.Where(line => line.StartsWith("[HTTP/2]", StringComparison.Ordinal))];
    }
}
