using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesDnsFilter" />, curl 8.21.0's <c>[DNS]</c> filter lines under
/// <c>-v --trace-config dns</c>, and <see cref="TcpConnector.ResolverEvents" /> (measured, BL-1102 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_TracingTheDnsFilter_WritesCurlsDnsLinesAroundTheConnect()
    {
        // curl -v --trace-config dns http://127.0.0.1:47110/ (BL-1102 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesDnsFilter = true,
        };

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47110, UseTls: false) { Events = events });

        Diagnostics.Assert("events.Calls", string.Join(" | ", new[] { "[DNS] created DNS filter for 127.0.0.1:47110, transport=3, queries=3", "[DNS] added", "[DNS] cf_dns_start host 127.0.0.1:47110", "  Trying 127.0.0.1:47110...", "[DNS] Curl_conn_connect(block=0) -> 0, done=0", "[DNS] connected filter chain below", "[DNS] Curl_conn_connect(block=0) -> 0, done=1", "opened", "[DNS] removing connected setup filter", "[DNS] destroy", }), string.Join(" | ", events.Calls));
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for 127.0.0.1:47110, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:47110",
                "  Trying 127.0.0.1:47110...",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "opened",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheDnsFilterWhenTheNameDoesNotResolve_WritesCurlsFailedResolveLines()
    {
        // curl 8.21.0 -s -v --trace-config dns http://nonexistent.invalid:47114/, leaving out the
        // threaded resolver's poll-timed lines (BL-1157 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesDnsFilter = true,
        };

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("nonexistent.invalid", 47114, UseTls: false) { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for nonexistent.invalid:47114, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host nonexistent.invalid:47114",
                "Could not resolve host: nonexistent.invalid",
                "Could not resolve host: nonexistent.invalid",
                "[DNS] cache negative name resolve for nonexistent.invalid:47114 type=A+AAAA",
                "Could not resolve: nonexistent.invalid:47114",
                "[DNS] error resolving: 6",
                "[DNS] Curl_conn_connect(block=0) -> 6, done=0",
                "[DNS] Curl_conn_connect(), filter returned 6",
                "[DNS] [1] shutdown async",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheDohResolverAnswersNothing_WritesCouldNotResolveHostOnce()
    {
        // curl 8.21.0 -s -v --doh-url https://127.0.0.1:47112/dns-query http://example.test:47113/
        // with a 3-byte answer (BL-1157 Notes; BL-642 Notes without --trace-config).
        var doh = new DohDnsResolver(new FakeConnector { Failure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect") }, new Uri("https://127.0.0.1:47112/dns-query"));
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(doh, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 47113, UseTls: false) { Events = events });

        Diagnostics.Assert("events.Calls", string.Join(" | ", new[] { "Could not resolve host: example.test", "Could not resolve: example.test:47113" }), string.Join(" | ", events.Calls));
        CollectionAssert.AreEqual(new[] { "Could not resolve host: example.test", "Could not resolve: example.test:47113" }, events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheResolverOptionsDoNotParse_WritesNoFailedResolveLines()
    {
        // Exit 43 "Error 43 resolving h:p" is c-ares' configuration error, not a name that did not resolve.
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new ReasoningDnsResolver(new DnsResolution([], DnsLookupFailure.BadConfiguration)), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("h", 1, UseTls: false) { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, result.ExitCode);
        Assert.IsEmpty(events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheDnsFilterWhenTheDialIsRefused_EndsWithTheFiltersExit7()
    {
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesDnsFilter = true };

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 47199, UseTls: false) { Events = events });

        Diagnostics.Assert("result.ExitCode", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                "Failed to connect to 127.0.0.1:47199 after 0 ms: Could not connect to server",
                "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "[DNS] Curl_conn_connect(), filter returned 7",
            },
            events.Calls.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_NotTracingTheDnsFilter_WritesNoDnsLine()
    {
        var events = new CountingTransferEvents();
        var connector = CreateConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider());

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("events.Calls", string.Join(" | ", new[] { "  Trying 127.0.0.1:80...", "opened" }), string.Join(" | ", events.Calls));
        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:80...", "opened" }, events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_WithResolverEvents_PointsThemAtTheResolvingTransfersEvents()
    {
        var events = new CountingTransferEvents();
        var resolverEvents = new FlowScopedTransferEvents();
        var resolver = new ResolverEventsReportingResolver(resolverEvents);
        var connector = new TcpConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            ResolverEvents = resolverEvents,
        };

        await ConnectLoggedAsync(connector, new ConnectTarget("example.test", 80, UseTls: false) { Events = events });

        Diagnostics.Assert("events.Calls[0]", "[DNS] resolving example.test", events.Calls[0]);
        Assert.AreEqual("[DNS] resolving example.test", events.Calls[0]);
        Assert.IsNull(resolverEvents.Current);
    }

    // Reports one line to the run's resolver events as it resolves, as DohDnsResolver reports its
    // --trace-config doh lines.
    private sealed class ResolverEventsReportingResolver(ITransferEvents resolverEvents) : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            resolverEvents.ReportInfo($"[DNS] resolving {host}");
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([Loopback]);
        }
    }
}
