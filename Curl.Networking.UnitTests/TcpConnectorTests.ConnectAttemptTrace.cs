using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector.TracesHappyEyeballsFilter" /> and <see cref="TcpConnector.TracesTcpFilter" />,
/// curl 8.21.0's <c>[HAPPY-EYEBALLS]</c> and <c>[TCP]</c> lines under <c>--trace-config happy-eyeballs</c>,
/// <c>tcp</c>, <c>network</c> and <c>all</c>, in curl's order beside the <c>[DNS]</c> and <c>[SETUP]</c>
/// lines (measured, BL-1161 Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_TracingEveryConnectFilter_WritesTheirLinesInCurlsOrder()
    {
        // curl -s -v --trace-config dns,setup,happy-eyeballs,tcp http://127.0.0.1:48764/ (BL-1161 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesSetupFilter = true,
            TracesDnsFilter = true,
            TracesHappyEyeballsFilter = true,
            TracesTcpFilter = true,
        };

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 48764, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "[SETUP] added",
                "[DNS] created DNS filter for 127.0.0.1:48764, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:48764",
                "[SETUP] happy eyeballing to origin 127.0.0.1:48764",
                "[HAPPY-EYEBALLS] init ip ballers for transport 3",
                "[HAPPY-EYEBALLS] want to do more",
                "[HAPPY-EYEBALLS] check for next AAAA address: none",
                "[HAPPY-EYEBALLS] check for next A address: found",
                "[HAPPY-EYEBALLS] starting first attempt for ipv4 -> 0",
                "  Trying 127.0.0.1:48764...",
                "[TCP] Set TCP_KEEP* on fd=3",
                "[TCP] cf_socket_open() -> 0, fd=3",
                "[TCP] local address 0.0.0.0 port 0...",
                "[HAPPY-EYEBALLS] checked connect attempts: 1 ongoing, 0 inconclusive",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=0",
                "[TCP] adjust_pollset, !connected, POLLOUT fd=3",
                "[HAPPY-EYEBALLS] adjust_pollset -> 0, 1 socks",
                "[TCP] connected on fd=3",
                "[HAPPY-EYEBALLS] connect attempt #0 successful",
                "[HAPPY-EYEBALLS] Connected to 127.0.0.1 (127.0.0.1) port 48764",
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "opened",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
                "[SETUP] removing connected setup filter",
                "[SETUP] destroy",
                "[HAPPY-EYEBALLS] removing connected setup filter",
                "[HAPPY-EYEBALLS] destroy",
            },
            events.Calls);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHappyEyeballsFilterWhenTheDialIsRefused_GivesUpBeforeTheFailedToConnectLine()
    {
        // curl -s -v --trace-config happy-eyeballs http://127.0.0.1:1/ (BL-1161 Notes).
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesHappyEyeballsFilter = true };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        var failedAt = events.Calls.FindIndex(line => line.StartsWith("Failed to connect to ", StringComparison.Ordinal));
        CollectionAssert.AreEqual(
            new[] { "[HAPPY-EYEBALLS] no more attempts to try", "[HAPPY-EYEBALLS] baller 0: result=7" },
            events.Calls[(failedAt - 2)..failedAt]);
        Assert.IsFalse(events.Calls.Contains(ConnectAttemptTraceEvents.RemovingLine));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHappyEyeballsFilterAcrossTwoFamilies_NamesTheTimeoutAndTheSecondAttempt()
    {
        // curl -s -v --trace-config happy-eyeballs http://localhost:48763/, IPv4 winning (BL-1161 Notes).
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(System.Net.IPAddress.IPv6Loopback, Loopback), dialer, new FakeTlsProvider(), time, happyEyeballsTimeout: TimeSpan.FromMilliseconds(200))
        {
            TracesHappyEyeballsFilter = true,
        };

        var connecting = connector.ConnectAsync(DualTarget(events), CancellationToken.None).AsTask();
        time.Advance(200);
        await dialer.WaitForDialsAsync(2);
        dialer.Connect(IPv4Attempt, new StallingConnection());
        await connecting;

        var lines = events.Info.Where(line => line.StartsWith("[HAPPY-EYEBALLS]", StringComparison.Ordinal)).ToList();
        CollectionAssert.IsSubsetOf(
            new[]
            {
                "[HAPPY-EYEBALLS] next HAPPY_EYEBALLS timeout in 200ms",
                "[HAPPY-EYEBALLS] happy eyeballs timeout expired, start next attempt",
                "[HAPPY-EYEBALLS] starting next attempt for ipv4 -> 0",
                "[HAPPY-EYEBALLS] connect attempt #1 successful",
                "[HAPPY-EYEBALLS] Connected to dual.example (127.0.0.1) port 18644",
                ConnectAttemptTraceEvents.DestroyLine,
            },
            lines);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHappyEyeballsTimerAcrossTwoFamilies_WritesTheTimerSetAndCleared()
    {
        // curl -s -v --trace-config timer http://localhost:47862/, IPv4 winning (BL-1186 Notes).
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(System.Net.IPAddress.IPv6Loopback, Loopback), dialer, new FakeTlsProvider(), time, happyEyeballsTimeout: TimeSpan.FromMilliseconds(200))
        {
            TracesTimers = true,
        };

        var connecting = connector.ConnectAsync(DualTarget(events), CancellationToken.None).AsTask();
        time.Advance(200);
        await dialer.WaitForDialsAsync(2);
        dialer.Connect(IPv4Attempt, new StallingConnection());
        await connecting;

        var lines = events.Info.Where(line => line.StartsWith("  Trying ", StringComparison.Ordinal) || line.StartsWith('[')).ToList();
        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying [::1]:18644...",
                "[TIMER] [HAPPY_EYEBALLS] set for 200000ns",
                "[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms",
                "  Trying 127.0.0.1:18644...",
                "[TIMER] [HAPPY_EYEBALLS] cleared",
            },
            lines);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTimersWithAConnectTimeoutAcrossTwoFamilies_WritesTheConnectTimeoutLinesFirstAndBetweenTheAttempts()
    {
        // curl -s -v --trace-config network --connect-timeout 1 http://localhost:P/, IPv4 winning (BL-1210 Notes):
        // the connect timeout is set before the [DNS] filter's first line.
        var time = new ManualTimeProvider();
        var dialer = new GatedTcpDialer(time);
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(System.Net.IPAddress.IPv6Loopback, Loopback), dialer, new FakeTlsProvider(), time, happyEyeballsTimeout: TimeSpan.FromMilliseconds(200))
        {
            TracesTimers = true,
            TracesTimerExpiry = true,
            TracedConnectTimeout = TimeSpan.FromSeconds(1),
            TracesDnsFilter = true,
        };

        var connecting = connector.ConnectAsync(DualTarget(events), CancellationToken.None).AsTask();
        time.Advance(200);
        await dialer.WaitForDialsAsync(2);
        dialer.Connect(IPv4Attempt, new StallingConnection());
        await connecting;

        Assert.StartsWith("[DNS] ", events.Info[1]);
        var lines = events.Info.Where(line => line.StartsWith("  Trying ", StringComparison.Ordinal) || line.StartsWith("[TIMER]", StringComparison.Ordinal)).ToList();
        CollectionAssert.AreEqual(
            new[]
            {
                "[TIMER] [CONNECTTIMEOUT] set for 1000000ns",
                "  Trying [::1]:18644...",
                "[TIMER] [HAPPY_EYEBALLS] set for 200000ns",
                "[TIMER] [HAPPY_EYEBALLS] expires in 200000ns",
                "[TIMER] [CONNECTTIMEOUT] expires in 1000000ns",
                "[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms",
                "  Trying 127.0.0.1:18644...",
                "[TIMER] [CONNECTTIMEOUT] expires in 800000ns",
                "[TIMER] [CONNECTTIMEOUT] gives multi timeout in 800ms",
                "[TIMER] [HAPPY_EYEBALLS] cleared",
            },
            lines);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheHappyEyeballsTimerWhenTheDialIsRefused_WritesNoTimerLine()
    {
        // curl -s -v --trace-config timer http://127.0.0.1:1/ (BL-1159 and BL-1186 Notes).
        var events = new CountingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider()) { TracesTimers = true };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.IsFalse(events.Calls.Any(line => line.StartsWith('[')));
    }

    [TestMethod]
    public async Task ConnectAsync_TracingTheTcpFilterForPlainHttp_QueriesAlpnAndTracesTheConnectionsIo()
    {
        // curl -s -v --trace-config tcp http://127.0.0.1:47195/ (BL-1195 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesTcpFilter = true,
        };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47195, UseTls: false) { Events = events, PoolScheme = "http" }, CancellationToken.None);
        await result.Connection!.WriteAsync(new byte[79], CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "[TCP] connected on fd=3", "opened", TcpConnector.QueryAlpnLine, "[TCP] send(len=79) -> 0, 79" },
            events.Calls.Skip(5).ToArray());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("ftp")]
    public async Task ConnectAsync_TracingTheTcpFilterForAnotherScheme_LeavesTheConnectionUntraced(string? poolScheme)
    {
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesTcpFilter = true,
        };

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47195, UseTls: false) { Events = events, PoolScheme = poolScheme }, CancellationToken.None);
        await result.Connection!.WriteAsync(new byte[79], CancellationToken.None);

        Assert.AreEqual("opened", events.Calls[^1]);
    }

    [TestMethod]
    public async Task ConnectAsync_TracingOnlyTheTcpFilter_WritesNoHappyEyeballsLine()
    {
        // curl -s -v --trace-config tcp http://127.0.0.1:48761/ (BL-1161 Notes).
        var events = new CountingTransferEvents();
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider())
        {
            TracesTcpFilter = true,
        };

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 48761, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:48761...",
                "[TCP] Set TCP_KEEP* on fd=3",
                "[TCP] cf_socket_open() -> 0, fd=3",
                "[TCP] local address 0.0.0.0 port 0...",
                "[TCP] adjust_pollset, !connected, POLLOUT fd=3",
                "[TCP] connected on fd=3",
                "opened",
            },
            events.Calls);
    }
}
