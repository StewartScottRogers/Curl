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
        Diagnostics.Arrange("proxy", "http://proxy.example:3128, answering 101");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => proxy, resolver: resolver), TunnelTarget(events));

        Diagnostics.Act("request head", proxy.RequestHead);
        Diagnostics.Act("capsules received", proxy.CapsulesReceived);
        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
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

        Diagnostics.Arrange("proxy", "HTTP/1.0 proxy.example:3128 with credential u:p");

        await ConnectMultiplexedAsync(TunnelConnector(_ => proxy), target);

        Diagnostics.Act("request head", proxy.RequestHead);
        Diagnostics.Assert("Proxy-Authorization present", true, proxy.RequestHead.Contains("Proxy-Authorization: Basic dTpw\r\n", StringComparison.Ordinal));
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
        Diagnostics.Arrange("proxy reply", "403 Forbidden");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => proxy), TunnelTarget() with { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
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
        Diagnostics.Arrange("proxy reply", "200 OK");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => proxy), TunnelTarget(events));

        await using var connection = result.Connection!;
        ActEvents(events);
        Diagnostics.Assert("has the established line", true, events.Info.Contains("CONNECT-UDP tunnel established, response 200"));
        CollectionAssert.Contains(events.Info, "CONNECT-UDP tunnel established, response 200");
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenThe101ReplyCarriesAContentLength_SaysItIgnoresItInACONNECTUDPResponse()
    {
        // curl ignores a 101's Content-Length only for CONNECT-UDP and names the method in the line (BL-1399).
        var proxy = new CapsuleQuicProxyConnection("HTTP/1.1 101 Switching Protocols\r\nContent-Length: 5\r\nCapsule-Protocol: ?1\r\n\r\n", Server());
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("proxy reply", "101 with Content-Length: 5");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => proxy), TunnelTarget(events));

        await using var connection = result.Connection!;
        ActEvents(events);
        Diagnostics.Assert("has the ignoring line", true, events.Info.Contains("Ignoring Content-Length in CONNECT-UDP 101 response"));
        CollectionAssert.Contains(events.Info, "Ignoring Content-Length in CONNECT-UDP 101 response");
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyClosesBeforeItsReplyEnds_FailsWithRecvError()
    {
        var proxy = new CapsuleQuicProxyConnection(string.Empty, null);
        Diagnostics.Arrange("proxy reply", "none, closes at once");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => proxy), TunnelTarget());

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.ErrorMessage);
        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyClosesWithoutAReply_WritesNoHead()
    {
        var events = new HeadRecordingTransferEvents();
        Diagnostics.Arrange("proxy reply", "none, closes at once");

        var result = await ConnectMultiplexedAsync(
            TunnelConnector(_ => new CapsuleQuicProxyConnection(string.Empty, null)),
            TunnelTarget() with { Events = events });

        Diagnostics.Assert("head count", 0, events.Heads.Count);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Assert.IsEmpty(events.Heads);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyClosesTheTunnel_FailsTheHandshakeAndDisposesTheConnection()
    {
        var proxy = new CapsuleQuicProxyConnection(UpgradeReply, null);
        Diagnostics.Arrange("proxy", "answers 101, then closes the tunnel");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => proxy), TunnelTarget(), writesErrorMessage: false);

        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        StringAssert.StartsWith(result.ErrorMessage, "QUIC: recvfrom() unexpectedly returned -1");
        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenReadingTheReplyThrows_DisposesTheConnectionAndRethrows()
    {
        var proxy = new ScriptedConnection([]) { ReadException = new IOException("reset") };
        Diagnostics.Arrange("read exception", "IOException: reset");

        var exception = await Assert.ThrowsExactlyAsync<IOException>(
            () => TunnelConnector(_ => proxy).ConnectMultiplexedAsync(TunnelTarget(), CancellationToken.None).AsTask());

        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Assert("proxy disposed", true, proxy.IsDisposed);
        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyDoesNotResolve_FailsWithCouldntResolveProxy()
    {
        var connector = TunnelConnector(_ => new FakeConnection(), resolver: new FakeDnsResolver());
        Diagnostics.Arrange("resolver addresses", "none");

        var result = await ConnectMultiplexedAsync(connector, TunnelTarget());

        Diagnostics.Assert("exit code", CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: proxy.example", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheProxyRefusesTheConnection_FailsNamingTheTargetAndTheProxy()
    {
        var connector = TunnelConnector(_ => throw new SocketException((int)SocketError.ConnectionRefused));
        Diagnostics.Arrange("proxy dial", SocketError.ConnectionRefused);

        var result = await ConnectMultiplexedAsync(connector, TunnelTarget());

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("Failed to connect to quic.test:443 over proxy proxy.example after 0 ms: Could not connect to server", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughAProxyWithABadResolveEntry_FailsWithExit49()
    {
        var connector = TunnelConnector(_ => new FakeConnection(), resolveOverrides: ResolveOverrides.Parse(["bad"]));
        Diagnostics.Arrange("resolve entry", "bad");

        var result = await ConnectMultiplexedAsync(connector, TunnelTarget());

        Diagnostics.Assert("exit code", CurlExitCode.SetoptOptionSyntax, result.ExitCode);
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
            Diagnostics.Arrange("proxy's agreed ALPN", agreed);

            await ConnectMultiplexedAsync(TunnelConnector(_ => new FakeConnection(), tlsProvider: tls), target);

            ActEvents(events);
            Diagnostics.Assert("has the ALPN line", true, events.Info.Contains(line));
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
        Diagnostics.Arrange("proxy handshake", "fails with SslConnectError");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => new FakeConnection(), tlsProvider: tls), target);

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
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
        Diagnostics.Arrange("connect timeout", "1000 ms; the stalled proxy advances the clock 1001 ms");

        var result = await ConnectMultiplexedAsync(connector, TunnelTarget(events));

        ActEvents(events);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
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
        Diagnostics.Arrange("cancelled", "when the proxy stalls");

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connector.ConnectMultiplexedAsync(TunnelTarget(), caller.Token));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("cancellation requested", true, caller.IsCancellationRequested);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_WhenTheTunnelUsesUpTheConnectTimeout_TimesOutBeforeTheHandshake()
    {
        var time = new SteppingTimeProvider(0) { Step = 1000 };
        var proxy = new CapsuleQuicProxyConnection(UpgradeReply, Server());
        Diagnostics.Arrange("connect timeout", "1000 ms; the clock steps 1000 ms a reading");

        var result = await ConnectMultiplexedAsync(TunnelConnector(_ => proxy, time, connectTimeout: TimeSpan.FromSeconds(1)), TunnelTarget());

        Diagnostics.Act("capsules received", proxy.CapsulesReceived);
        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(0, proxy.CapsulesReceived);
        Assert.IsTrue(proxy.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_ThroughASocksProxy_DialsQuicDirectly()
    {
        var opener = new QuicServerChannelOpener { ServerFor = _ => Server() };
        var target = Target() with { Proxy = TunnelHttpProxy with { Kind = ProxyKind.Socks5 } };
        Diagnostics.Arrange("proxy kind", ProxyKind.Socks5);

        var result = await ConnectMultiplexedAsync(Connector(opener, new ManualTimeProvider()), target);

        await using var connection = result.Connection!;
        Diagnostics.Assert("channels opened", 1, opener.Opened.Count);
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
