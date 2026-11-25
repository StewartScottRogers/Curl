using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

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
    public async Task AbandonResponseAsync_RequestEnded_ReadsTheStreamToItsEndAndSendsNoReset()
    {
        HpackEncoder server = new();
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "401")]), isEndStream: false, isEndHeaders: true)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "no\n"u8.ToArray(), isEndStream: false)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateData(1, "more"u8.ToArray(), isEndStream: true)),
        ];
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 1 :status 401, DATA 1 no, DATA 1 more end");
        ScriptedConnection connection = new(scripted, 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);

        await stream.AbandonResponseAsync(CancellationToken.None);
        int read = await stream.ReadAsync(new byte[64], CancellationToken.None);

        int resets = Convert.ToHexString(connection.Written).Split(Stream1ClosedReset).Length - 1;
        Diagnostics.Act("STREAM_CLOSED resets written, bytes read", $"{resets}, {read}");
        Diagnostics.Assert("STREAM_CLOSED resets written", 0, resets);
        Assert.AreEqual(0, resets, "curl sends no RST_STREAM for a stream it reads to its end");
        Assert.AreEqual(0, read, "the ignored body is dropped");
    }

    [TestMethod]
    public async Task AbandonResponseAsync_RequestEndedAndPeerClosesBeforeTheBodyEnds_FailsWithExit18AndSendsNoReset()
    {
        HpackEncoder server = new();
        byte[] scripted =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "401")]), isEndStream: false, isEndHeaders: true)),
        ];
        Diagnostics.Arrange("server frames", "SETTINGS, HEADERS 1 :status 401, then closed");
        ScriptedConnection connection = new(scripted, 65536);
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 0, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: h\r\n\r\n"), CancellationToken.None);

        HttpTransferException failure = await Assert.ThrowsExactlyAsync<HttpTransferException>(
            async () => await stream.AbandonResponseAsync(CancellationToken.None));

        Diagnostics.Act("exit code", failure.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.PartialFile, failure.ExitCode);
        Assert.AreEqual(CurlExitCode.PartialFile, failure.ExitCode);
        Assert.DoesNotContain(Stream1ClosedReset, Convert.ToHexString(connection.Written));
    }

    [TestMethod]
    public async Task AbandonResponseAsync_TwiceWithTheRequestUnended_ResetsTheStreamOnceAndDropsWhatThePeerStillSends()
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
        Http2StreamConnection stream = (Http2StreamConnection)new Http2Session(connection).CreateStream("http", 5, ignoresBody: false);
        await stream.WriteAsync(Encoding.Latin1.GetBytes("POST / HTTP/1.1\r\nHost: h\r\nContent-Length: 5\r\n\r\n"), CancellationToken.None);

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
