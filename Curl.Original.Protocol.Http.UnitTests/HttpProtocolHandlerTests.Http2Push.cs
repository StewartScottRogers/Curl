using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins HTTP/2 server push through the handler (BL-2516): a handler given an
/// <see cref="IHttp2PushSink" /> advertises ENABLE_PUSH 1 and offers each PUSH_PROMISE on its
/// transfer's stream to the sink; one without keeps the measured preface with ENABLE_PUSH 0.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_Http2WithAPushSink_AdvertisesPushAndHandsThePushToTheSink()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(
            Http2FrameFactory.CreatePushPromise(1, 2, server.Encode([new(":method", "GET"), new(":scheme", "http"), new(":authority", "example.com"), new(":path", "/a.css")]), isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(2, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(2, "css"u8.ToArray(), isEndStream: true),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("content-length", "4")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "page"u8.ToArray(), isEndStream: true));
        ScriptedConnection connection = new(response, 65536);
        AcceptingPushSink sink = new();
        MemoryStream output = new();
        Diagnostics.Arrange("response frames", "PUSH_PROMISE 1 promising 2, HEADERS 2, DATA 2 css end, HEADERS 1 length 4, DATA 1 page end");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator(), http2PushSink: sink)
            .ExecuteAsync(Http2Context("http://example.com/", output));
        byte[] buffer = new byte[256];
        int pushedLength = await sink.Response!.Content.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
        string pushed = Encoding.Latin1.GetString(buffer, 0, pushedLength);

        WriteResult(result);
        Diagnostics.Act("pushed headers", string.Join(" | ", sink.Headers!));
        Diagnostics.Act("pushed response", pushed.Replace("\r\n", "\\r\\n"));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("page", Latin1(output.ToArray()));
        Assert.AreEqual(Http2PushEnabledPreface, Convert.ToHexString(connection.Written[..(Http2PushEnabledPreface.Length / 2)]).ToLowerInvariant());
        CollectionAssert.AreEqual(new[] { ":method:GET", ":scheme:http", ":authority:example.com", ":path:/a.css" }, sink.Headers!.ToArray());
        Assert.AreEqual("HTTP/2 200 \r\n\r\n", pushed, "the first read gives the pushed head");
        Assert.AreEqual(2, sink.Response.StreamId);
    }

    [TestMethod]
    public async Task ExecuteAsync_Http2WithoutAPushSink_SendsTheMeasuredPrefaceWithEnablePushZero()
    {
        HpackEncoder server = new();
        byte[] response = Http2Response(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        ScriptedConnection connection = new(response, 65536);
        Diagnostics.Arrange("response frames", "HEADERS 1 204 end");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(Http2Context("http://example.com/", new MemoryStream()));

        WriteResult(result);
        string preface = Convert.ToHexString(connection.Written[..(Http2Preface.Length / 2)]).ToLowerInvariant();
        Diagnostics.Assert("preface", Http2Preface, preface);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(Http2Preface, preface);
    }

    /// <summary>The measured preface (<see cref="Http2Preface" />) with ENABLE_PUSH 1, as curl sends it with a push callback.</summary>
    private const string Http2PushEnabledPreface =
        "505249202a20485454502f322e300d0a0d0a534d0d0a0d0a"
        + "000012040000000000" + "000300000064" + "000400010000" + "000200000001"
        + "000004080000000000" + "3e7f0001";

    /// <summary>Takes every push, keeping the last one offered.</summary>
    private sealed class AcceptingPushSink : IHttp2PushSink
    {
        public IReadOnlyList<string>? Headers { get; private set; }

        public IHttpPushedResponse? Response { get; private set; }

        public bool TryAcceptPush(IReadOnlyList<string> headers, IHttpPushedResponse response)
        {
            Headers = headers;
            Response = response;
            return true;
        }
    }
}
