using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// <see cref="TcpConnector.ConnectMultiplexedAsync" /> through an HTTP or HTTPS proxy (BL-942):
/// the CONNECT-UDP request curl 8.22.0 sends, its reply, and a real QUIC handshake carried in
/// <c>DATAGRAM</c> capsules to the in-memory server behind <see cref="CapsuleQuicProxyConnection" />.
/// </summary>
public sealed partial class TcpConnectorQuicTests
{
    private const string UpgradeReply = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n";

    private static readonly IPAddress TunnelProxyAddress = IPAddress.Parse("192.0.2.10");

    private static readonly ProxyEndpoint TunnelHttpProxy = new(ProxyKind.Http, "proxy.example", 3128, null);

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAnHttpProxy_SendsCurlsConnectUdpRequestAndRunsQuicInTheTunnel()
    {
        // curl 8.22.0 --http3-only -x http://127.0.0.1:18942 https://example.com/ -v (BL-942 Notes):
        // the GET upgrade to connect-udp, "CONNECT-UDP tunnel established, response 101", then
        // the QUIC handshake in DATAGRAM capsules with no Trying line of its own.
        var proxy = new CapsuleQuicProxyConnection(UpgradeReply, Server());
        var events = new RecordingTransferEvents();
        var resolver = new FakeDnsResolver(TunnelProxyAddress);

        var result = await TunnelConnector(_ => proxy, resolver: resolver).ConnectMultiplexedAsync(TunnelTarget(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        Assert.AreEqual("h3", connection.ApplicationProtocol);
        Assert.AreEqual(new IPEndPoint(TunnelProxyAddress, 3128), connection.RemoteEndPoint);
        Assert.AreEqual(
            "GET http://proxy.example:3128/.well-known/masque/udp/quic.test/443/ HTTP/1.1\r\n"
                + "Host: proxy.example:3128\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n"
                + "Connection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n",
            proxy.RequestHead);
        Assert.AreEqual(0x00, proxy.Written[proxy.RequestHead.Length], "a DATAGRAM capsule follows the head");
        Assert.IsGreaterThan(0, proxy.CapsulesReceived);
        CollectionAssert.AreEqual(new[] { "proxy.example" }, resolver.ResolvedHosts);
        CollectionAssert.Contains(events.Info, "CONNECT-UDP tunnel established, response 101");
        CollectionAssert.DoesNotContain(events.Info, "  Trying 192.0.2.10:443...");
        Assert.AreEqual("quic.test", events.Opened.Single().HostName);
        Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 50000), events.Opened.Single().LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAProxyWithACredential_SendsBasicProxyAuthorization()
    {
        // curl 8.22.0 --http3-only --proxy1.0 127.0.0.1:18942 -U u:p https://[::1]:8443/ (BL-942 Notes).
        var proxy = new CapsuleQuicProxyConnection("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n", null);
        var target = new ConnectTarget("::1", 8443, UseTls: true)
        {
            PoolScheme = "https",
            Proxy = TunnelHttpProxy with { Kind = ProxyKind.Http10, Credential = new NetworkCredential("u", "p") },
        };

        await TunnelConnector(_ => proxy).ConnectMultiplexedAsync(target, CancellationToken.None);

        Assert.AreEqual(
            "GET http://proxy.example:3128/.well-known/masque/udp/%3A%3A1/8443/ HTTP/1.0\r\n"
                + "Host: proxy.example:3128\r\nProxy-Authorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n"
                + "Connection: Upgrade\r\nUpgrade: connect-udp\r\nCapsule-Protocol: ?1\r\n\r\n",
            proxy.RequestHead);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyRefusesTheUdpTunnel_FailsWithExit7AndDisposesTheConnection()
    {
        // curl 8.22.0 answered 403: "* CONNECT-UDP tunnel failed, response 403", then
        // curl: (7) CONNECT-UDP tunnel failed, response 403 (BL-942 Notes).
        const string refusal = "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n";
        var proxy = new CapsuleQuicProxyConnection(refusal, null);
        var events = new HeadRecordingTransferEvents();

        var result = await TunnelConnector(_ => proxy).ConnectMultiplexedAsync(TunnelTarget() with { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT-UDP tunnel failed, response 403", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(proxy.IsDisposed);
        CollectionAssert.AreEqual(new[] { refusal }, events.Heads);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyAnswers200_OpensTheTunnel()
    {
        // curl 8.22.0 takes a 200 as it takes a 101: "CONNECT-UDP tunnel established, response 200".
        var proxy = new CapsuleQuicProxyConnection("HTTP/1.1 200 OK\r\n\r\n", Server());
        var events = new RecordingTransferEvents();

        var result = await TunnelConnector(_ => proxy).ConnectMultiplexedAsync(TunnelTarget(events), CancellationToken.None);

        await using var connection = result.Connection!;
        CollectionAssert.Contains(events.Info, "CONNECT-UDP tunnel established, response 200");
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyClosesBeforeItsReplyEnds_FailsWithRecvError()
    {
        var proxy = new CapsuleQuicProxyConnection(string.Empty, null);

        var result = await TunnelConnector(_ => proxy).ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.ErrorMessage);
        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyClosesWithoutAReply_WritesNoHead()
    {
        var events = new HeadRecordingTransferEvents();

        var result = await TunnelConnector(_ => new CapsuleQuicProxyConnection(string.Empty, null))
            .ConnectMultiplexedAsync(TunnelTarget() with { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.IsEmpty(events.Heads);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyClosesTheTunnel_FailsTheHandshakeAndDisposesTheConnection()
    {
        var proxy = new CapsuleQuicProxyConnection(UpgradeReply, null);

        var result = await TunnelConnector(_ => proxy).ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "QUIC: recvfrom() unexpectedly returned -1");
        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenReadingTheReplyThrows_DisposesTheConnectionAndRethrows()
    {
        var proxy = new ScriptedConnection([]) { ReadException = new IOException("reset") };

        await Assert.ThrowsExactlyAsync<IOException>(
            () => TunnelConnector(_ => proxy).ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None).AsTask());

        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyDoesNotResolve_FailsWithCouldntResolveProxy()
    {
        var connector = TunnelConnector(_ => new FakeConnection(), resolver: new FakeDnsResolver());

        var result = await connector.ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: proxy.example", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyRefusesTheConnection_FailsNamingTheTargetAndTheProxy()
    {
        var connector = TunnelConnector(_ => throw new SocketException((int)SocketError.ConnectionRefused));

        var result = await connector.ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to quic.test:443 over proxy proxy.example after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAProxyWithABadResolveEntry_FailsWithExit49()
    {
        var connector = TunnelConnector(_ => new FakeConnection(), resolveOverrides: ResolveOverrides.Parse(["bad"]));

        var result = await connector.ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAnHttpsProxy_SecuresItAndSaysWhatItsAlpnAgreed()
    {
        // curl 8.22.0 -x https://localhost:18942 --proxy-insecure: "CONNECT-UDP: no ALPN
        // negotiated", then "GET https://localhost:18942/.well-known/masque/udp/..." (BL-942 Notes).
        foreach (var (agreed, line) in new[] { ("http/1.1", "CONNECT-UDP: 'http/1.1' negotiated"), (null, "CONNECT-UDP: no ALPN negotiated") })
        {
            var secured = new CapsuleQuicProxyConnection("HTTP/1.1 403 Forbidden\r\n\r\n", null);
            var tls = new SequencedTlsProvider(ConnectResult.Connected(secured, null, applicationProtocol: agreed));
            var events = new RecordingTransferEvents();
            var target = TunnelTarget(events) with { Proxy = TunnelHttpProxy with { Kind = ProxyKind.Https } };

            await TunnelConnector(_ => new FakeConnection(), tlsProvider: tls).ConnectMultiplexedAsync(target, CancellationToken.None);

            Assert.AreEqual("proxy.example", tls.ReceivedTargetHosts.Single());
            CollectionAssert.Contains(events.Info, line);
            StringAssert.StartsWith(secured.RequestHead, "GET https://proxy.example:3128/.well-known/masque/udp/quic.test/443/ HTTP/1.1\r\n");
        }
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheHttpsProxysHandshakeFails_FailsWithItsExitCode()
    {
        var tls = new SequencedTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake failed"));
        var target = TunnelTarget() with { Proxy = TunnelHttpProxy with { Kind = ProxyKind.Https } };

        var result = await TunnelConnector(_ => new FakeConnection(), tlsProvider: tls).ConnectMultiplexedAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("handshake failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyStallsPastTheConnectTimeout_FailsWithExit28()
    {
        var time = new ManualTimeProvider();
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(TunnelProxyAddress),
            new StallingTcpDialer { OnStalled = () => time.Advance(1001) },
            new FakeTlsProvider(),
            time,
            connectTimeout: TimeSpan.FromSeconds(1),
            quicDialer: new QuicDialer(new QuicServerChannelOpener(), new TlsClientOptions(Insecure: true), true, time, SystemTlsRandomSource.Instance));

        var result = await connector.ConnectMultiplexedAsync(TunnelTarget(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 1001 milliseconds", result.ErrorMessage);
        CollectionAssert.Contains(events.Info, "Connection timed out after 1001 milliseconds");
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheCallerCancelsTheTunnelBeforeTheTimeout_Throws()
    {
        using var caller = new CancellationTokenSource();
        var time = new ManualTimeProvider();
        var connector = new TcpConnector(
            new FakeDnsResolver(TunnelProxyAddress),
            new StallingTcpDialer { OnStalled = caller.Cancel },
            new FakeTlsProvider(),
            time,
            quicDialer: new QuicDialer(new QuicServerChannelOpener(), new TlsClientOptions(Insecure: true), true, time, SystemTlsRandomSource.Instance));

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connector.ConnectMultiplexedAsync(TunnelTarget(), caller.Token));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheTunnelUsesUpTheConnectTimeout_TimesOutBeforeTheHandshake()
    {
        var time = new SteppingTimeProvider(0) { Step = 1000 };
        var proxy = new CapsuleQuicProxyConnection(UpgradeReply, Server());

        var result = await TunnelConnector(_ => proxy, time, connectTimeout: TimeSpan.FromSeconds(1)).ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(0, proxy.CapsulesReceived);
        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughASocksProxy_DialsQuicDirectly()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var target = Target() with { Proxy = TunnelHttpProxy with { Kind = ProxyKind.Socks5 } };

        var result = await Connector(opener, new ManualTimeProvider()).ConnectMultiplexedAsync(target, CancellationToken.None);

        await using var connection = result.Connection!;
        Assert.HasCount(1, opener.Opened);
    }

    private static ConnectTarget TunnelTarget(ITransferEvents? events = null) =>
        Target(events) with { Proxy = TunnelHttpProxy };

    private static TcpConnector TunnelConnector(
        Func<IPEndPoint, IConnection> dialOutcome,
        TimeProvider? clock = null,
        FakeDnsResolver? resolver = null,
        ITlsProvider? tlsProvider = null,
        ResolveOverrides? resolveOverrides = null,
        TimeSpan? connectTimeout = null)
    {
        var time = clock ?? new ManualTimeProvider();
        return new(
            resolver ?? new FakeDnsResolver(TunnelProxyAddress),
            new FakeTcpDialer { DialOutcome = dialOutcome },
            tlsProvider ?? new FakeTlsProvider(),
            time,
            proxyTunnelOptions: HttpProxyTunnelOptions.Default,
            resolveOverrides: resolveOverrides,
            connectTimeout: connectTimeout,
            quicDialer: new QuicDialer(new QuicServerChannelOpener(), new TlsClientOptions(Insecure: true), true, time, SystemTlsRandomSource.Instance));
    }
}
