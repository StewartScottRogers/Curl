using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesDnsFilter" /> through a tunnelling proxy, a Unix socket, a
/// negative DNS cache entry and a looked-up name whose dial is refused, as curl 8.21.0 writes its
/// <c>[DNS]</c> lines for each (measured, BL-1181 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_TracingTheDnsFilterThroughATunnellingProxy_WritesTheFilterLinesForTheProxy()
    {
        // curl -s -v --trace-config dns -p -x http://127.0.0.1:47115 http://example.test/ (refused).
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesDnsFilter = true };
        var target = new ConnectTarget("example.test", 80, UseTls: false) { Events = events, Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 47115, null) };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for 127.0.0.1:47115, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:47115",
            },
            events.Calls.Take(3).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "Failed to connect to example.test:80 over proxy 127.0.0.1 after 0 ms: Could not connect to server",
                "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "[DNS] Curl_conn_connect(), filter returned 7",
            },
            events.Calls.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheDnsFilterOverARefusedUnixSocket_WritesTheUnixSocketFiltersLines()
    {
        // curl -s -v --trace-config dns --unix-socket <path> http://example.test/ (refused).
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(), dialer, new FakeTlsProvider(), new ManualTimeProvider(), unixSocket: new UnixSocketAddress("/tmp/s.sock", IsAbstract: false))
        {
            TracesDnsFilter = true,
        };

        var result = await connector.ConnectAsync(new ConnectTarget("example.test", 80, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for /tmp/s.sock:0, transport=6, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start unix-domain-socket /tmp/s.sock:0",
                "  Trying /tmp/s.sock:0...",
                "Immediate connect fail for /tmp/s.sock: Connection refused",
                "connect to /tmp/s.sock port 0 from  port 0 failed: Connection refused",
                "Failed to connect to example.test:80 over unix:///tmp/s.sock after 0 ms: Could not connect to server",
                "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "[DNS] Curl_conn_connect(), filter returned 7",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheDnsFilterOnANegativeCacheEntry_WritesTheEntrysLinesAndExit6()
    {
        // curl -s -v --trace-config dns -6 --resolve foo:47500:127.0.0.1 http://foo:47500/.
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["foo:47500:127.0.0.1"]),
            addressFamily: AddressFamily.InterNetworkV6)
        {
            TracesDnsFilter = true,
        };

        var result = await connector.ConnectAsync(new ConnectTarget("foo", 47500, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "Added foo:47500:127.0.0.1 to DNS cache",
                "[DNS] created DNS filter for foo:47500, transport=3, queries=2",
                "[DNS] added",
                "[DNS] cf_dns_start host foo:47500",
                "[DNS] cache entry does not have type=AAAA addresses",
                "Negative DNS entry",
                "Could not resolve host: foo",
                "Could not resolve: foo:47500",
                "Could not resolve: foo",
                "[DNS] error resolving: 6",
                "[DNS] Curl_conn_connect(block=0) -> 6, done=0",
                "[DNS] Curl_conn_connect(), filter returned 6",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheDnsFilterWhenALookedUpNameIsRefused_CompletesTheResolveAndShutsItDown()
    {
        // curl -s -v --trace-config dns http://<a name the resolver answers>:47199/ (refused).
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesDnsFilter = true };

        await connector.ConnectAsync(new ConnectTarget("example.test", 47199, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] cf_dns_start host example.test:47199",
                "[DNS] resolve complete for example.test:47199",
                "Host example.test:47199 was resolved.",
            },
            events.Calls.Skip(2).Take(3).ToArray());
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "[DNS] Curl_conn_connect(), filter returned 7",
                "[DNS] [1] shutdown async",
            },
            events.Calls.TakeLast(3).ToArray());
    }
}
