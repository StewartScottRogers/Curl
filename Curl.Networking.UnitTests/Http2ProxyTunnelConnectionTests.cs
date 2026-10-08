using System.Text;

using Curl.Http2;
using Curl.Networking.Fakes;
using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class Http2ProxyTunnelConnectionTests
{
    private static readonly IReadOnlyList<HeaderField> ConnectFields =
        Http2ProxyTunnelConnection.ConnectHeaderFields("example.test:443", null, null);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ConnectHeaderFields_WithAProxyAuthorizationAndNoUserAgent_SendsTheAuthorizationAfterThePseudoHeaders()
    {
        Diagnostics.Arrange("authority", "example.test:443");
        Diagnostics.Arrange("proxy authorization", "Basic dTpw");
        Diagnostics.Arrange("user agent", "none");

        var fields = Http2ProxyTunnelConnection.ConnectHeaderFields("example.test:443", "Basic dTpw", null);

        var actual = string.Join(" | ", fields.Select(field => $"{field.Name}={field.Value}"));
        Diagnostics.Act("fields", actual);
        Diagnostics.Assert("fields", ":method=CONNECT | :authority=example.test:443 | proxy-authorization=Basic dTpw", actual);
        CollectionAssert.AreEqual(
            new[] { ":method=CONNECT", ":authority=example.test:443", "proxy-authorization=Basic dTpw" },
            fields.Select(field => $"{field.Name}={field.Value}").ToArray());
    }

    [TestMethod]
    public async Task OpenAsync_SkipsInterimStatusesDataAndBlocksWithoutAStatus_UntilTheFinalOne()
    {
        var proxy = new ScriptedConnection(TcpConnectorTests.ProxyFrames(
            Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode([new HeaderField("x-note", "no status")]), isEndStream: false, isEndHeaders: true),
            TcpConnectorTests.StatusHeaders(1, "1x0"),
            TcpConnectorTests.StatusHeaders(1, "100"),
            Http2FrameFactory.CreateData(1, "early"u8.ToArray(), isEndStream: false),
            TcpConnectorTests.StatusHeaders(1, "200")));
        Diagnostics.Arrange("proxy frames", "HEADERS without :status, :status 1x0, :status 100, DATA \"early\", :status 200");

        int statusCode;
        Http2ProxyTunnelConnection? tunnel;
        using (Diagnostics.Phase("open tunnel"))
        {
            (statusCode, tunnel) = await Http2ProxyTunnelConnection.OpenAsync(proxy, ConnectFields, CancellationToken.None);
        }

        Diagnostics.Act("status code", statusCode);
        Diagnostics.Act("tunnel opened", tunnel is not null);
        Diagnostics.Act("flushes", proxy.FlushCount);
        Diagnostics.Assert("status code", 200, statusCode);
        Diagnostics.Assert("tunnel opened", true, tunnel is not null);
        Diagnostics.Assert("flushes", 1, proxy.FlushCount);
        Assert.AreEqual(200, statusCode);
        Assert.IsNotNull(tunnel);
        Assert.AreEqual(1, proxy.FlushCount);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheProxyClosesBeforeAStatus_ReturnsStatusZeroAndNoTunnel()
    {
        var proxy = new ScriptedConnection(TcpConnectorTests.ProxyFrames());
        Diagnostics.Arrange("proxy frames", "the preface only, then the connection closes");

        int statusCode;
        Http2ProxyTunnelConnection? tunnel;
        using (Diagnostics.Phase("open tunnel"))
        {
            (statusCode, tunnel) = await Http2ProxyTunnelConnection.OpenAsync(proxy, ConnectFields, CancellationToken.None);
        }

        Diagnostics.Act("status code", statusCode);
        Diagnostics.Act("tunnel opened", tunnel is not null);
        Diagnostics.Assert("status code", 0, statusCode);
        Diagnostics.Assert("tunnel opened", false, tunnel is not null);
        Assert.AreEqual(0, statusCode);
        Assert.IsNull(tunnel);
    }

    [TestMethod]
    public async Task ReadAsync_ReturnsEachDataFrameThenZeroAtTheStreamsEnd()
    {
        var proxy = new ScriptedConnection(TcpConnectorTests.ProxyFrames(
            TcpConnectorTests.StatusHeaders(1, "200"),
            Http2FrameFactory.CreateData(1, "abc"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateData(1, "de"u8.ToArray(), isEndStream: true)));
        var tunnel = await OpenTunnelAsync(proxy);
        var buffer = new byte[2];
        Diagnostics.Arrange("proxy frames", ":status 200, DATA \"abc\", DATA \"de\" with END_STREAM");
        Diagnostics.Arrange("read buffer", "2 bytes");

        var reads = new List<string>();
        int read;
        while ((read = await tunnel.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reads.Add(Encoding.Latin1.GetString(buffer, 0, read));
        }

        var readAfterTheEnd = await tunnel.ReadAsync(buffer, CancellationToken.None);
        Diagnostics.Act("reads", string.Join(" | ", reads));
        Diagnostics.Act("read after the end", readAfterTheEnd);
        Diagnostics.Assert("reads", "ab | c | de", string.Join(" | ", reads));
        Diagnostics.Assert("read after the end", 0, readAfterTheEnd);
        CollectionAssert.AreEqual(new[] { "ab", "c", "de" }, reads);
        Assert.AreEqual(0, readAfterTheEnd);
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheProxyClosesTheConnection_ReturnsZero()
    {
        var tunnel = await OpenTunnelAsync(new ScriptedConnection(TcpConnectorTests.ProxyFrames(TcpConnectorTests.StatusHeaders(1, "200"))));
        Diagnostics.Arrange("proxy frames", ":status 200, then the connection closes");

        var read = await tunnel.ReadAsync(new byte[4], CancellationToken.None);

        Diagnostics.Act("read", read);
        Diagnostics.Assert("read", 0, read);
        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheWindowsAreSpent_WaitsForWindowUpdatesAndKeepsDataThatArrivesMeanwhile()
    {
        var proxy = new ScriptedConnection(TcpConnectorTests.ProxyFrames(
            TcpConnectorTests.StatusHeaders(1, "200"),
            Http2FrameFactory.CreateData(1, "x"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateData(1, "y"u8.ToArray(), isEndStream: false),
            Http2FrameFactory.CreateWindowUpdate(0, 100),
            Http2FrameFactory.CreateWindowUpdate(1, 100)));
        var tunnel = await OpenTunnelAsync(proxy);
        var payload = new byte[Http2Settings.DefaultInitialWindowSize + 50];
        Diagnostics.Arrange("proxy frames", ":status 200, DATA \"x\", DATA \"y\", WINDOW_UPDATE 0 +100, WINDOW_UPDATE 1 +100");
        Diagnostics.Arrange("payload length", payload.Length);

        using (Diagnostics.Phase("write past the window"))
        {
            await tunnel.WriteAsync(payload, CancellationToken.None);
        }

        var sent = (await TcpConnectorTests.ReadClientFramesAsync(proxy.Written))
            .Where(frame => frame.Type == Http2FrameType.Data)
            .Sum(frame => frame.Payload.Length);
        Diagnostics.Act("DATA bytes sent", sent);
        Diagnostics.Assert("DATA bytes sent", payload.Length, sent);
        Assert.AreEqual(payload.Length, sent);
        var buffer = new byte[4];
        var read = await tunnel.ReadAsync(buffer, CancellationToken.None);
        Diagnostics.Act("kept data", Encoding.Latin1.GetString(buffer, 0, read));
        Diagnostics.Assert("kept data", "xy", Encoding.Latin1.GetString(buffer, 0, read));
        Assert.AreEqual("xy", Encoding.Latin1.GetString(buffer, 0, read));
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheProxyClosesWhileTheWindowsAreSpent_ThrowsIOException()
    {
        var tunnel = await OpenTunnelAsync(new ScriptedConnection(TcpConnectorTests.ProxyFrames(TcpConnectorTests.StatusHeaders(1, "200"))));
        Diagnostics.Arrange("proxy frames", ":status 200, then the connection closes");
        Diagnostics.Arrange("payload length", Http2Settings.DefaultInitialWindowSize + 1);

        var exception = await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await tunnel.WriteAsync(new byte[Http2Settings.DefaultInitialWindowSize + 1], CancellationToken.None));

        Diagnostics.Act("exception", $"{exception.GetType().Name}: {exception.Message}");
        Diagnostics.Assert("exception type", nameof(IOException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task TheTunnel_TakesItsSecurityEndPointsFlushAndDisposalFromTheProxysConnection()
    {
        var proxy = new ScriptedConnection(TcpConnectorTests.ProxyFrames(TcpConnectorTests.StatusHeaders(1, "200")));
        var tunnel = await OpenTunnelAsync(proxy);

        await tunnel.FlushAsync(CancellationToken.None);
        await tunnel.DisposeAsync();

        Diagnostics.Arrange("proxy connection", $"secure {proxy.IsSecure}, remote {proxy.RemoteEndPoint?.ToString() ?? "none"}");
        Diagnostics.Act("tunnel", $"secure {tunnel.IsSecure}, remote {tunnel.RemoteEndPoint?.ToString() ?? "none"}, local {tunnel.LocalEndPoint?.ToString() ?? "none"}");
        Diagnostics.Act("proxy flushes", proxy.FlushCount);
        Diagnostics.Act("proxy disposed", proxy.IsDisposed);
        Diagnostics.Assert("secure", proxy.IsSecure, tunnel.IsSecure);
        Diagnostics.Assert("remote end", proxy.RemoteEndPoint?.ToString() ?? "none", tunnel.RemoteEndPoint?.ToString() ?? "none");
        Diagnostics.Assert("local end", "none", tunnel.LocalEndPoint?.ToString() ?? "none");
        Diagnostics.Assert("proxy flushes", 2, proxy.FlushCount);
        Diagnostics.Assert("proxy disposed", true, proxy.IsDisposed);
        Assert.AreEqual(proxy.IsSecure, tunnel.IsSecure);
        Assert.AreEqual(proxy.RemoteEndPoint, tunnel.RemoteEndPoint);
        Assert.IsNull(tunnel.LocalEndPoint);
        Assert.AreEqual(2, proxy.FlushCount);
        Assert.IsTrue(proxy.IsDisposed);
    }

    private static async Task<Http2ProxyTunnelConnection> OpenTunnelAsync(ScriptedConnection proxy) =>
        (await Http2ProxyTunnelConnection.OpenAsync(proxy, ConnectFields, CancellationToken.None)).Tunnel!;
}
