using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through a <c>--preproxy</c> SOCKS proxy to an HTTP proxy
/// with fakes. Every byte and message is curl 8.21.0's, measured against a scripted loopback
/// SOCKS5 server with <c>--preproxy socks5://127.0.0.1:41080 -x http://10.0.0.1:3128</c>; the
/// commands and bytes are in BL-614's Notes.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly ProxyEndpoint Socks5PreProxy = new(ProxyKind.Socks5, "socks.example", 1080, null);

    private static readonly IPAddress PreProxyAddress = IPAddress.Parse("192.0.2.20");

    private static readonly ConnectTarget ForwardProxyTarget = new("10.0.0.1", 3128, UseTls: false) { IsForwardProxy = true };

    // 05 02 00 01 offers no authentication and GSSAPI; 05 01 00 01 0a 00 00 01 0c 38 asks for 10.0.0.1:3128.
    private static readonly byte[] Socks5RequestToTheHttpProxy =
        [0x05, 0x02, 0x00, 0x01, 0x05, 0x01, 0x00, 0x01, 0x0A, 0x00, 0x00, 0x01, 0x0C, 0x38];

    [TestMethod]
    public async Task ConnectAsync_ToAForwardProxyThroughAPreProxy_OpensTheSocksTunnelToTheHttpProxy()
    {
        var socksConnection = new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded, .. BytesAfterTheHandshake]);
        var (connector, dialer) = CreatePreProxyConnector(socksConnection);

        var result = await connector.ConnectAsync(ForwardProxyTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(socksConnection, result.Connection);
        CollectionAssert.AreEqual(Socks5RequestToTheHttpProxy, socksConnection.Written);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(PreProxyAddress, 1080) }, dialer.DialedEndPoints);
        Assert.AreEqual(BytesAfterTheHandshake.Length, socksConnection.UnreadCount);
    }

    [TestMethod]
    [DataRow(ProxyKind.Http)]
    [DataRow(ProxyKind.Http10)]
    public async Task ConnectAsync_ThroughAnHttpProxyBehindAPreProxy_SendsTheConnectInsideTheSocksTunnel(ProxyKind kind)
    {
        var socksConnection = new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded, .. Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\n\r\n")]);
        var (connector, dialer) = CreatePreProxyConnector(socksConnection);

        var result = await connector.ConnectAsync(
            new ConnectTarget("h", 443, UseTls: false) { Proxy = new ProxyEndpoint(kind, "10.0.0.1", 3128, null) },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(PreProxyAddress, 1080) }, dialer.DialedEndPoints);
        var written = socksConnection.Written.ToArray();
        CollectionAssert.AreEqual(Socks5RequestToTheHttpProxy, written[..Socks5RequestToTheHttpProxy.Length]);
        StringAssert.StartsWith(Encoding.Latin1.GetString(written[Socks5RequestToTheHttpProxy.Length..]), "CONNECT h:443 HTTP/1.");
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxyBehindAPreProxy_RunsTheProxyHandshakeInsideTheSocksTunnel()
    {
        var socksConnection = new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded]);
        var handshakeFailure = ConnectResult.Failed(CurlExitCode.SslConnectError, "proxy handshake failed");
        var tlsProvider = new FakeTlsProvider { FailureToReturn = handshakeFailure };
        var (connector, _) = CreatePreProxyConnector(socksConnection, tlsProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("h", 443, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Https, "10.0.0.1", 3128, null) },
            CancellationToken.None);

        Assert.AreEqual("proxy handshake failed", result.ErrorMessage);
        CollectionAssert.AreEqual(Socks5RequestToTheHttpProxy, socksConnection.Written);
        Assert.AreSame(socksConnection, tlsProvider.ReceivedPlaintext);
        Assert.AreEqual("10.0.0.1", tlsProvider.ReceivedTargetHost);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenThePreProxyRefusesTheHttpProxy_FailsWithProxyAndDisposesTheConnection()
    {
        // curl --preproxy socks5://127.0.0.1:41080 -x http://10.0.0.1:3128 http://h/ against a reply of
        // 05 05 ... -> curl: (97) cannot complete SOCKS5 connection to 10.0.0.1. (5)
        var socksConnection = new ScriptedConnection([.. Socks5NoAuthentication, 0x05, 0x05, 0x00, 0x01, 0, 0, 0, 0, 0, 0]);
        var (connector, _) = CreatePreProxyConnector(socksConnection);

        var result = await connector.ConnectAsync(PlainTarget with { Proxy = new ProxyEndpoint(ProxyKind.Http, "10.0.0.1", 3128, null) }, CancellationToken.None);

        AssertProxyFailure(result, socksConnection, "cannot complete SOCKS5 connection to 10.0.0.1. (5)");
    }

    [TestMethod]
    public async Task ConnectAsync_ToAForwardProxyWhenThePreProxyRefusesIt_FailsWithProxy()
    {
        var socksConnection = new ScriptedConnection([.. Socks5NoAuthentication, 0x05, 0x05, 0x00, 0x01, 0, 0, 0, 0, 0, 0]);
        var (connector, _) = CreatePreProxyConnector(socksConnection);

        var result = await connector.ConnectAsync(ForwardProxyTarget, CancellationToken.None);

        AssertProxyFailure(result, socksConnection, "cannot complete SOCKS5 connection to 10.0.0.1. (5)");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenThePreProxyCannotBeReached_FailsWithCouldntConnectNamingTheHttpProxyAndThePreProxy()
    {
        // curl --preproxy socks5://127.0.0.1:41081 -x http://10.0.0.1:3128 http://h/ ->
        // curl: (7) Failed to connect to 10.0.0.1:3128 over proxy 127.0.0.1 after 2048 ms: Could not connect to server
        var timeProvider = new ManualTimeProvider();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ =>
            {
                timeProvider.Advance(2048);
                throw new SocketException((int)SocketError.ConnectionRefused);
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(PreProxyAddress), dialer, new FakeTlsProvider(), timeProvider, preProxy: Socks5PreProxy);

        var result = await connector.ConnectAsync(
            new ConnectTarget("h", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "10.0.0.1", 3128, null) },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 10.0.0.1:3128 over proxy socks.example after 2048 ms: Could not connect to server", result.ErrorMessage);
        Assert.IsTrue(result.IsConnectionRefused);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(PreProxyAddress, 1080) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAForwardProxyWhenThePreProxyCannotBeReached_FailsWithCouldntConnectNamingTheForwardProxy()
    {
        var connector = new TcpConnector(new FakeDnsResolver(PreProxyAddress), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(), preProxy: Socks5PreProxy);

        var result = await connector.ConnectAsync(ForwardProxyTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to 10.0.0.1:3128 over proxy socks.example after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenThePreProxyDoesNotResolve_FailsWithCouldntResolveProxyNamingThePreProxy()
    {
        // curl --preproxy socks5://nonexistent.invalid:41081 -x http://10.0.0.1:3128 http://h/ ->
        // curl: (5) Could not resolve proxy: nonexistent.invalid
        var resolver = new FakeDnsResolver();
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(), preProxy: Socks5PreProxy);

        var result = await connector.ConnectAsync(PlainTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: socks.example", result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "socks.example" }, resolver.ResolvedHosts);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughASocksProxyWithAPreProxy_DialsTheSocksProxyItself()
    {
        var socksConnection = new ScriptedConnection([.. Socks5NoAuthentication, .. Socks5Succeeded]);
        var dialer = new FakeTcpDialer { DialOutcome = _ => socksConnection };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider(), new ManualTimeProvider(), preProxy: Socks5PreProxy);

        var result = await connector.ConnectAsync(
            new ConnectTarget("10.0.0.1", 3128, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Socks5, "other.example", 1080, null) },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(ProxyAddress, 1080) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_DirectlyWithAPreProxy_DialsTheHostItself()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(), preProxy: Socks5PreProxy);

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(Socks5PreProxy, connector.PreProxy);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 80) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAForwardProxyWithoutAPreProxy_DialsTheForwardProxyItself()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider());

        var result = await connector.ConnectAsync(ForwardProxyTarget, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(connector.PreProxy);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 3128) }, dialer.DialedEndPoints);
    }

    // A connector whose resolver answers the pre-proxy's address and whose dialer answers every dial with socksConnection.
    private static (TcpConnector Connector, FakeTcpDialer Dialer) CreatePreProxyConnector(ScriptedConnection socksConnection, FakeTlsProvider? tlsProvider = null)
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => socksConnection };
        var connector = new TcpConnector(new FakeDnsResolver(PreProxyAddress), dialer, tlsProvider ?? new FakeTlsProvider(), new ManualTimeProvider(), preProxy: Socks5PreProxy);
        return (connector, dialer);
    }
}
