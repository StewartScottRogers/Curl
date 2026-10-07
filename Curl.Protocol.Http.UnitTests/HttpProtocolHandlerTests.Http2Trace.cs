using System.Text;
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

        Diagnostics.Arrange("url, traces http2 frames", $"{LogUrl}, true");
        Diagnostics.Arrange("response frames", $"HEADERS 200 length 5 ({headerBlock.Length} byte block), DATA hello (end stream)");

        List<string> lines = await Http2TraceLinesAsync(new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator()) { TracesHttp2Frames = true });

        WriteEvents("http2 lines", lines);
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
            "[HTTP/2] [1] status: HTTP/2 200",
            "[HTTP/2] [1] header: content-length: 5",
            "[HTTP/2] [1] <- FRAME[DATA, len=5, eos=1, padlen=0]",
            "[HTTP/2] [1] CLOSED",
        ];
        WriteExpectedLines("frame lines", expected, frameLines);
        CollectionAssert.AreEqual(expected, frameLines, string.Join('\n', frameLines));
        Diagnostics.Assert("sent headers line ends with", true, frameLines[sentHeaders].EndsWith(", hend=1, eos=1]", StringComparison.Ordinal));
        StringAssert.EndsWith(frameLines[sentHeaders], ", hend=1, eos=1]");
        Diagnostics.Assert("method line precedes sent headers", true, lines.IndexOf("[HTTP/2] [1] [:method: GET]") < lines.IndexOf(frameLines[sentHeaders]));
        Assert.IsTrue(lines.IndexOf("[HTTP/2] [1] [:method: GET]") < lines.IndexOf(frameLines[sentHeaders]));
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2GetNotTracingFrames_WritesOnlyTheOpenedStreamLines()
    {
        Diagnostics.Arrange("url, traces http2 frames", $"{LogUrl}, false");

        List<string> lines = await Http2TraceLinesAsync(Handler(QueueConnector.For(new ScriptedConnection(Http2HelloResponse(), 65536))));

        WriteEvents("http2 lines", lines);
        Diagnostics.Assert(
            "every line is an opened or header line",
            true,
            lines.All(line => line.StartsWith("[HTTP/2] [1] OPENED", StringComparison.Ordinal) || line.StartsWith("[HTTP/2] [1] [", StringComparison.Ordinal)));
        Assert.IsTrue(lines.All(line => line.StartsWith("[HTTP/2] [1] OPENED", StringComparison.Ordinal) || line.StartsWith("[HTTP/2] [1] [", StringComparison.Ordinal)), string.Join('\n', lines));
        Diagnostics.Assert("default traces http2 frames", false, new HttpProtocolHandler(QueueConnector.For(), new SilentAuthenticator()).TracesHttp2Frames);
        Assert.IsFalse(new HttpProtocolHandler(QueueConnector.For(), new SilentAuthenticator()).TracesHttp2Frames);
    }

    [TestMethod]
    public async Task ExecuteAsync_H2cUpgradeTracingFrames_WritesTheUpgradesLinesAndEchoesTheHead()
    {
        // curl 8.18.0 (OpenSSL, nghttp2 1.68.0) -s -o /dev/null -v --http2 --trace-config http/2 against
        // a 101 followed in the same read by an empty SETTINGS, its acknowledgement, :status 200 with
        // content-length: 2 and "hi" on stream 1 (BL-1205 Notes).
        byte[] response =
        [
            .. Encoding.Latin1.GetBytes(SwitchingProtocolsHead),
            .. Convert.FromHexString("000000040000000000" + "000000040100000000" + "000004010400000001885C0132" + "00000200010000000168 69".Replace(" ", string.Empty, StringComparison.Ordinal)),
        ];
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, traces http2 frames", "http://127.0.0.1:48717/, true");
        Diagnostics.Arrange("response", "101 then SETTINGS, SETTINGS ack, HEADERS 200 length 2, DATA hi (end stream)");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new SilentAuthenticator()) { TracesHttp2Frames = true }
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48717/", new MemoryStream(), null, events));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] lines = [.. events.Events.SkipWhile(line => !line.StartsWith("* Received 101", StringComparison.Ordinal)).Where(line => !line.StartsWith("* Connection", StringComparison.Ordinal))];
        string[] expected =
        [
            "* " + HttpConnectionInfoLines.SwitchingToHttp2,
            "* [HTTP/2] added",
            "* [HTTP/2] upgrading connection to HTTP/2",
            "* Copied HTTP/2 data in stream buffer to connection buffer after upgrade: len=42",
            "* [HTTP/2] created session via Upgrade",
            "* [HTTP/2] [0] created h2 session (via h1 upgrade)",
            "* [HTTP/2] [0] -> FRAME[SETTINGS, len=18]",
            "* [HTTP/2] [0] -> FRAME[WINDOW_UPDATE, incr=1048510465]",
            "* [HTTP/2] [0] <- FRAME[SETTINGS, len=0]",
            "* [HTTP/2] [0] MAX_CONCURRENT_STREAMS: -1",
            "* [HTTP/2] [0] ENABLE_PUSH: TRUE",
            "* [HTTP/2] [0] -> FRAME[SETTINGS, ack=1]",
            "* [HTTP/2] [0] <- FRAME[SETTINGS, ack=1]",
            "* [HTTP/2] [1] <- FRAME[HEADERS, len=4, hend=1, eos=0]",
            "< HTTP/2 200 \r\n",
            "* [HTTP/2] [1] status: HTTP/2 200",
            "< content-length: 2\r\n",
            "* [HTTP/2] [1] header: content-length: 2",
            "< \r\n",
            "* [HTTP/2] [1] <- FRAME[DATA, len=2, eos=1, padlen=0]",
            "* [HTTP/2] [1] CLOSED",
            "{ hi",
        ];
        WriteEvents("lines after 101", lines);
        WriteExpectedLines("lines after 101", expected, lines);
        CollectionAssert.AreEqual(expected, lines, string.Join('\n', lines));
    }

    [TestMethod]
    public async Task ExecuteAsync_H2cUpgrade401TracingFrames_WritesThePostUpgradeSettingsBeforeStream3()
    {
        HpackEncoder server = new();
        byte[] response =
        [
            .. Encoding.Latin1.GetBytes(SwitchingProtocolsHead),
            .. RecordedUpgradeFrames(
                Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "401"), new("www-authenticate", "Basic realm=\"x\"")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(1, "no\n"u8.ToArray(), isEndStream: true),
                Http2FrameFactory.CreateHeaders(3, server.Encode([new(":status", "200"), new("content-length", "2")]), isEndStream: false, isEndHeaders: true),
                Http2FrameFactory.CreateData(3, "ok"u8.ToArray(), isEndStream: true)),
        ];
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("url, traces http2 frames, authorization on retry", "http://127.0.0.1:48973/a, true, Basic YTpi");
        Diagnostics.Arrange("response", "101 then HEADERS 401 www-authenticate, DATA no, HEADERS 200 length 2 on 3, DATA ok");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(response, 65536)), new ScriptedAuthenticator(null, "Basic YTpi")) { TracesHttp2Frames = true }
            .ExecuteAsync(UpgradeContext("http://127.0.0.1:48973/a", new MemoryStream(), null, events));

        WriteResult(result);
        WriteEvents("info", events.Info);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        int settings = events.Info.IndexOf("[HTTP/2] [0] -> FRAME[SETTINGS, len=6]");
        int status401 = events.Info.IndexOf("[HTTP/2] [1] status: HTTP/2 401");
        int stream3Headers = events.Info.FindIndex(line => line.StartsWith("[HTTP/2] [3] -> FRAME[HEADERS", StringComparison.Ordinal));
        Diagnostics.Act("settings index, 401 status index, stream 3 headers index", $"{settings}, {status401}, {stream3Headers}");
        Diagnostics.Assert("settings index is above", status401, settings);
        Assert.IsGreaterThan(events.Info.IndexOf("[HTTP/2] [1] status: HTTP/2 401"), settings, string.Join('\n', events.Info));
        Diagnostics.Assert("settings index is below", stream3Headers, settings);
        Assert.IsLessThan(events.Info.FindIndex(line => line.StartsWith("[HTTP/2] [3] -> FRAME[HEADERS", StringComparison.Ordinal)), settings);
        Diagnostics.Assert("contains 401 header", true, events.Info.Contains("[HTTP/2] [1] header: www-authenticate: Basic realm=\"x\""));
        CollectionAssert.Contains(events.Info, "[HTTP/2] [1] header: www-authenticate: Basic realm=\"x\"");
        Diagnostics.Assert("contains 200 status", true, events.Info.Contains("[HTTP/2] [3] status: HTTP/2 200"));
        CollectionAssert.Contains(events.Info, "[HTTP/2] [3] status: HTTP/2 200");
        Diagnostics.Assert("contains content-length header", true, events.Info.Contains("[HTTP/2] [3] header: content-length: 2"));
        CollectionAssert.Contains(events.Info, "[HTTP/2] [3] header: content-length: 2");
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
