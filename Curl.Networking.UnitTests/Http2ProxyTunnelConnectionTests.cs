using System.Text;

using Curl.Http2;
using Curl.Networking.Fakes;

namespace Curl.Networking;

[TestClass]
public sealed class Http2ProxyTunnelConnectionTests
{
    private static readonly IReadOnlyList<HeaderField> ConnectFields =
        Http2ProxyTunnelConnection.ConnectHeaderFields("example.test:443", null, null);

    [TestMethod]
    public void ConnectHeaderFields_WithAProxyAuthorizationAndNoUserAgent_SendsTheAuthorizationAfterThePseudoHeaders()
    {
        var fields = Http2ProxyTunnelConnection.ConnectHeaderFields("example.test:443", "Basic dTpw", null);

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

        var (statusCode, tunnel) = await Http2ProxyTunnelConnection.OpenAsync(proxy, ConnectFields, CancellationToken.None);

        Assert.AreEqual(200, statusCode);
        Assert.IsNotNull(tunnel);
        Assert.AreEqual(1, proxy.FlushCount);
    }

    [TestMethod]
    public async Task OpenAsync_WhenTheProxyClosesBeforeAStatus_ReturnsStatusZeroAndNoTunnel()
    {
        var proxy = new ScriptedConnection(TcpConnectorTests.ProxyFrames());

        var (statusCode, tunnel) = await Http2ProxyTunnelConnection.OpenAsync(proxy, ConnectFields, CancellationToken.None);

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

        var reads = new List<string>();
        int read;
        while ((read = await tunnel.ReadAsync(buffer, CancellationToken.None)) > 0)
        {
            reads.Add(Encoding.Latin1.GetString(buffer, 0, read));
        }

        CollectionAssert.AreEqual(new[] { "ab", "c", "de" }, reads);
        Assert.AreEqual(0, await tunnel.ReadAsync(buffer, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_WhenTheProxyClosesTheConnection_ReturnsZero()
    {
        var tunnel = await OpenTunnelAsync(new ScriptedConnection(TcpConnectorTests.ProxyFrames(TcpConnectorTests.StatusHeaders(1, "200"))));

        Assert.AreEqual(0, await tunnel.ReadAsync(new byte[4], CancellationToken.None));
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

        await tunnel.WriteAsync(payload, CancellationToken.None);

        var sent = (await TcpConnectorTests.ReadClientFramesAsync(proxy.Written))
            .Where(frame => frame.Type == Http2FrameType.Data)
            .Sum(frame => frame.Payload.Length);
        Assert.AreEqual(payload.Length, sent);
        var buffer = new byte[4];
        var read = await tunnel.ReadAsync(buffer, CancellationToken.None);
        Assert.AreEqual("xy", Encoding.Latin1.GetString(buffer, 0, read));
    }

    [TestMethod]
    public async Task WriteAsync_WhenTheProxyClosesWhileTheWindowsAreSpent_ThrowsIOException()
    {
        var tunnel = await OpenTunnelAsync(new ScriptedConnection(TcpConnectorTests.ProxyFrames(TcpConnectorTests.StatusHeaders(1, "200"))));

        await Assert.ThrowsExactlyAsync<IOException>(async () =>
            await tunnel.WriteAsync(new byte[Http2Settings.DefaultInitialWindowSize + 1], CancellationToken.None));
    }

    [TestMethod]
    public async Task TheTunnel_TakesItsSecurityEndPointsFlushAndDisposalFromTheProxysConnection()
    {
        var proxy = new ScriptedConnection(TcpConnectorTests.ProxyFrames(TcpConnectorTests.StatusHeaders(1, "200")));
        var tunnel = await OpenTunnelAsync(proxy);

        await tunnel.FlushAsync(CancellationToken.None);
        await tunnel.DisposeAsync();

        Assert.AreEqual(proxy.IsSecure, tunnel.IsSecure);
        Assert.AreEqual(proxy.RemoteEndPoint, tunnel.RemoteEndPoint);
        Assert.IsNull(tunnel.LocalEndPoint);
        Assert.AreEqual(2, proxy.FlushCount);
        Assert.IsTrue(proxy.IsDisposed);
    }

    private static async Task<Http2ProxyTunnelConnection> OpenTunnelAsync(ScriptedConnection proxy) =>
        (await Http2ProxyTunnelConnection.OpenAsync(proxy, ConnectFields, CancellationToken.None)).Tunnel!;
}
