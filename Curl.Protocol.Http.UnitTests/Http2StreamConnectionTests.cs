using System.Text;
using Curl.Http2;
using Curl.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what <see cref="Http2StreamConnection" /> takes from the connection under it; the
/// exchange itself is pinned through the handler in <c>HttpProtocolHandlerTests.Http2</c>.
/// </summary>
[TestClass]
public sealed class Http2StreamConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Members_TakeTheConnectionsSecurityAndEndPointAndDisposingLeavesItOpen()
    {
        ScriptedConnection connection = new([], 1);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);

        Diagnostics.Arrange("connection", $"secure {connection.IsSecure}, remote end point {connection.RemoteEndPoint?.ToString() ?? "none"}");

        await stream.DisposeAsync();

        Diagnostics.Act("stream secure, connection disposed", $"{stream.IsSecure}, {connection.IsDisposed}");
        Diagnostics.Assert("connection disposed", false, connection.IsDisposed);

        Assert.AreEqual(connection.IsSecure, stream.IsSecure);
        Assert.AreEqual(connection.RemoteEndPoint, stream.RemoteEndPoint);
        Assert.IsFalse(connection.IsDisposed);
    }

    [TestMethod]
    public async Task AbandonResponseAsync_Twice_ResetsTheStreamOnceAndDropsWhatThePeerStillSends()
    {
        // AF-0047: the abandoned stream counts as ended, so a second abandon sends nothing and
        // reads take none of the frames the peer still sends on it.
        HpackEncoder server = new();
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200")]), isEndStream: false, isEndHeaders: true)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "dropped"u8.ToArray(), isEndStream: true)),
        ];
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 1 :status 200, DATA 1 dropped end");
        ScriptedConnection connection = new(scripted, 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);

        await stream.AbandonResponseAsync(CancellationToken.None);
        await stream.AbandonResponseAsync(CancellationToken.None);
        int read = await stream.ReadAsync(new byte[64], CancellationToken.None);

        string written = Convert.ToHexString(connection.Written);
        int resets = written.Split(Stream1ClosedReset).Length - 1;
        Diagnostics.Act("STREAM_CLOSED resets written, bytes read, connection reads", $"{resets}, {read}, {connection.ReadCount}");
        Diagnostics.Assert("STREAM_CLOSED resets written", 1, resets);
        Assert.AreEqual(1, resets, "one RST_STREAM STREAM_CLOSED on stream 1, however often the response is abandoned");
        Assert.AreEqual(0, read, "the abandoned stream reads as ended");
        Assert.AreEqual(0, connection.ReadCount, "no frame is read for the abandoned stream");
    }

    /// <summary>The RST_STREAM with STREAM_CLOSED curl sends on stream 1 when it ignores that stream's body (BL-970 Notes).</summary>
    private const string Stream1ClosedReset = "00000403000000000100000005";
}
