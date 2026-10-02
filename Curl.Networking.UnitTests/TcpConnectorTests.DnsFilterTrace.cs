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

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47110, UseTls: false) { Events = events }, CancellationToken.None);

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
    public async Task ConnectAsync_TracingTheDnsFilterWhenTheDialIsRefused_EndsWithTheFiltersExit7()
    {
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesDnsFilter = true };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47199, UseTls: false) { Events = events }, CancellationToken.None);

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

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 80, UseTls: false) { Events = events }, CancellationToken.None);

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

        await connector.ConnectAsync(new ConnectTarget("example.test", 80, UseTls: false) { Events = events }, CancellationToken.None);

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
