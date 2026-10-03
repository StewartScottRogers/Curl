using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesHttpsConnectFilter" />, curl 8.21.0's <c>[HTTPS-CONNECT]</c>
/// lines under <c>--trace-config https-connect</c> and <c>all</c>, and the <c>[SETUP]</c> lines of an
/// <c>https://</c> origin beside them (measured, BL-1192 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string HttpsConnecting = "[HTTPS-CONNECT] connect -> 0, done=0";
    private const string HttpsPollset = "[HTTPS-CONNECT] adjust_pollset -> 0, 1 socks";

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectAndSetupFilters_WritesTheirLinesInCurlsOrder()
    {
        // curl -s -k -v --trace-config https-connect,setup --http1.1 https://127.0.0.1:18443/ (BL-1192 Notes).
        var events = new CountingTransferEvents();
        var tlsProvider = new FakeTlsProvider { HandshakeToReport = Handshake(verified: false) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, tlsProvider, new ManualTimeProvider())
        {
            TracesSetupFilter = true,
            TracesHttpsConnectFilter = true,
            HttpsConnectFirstAttemptVersion = "h1",
        };

        var result = await connector.ConnectAsync(HttpsOrigin(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] added",
                "[HTTPS-CONNECT] connect, init",
                "[HTTPS-CONNECT] 1st attempt uses h1 from wanted versions",
                "[SETUP] happy eyeballing to origin 127.0.0.1:18443",
                "  Trying 127.0.0.1:18443...",
                HttpsConnecting,
                HttpsPollset,
                "[SETUP] added SSL filter for origin",
                HttpsConnecting,
                HttpsPollset,
                HttpsConnecting,
                HttpsPollset,
                "handshake",
                "[HTTPS-CONNECT] connect -> 0, done=1",
                "opened",
                "[HTTPS-CONNECT] removing connected setup filter",
                "[HTTPS-CONNECT] destroy",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectAndDnsFilters_NestsTheDnsLinesInside()
    {
        // curl -s -k -v --trace-config all: [HTTPS-CONNECT] added before [DNS] created, its removal after [DNS]'s.
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesDnsFilter = true,
            TracesHttpsConnectFilter = true,
        };

        await connector.ConnectAsync(HttpsOrigin(events), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] added",
                "[DNS] created DNS filter for 127.0.0.1:18443, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:18443",
                "[HTTPS-CONNECT] connect, init",
                "[HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
                "  Trying 127.0.0.1:18443...",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=0",
                HttpsConnecting,
                HttpsPollset,
                "[HTTPS-CONNECT] connect -> 0, done=1",
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "opened",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
                "[HTTPS-CONNECT] removing connected setup filter",
                "[HTTPS-CONNECT] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectFilterWhenTheDialIsRefused_EndsWithAllAttemptsFailedAndExit7()
    {
        // curl -s -k -v --trace-config https-connect https://127.0.0.1:18444/ with nothing listening.
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesHttpsConnectFilter = true };

        var result = await connector.ConnectAsync(HttpsOrigin(events), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] connect, all attempts failed", "[HTTPS-CONNECT] connect -> 7, done=0" },
            events.Calls.TakeLast(2).ToArray());
        Assert.IsFalse(events.Calls.Contains("[HTTPS-CONNECT] removing connected setup filter"));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectFilterWhenTheHandshakeFails_EndsWithTheHandshakesExitCode()
    {
        // curl -s -v --trace-config https-connect against an untrusted certificate: exit 60.
        var events = new CountingTransferEvents();
        var tlsProvider = new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.PeerFailedVerification, "x") };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, tlsProvider, new ManualTimeProvider())
        {
            TracesHttpsConnectFilter = true,
        };

        await connector.ConnectAsync(HttpsOrigin(events), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] connect, all attempts failed", "[HTTPS-CONNECT] connect -> 60, done=0" },
            events.Calls.TakeLast(2).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHttpsConnectFilterWhenNothingResolves_WritesOnlyTheAddedLine()
    {
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider()) { TracesHttpsConnectFilter = true };

        var result = await connector.ConnectAsync(new ConnectTarget("nowhere.test", 18443, UseTls: true) { Events = events, PoolScheme = "https" }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] added" },
            events.Calls.Where(line => line.StartsWith("[HTTPS-CONNECT]", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    [DataRow("http", false, false)]
    [DataRow("ftps", true, false)]
    [DataRow(null, true, false)]
    [DataRow("https", true, true)]
    public async Task ConnectAsync_TracingTheHttpsConnectFilterForAnythingButAnHttpsOrigin_WritesNoHttpsConnectLine(string? scheme, bool useTls, bool forwardProxy)
    {
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesHttpsConnectFilter = true,
        };
        var target = new ConnectTarget("127.0.0.1", 18443, useTls) { Events = events, PoolScheme = scheme, IsForwardProxy = forwardProxy };

        await connector.ConnectAsync(target, CancellationToken.None);

        Assert.IsFalse(events.Calls.Any(line => line.StartsWith("[HTTPS-CONNECT]", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheSetupFilterOfAnHttpsOrigin_WritesNoAddedLineButTheSslFilterLine()
    {
        // curl -s -k -v --trace-config setup https://...: the ALPN connect filter adds the setup filter.
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesSetupFilter = true,
        };

        await connector.ConnectAsync(HttpsOrigin(events), CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] happy eyeballing to origin 127.0.0.1:18443",
                "  Trying 127.0.0.1:18443...",
                "[SETUP] added SSL filter for origin",
                "opened",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
            },
            events.Calls);
    }

    private static ConnectTarget HttpsOrigin(ITransferEvents events) =>
        new("127.0.0.1", 18443, UseTls: true) { Events = events, PoolScheme = "https" };
}
