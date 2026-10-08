using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// <see cref="ConnectAttemptTraceEvents" /> writes curl 8.21.0's <c>[HAPPY-EYEBALLS]</c> and
/// <c>[TCP]</c> lines around the connect attempts it is told of, measured with
/// <c>Record-CurlExchange.ps1</c> on 2026-10-02 (BL-1161 Notes); socket descriptors, local ports and
/// poll repetitions are Curl's own (ADR-0357).
/// </summary>
[TestClass]
public sealed class ConnectAttemptTraceEventsTests
{
    private static readonly IPEndPoint IPv4 = new(IPAddress.Loopback, 48763);

    private static readonly IPEndPoint IPv6 = new(IPAddress.IPv6Loopback, 48763);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static string Lines(IEnumerable<string> lines) => string.Join("\n", lines);

    [TestMethod]
    public void OneAttemptThatConnects_WritesCurlsLinesForAPlainTransfer()
    {
        // curl -s -v --trace-config happy-eyeballs,tcp http://127.0.0.1:48761/
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "127.0.0.1");
        Diagnostics.Arrange("endpoint", IPv4);
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", tracesHappyEyeballs: true, tracesTcp: true);

        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[]
        {
            "[HAPPY-EYEBALLS] init ip ballers for transport 3",
            "[HAPPY-EYEBALLS] want to do more",
            "[HAPPY-EYEBALLS] check for next AAAA address: none",
            "[HAPPY-EYEBALLS] check for next A address: found",
            "[HAPPY-EYEBALLS] starting first attempt for ipv4 -> 0",
            "  Trying 127.0.0.1:48763...",
            "[TCP] Set TCP_KEEP* on fd=3",
            "[TCP] cf_socket_open() -> 0, fd=3",
            "[TCP] local address 0.0.0.0 port 0...",
            "[HAPPY-EYEBALLS] checked connect attempts: 1 ongoing, 0 inconclusive",
            "[TCP] adjust_pollset, !connected, POLLOUT fd=3",
            "[HAPPY-EYEBALLS] adjust_pollset -> 0, 1 socks",
            "[TCP] connected on fd=3",
            "[HAPPY-EYEBALLS] connect attempt #0 successful",
            "[HAPPY-EYEBALLS] Connected to 127.0.0.1 (127.0.0.1) port 48763",
        };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void TwoFamiliesRacing_WritesCurlsLinesForLocalhost()
    {
        // curl -s -v --trace-config happy-eyeballs,tcp http://localhost:48763/, IPv4 winning.
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "localhost");
        Diagnostics.Arrange("second family delay", TimeSpan.FromMilliseconds(200));
        var events = new ConnectAttemptTraceEvents(inner, "localhost", tracesHappyEyeballs: true, tracesTcp: true);

        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.SecondFamilyDue();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[]
        {
            "[HAPPY-EYEBALLS] init ip ballers for transport 3",
            "[HAPPY-EYEBALLS] want to do more",
            "[HAPPY-EYEBALLS] check for next AAAA address: found",
            "[HAPPY-EYEBALLS] starting first attempt for ipv6 -> 0",
            "  Trying [::1]:48763...",
            "[TCP] Set TCP_KEEP* on fd=3",
            "[TCP] cf_socket_open() -> 0, fd=3",
            "[TCP] local address :: port 0...",
            "[HAPPY-EYEBALLS] checked connect attempts: 1 ongoing, 0 inconclusive",
            "[HAPPY-EYEBALLS] next HAPPY_EYEBALLS timeout in 200ms",
            "[TCP] adjust_pollset, !connected, POLLOUT fd=3",
            "[HAPPY-EYEBALLS] adjust_pollset -> 0, 1 socks",
            "[TCP] not connected yet on fd=3",
            "[HAPPY-EYEBALLS] checked connect attempts: 1 ongoing, 0 inconclusive",
            "[HAPPY-EYEBALLS] happy eyeballs timeout expired, start next attempt",
            "[HAPPY-EYEBALLS] want to do more",
            "[HAPPY-EYEBALLS] check for next A address: found",
            "[HAPPY-EYEBALLS] starting next attempt for ipv4 -> 0",
            "[TCP] not connected yet on fd=3",
            "  Trying 127.0.0.1:48763...",
            "[TCP] Set TCP_KEEP* on fd=4",
            "[TCP] cf_socket_open() -> 0, fd=4",
            "[TCP] local address 0.0.0.0 port 0...",
            "[HAPPY-EYEBALLS] checked connect attempts: 2 ongoing, 0 inconclusive",
            "[TCP] adjust_pollset, !connected, POLLOUT fd=3",
            "[TCP] adjust_pollset, !connected, POLLOUT fd=4",
            "[HAPPY-EYEBALLS] adjust_pollset -> 0, 2 socks",
            "[TCP] not connected yet on fd=3",
            "[TCP] connected on fd=4",
            "[HAPPY-EYEBALLS] connect attempt #1 successful",
            "[TCP] destroy",
            "[TCP] cf_socket_close, fd=3",
            "[HAPPY-EYEBALLS] Connected to localhost (127.0.0.1) port 48763",
        };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ARefusedAttempt_WritesCurlsLinesAroundTheFailure()
    {
        // curl -s -v --trace-config happy-eyeballs,tcp http://127.0.0.1:1/, one poll round kept.
        var inner = new CountingTransferEvents();
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", tracesHappyEyeballs: true, tracesTcp: true);
        var refused = new IPEndPoint(IPAddress.Loopback, 1);
        Diagnostics.Arrange("host", "127.0.0.1");
        Diagnostics.Arrange("refused endpoint", refused);

        events.ReportInfo("  Trying 127.0.0.1:1...");
        events.AttemptFailing(refused);
        events.ReportInfo("connect to 127.0.0.1 port 1 from 0.0.0.0 port 0 failed: Connection refused");
        events.AttemptFailed(refused);
        events.NoMoreAttempts();
        var actual = inner.Calls.Skip(5).ToArray();
        Diagnostics.Act("calls after the first five", Lines(actual));

        var expected = new[]
        {
            "  Trying 127.0.0.1:1...",
            "[TCP] Set TCP_KEEP* on fd=3",
            "[TCP] cf_socket_open() -> 0, fd=3",
            "[TCP] local address 0.0.0.0 port 0...",
            "[HAPPY-EYEBALLS] checked connect attempts: 1 ongoing, 0 inconclusive",
            "[TCP] adjust_pollset, !connected, POLLOUT fd=3",
            "[HAPPY-EYEBALLS] adjust_pollset -> 0, 1 socks",
            "[TCP] poll/select error on fd=3",
            "connect to 127.0.0.1 port 1 from 0.0.0.0 port 0 failed: Connection refused",
            "[TCP] destroy",
            "[HAPPY-EYEBALLS] checked connect attempts: 0 ongoing, 0 inconclusive",
            "[HAPPY-EYEBALLS] want to do more",
            "[HAPPY-EYEBALLS] check for next AAAA address: none",
            "[HAPPY-EYEBALLS] check for next A address: none",
            "[HAPPY-EYEBALLS] no more attempts to try",
            "[HAPPY-EYEBALLS] baller 0: result=7",
        };
        Diagnostics.Diff("calls after the first five", Lines(expected), Lines(actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(true, false, "[HAPPY-EYEBALLS]")]
    [DataRow(false, true, "[TCP]")]
    public void OneComponentAlone_WritesOnlyItsOwnLines(bool tracesHappyEyeballs, bool tracesTcp, string prefix)
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("traces happy eyeballs", tracesHappyEyeballs);
        Diagnostics.Arrange("traces TCP", tracesTcp);
        Diagnostics.Arrange("prefix", prefix);
        var events = new ConnectAttemptTraceEvents(inner, "localhost", tracesHappyEyeballs, tracesTcp);

        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv6);
        var allOnPrefix = inner.Calls.Where(line => !line.StartsWith("  Trying ", StringComparison.Ordinal)).All(line => line.StartsWith(prefix, StringComparison.Ordinal));
        Diagnostics.Act("traces happy eyeballs", events.TracesHappyEyeballs);
        Diagnostics.Act("every non-Trying line starts with prefix", allOnPrefix);
        Diagnostics.Act("call count", inner.Calls.Count);

        Diagnostics.Assert("traces happy eyeballs", tracesHappyEyeballs, events.TracesHappyEyeballs);
        Diagnostics.Assert("every non-Trying line starts with prefix", true, allOnPrefix);
        Diagnostics.Assert("call count greater than", 2, inner.Calls.Count);
        Assert.AreEqual(tracesHappyEyeballs, events.TracesHappyEyeballs);
        Assert.IsTrue(inner.Calls.Where(line => !line.StartsWith("  Trying ", StringComparison.Ordinal)).All(line => line.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.IsGreaterThan(2, inner.Calls.Count);
    }

    [TestMethod]
    public void TwoFamiliesRacingUnderNetwork_WritesTheTimerLinesAmongTheFilterLines()
    {
        // curl -s -v --trace-config network http://localhost:47863/, IPv4 winning (BL-1186 Notes);
        // the [TIMER] ... expires in line is the multi's, not this filter's.
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "localhost");
        Diagnostics.Arrange("traces timer", true);
        var events = new ConnectAttemptTraceEvents(inner, "localhost", tracesHappyEyeballs: true, tracesTcp: false, tracesTimer: true);

        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.SecondFamilyDue();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var subset = new[]
        {
            "[HAPPY-EYEBALLS] next HAPPY_EYEBALLS timeout in 200ms",
            "[TIMER] [HAPPY_EYEBALLS] set for 200000ns",
            "[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms",
            "[HAPPY-EYEBALLS] connect attempt #1 successful",
            "[TIMER] [HAPPY_EYEBALLS] cleared",
            "[HAPPY-EYEBALLS] Connected to localhost (127.0.0.1) port 48763",
        };
        Diagnostics.Assert("expected lines missing from the calls", 0, subset.Count(line => !inner.Calls.Contains(line)));
        CollectionAssert.IsSubsetOf(subset, inner.Calls);
        var timeoutAt = inner.Calls.IndexOf("[HAPPY-EYEBALLS] next HAPPY_EYEBALLS timeout in 200ms");
        Diagnostics.Act("line after the timeout line", inner.Calls[timeoutAt + 1]);
        Diagnostics.Act("second line after the timeout line", inner.Calls[timeoutAt + 2]);
        Diagnostics.Assert("line after the timeout line", "[TIMER] [HAPPY_EYEBALLS] set for 200000ns", inner.Calls[timeoutAt + 1]);
        Assert.AreEqual("[TIMER] [HAPPY_EYEBALLS] set for 200000ns", inner.Calls[timeoutAt + 1]);
        Diagnostics.Assert("second line after the timeout line", "[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms", inner.Calls[timeoutAt + 2]);
        Assert.AreEqual("[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms", inner.Calls[timeoutAt + 2]);
        var connectedAt = inner.Calls.IndexOf("[HAPPY-EYEBALLS] Connected to localhost (127.0.0.1) port 48763");
        Diagnostics.Act("line before Connected", inner.Calls[connectedAt - 1]);
        Diagnostics.Assert("line before Connected", "[TIMER] [HAPPY_EYEBALLS] cleared", inner.Calls[connectedAt - 1]);
        Assert.AreEqual("[TIMER] [HAPPY_EYEBALLS] cleared", inner.Calls[connectedAt - 1]);
    }

    [TestMethod]
    public void TimerAloneOnAPlainConnect_WritesOnlyCleared()
    {
        // curl -s -v --trace-config timer http://127.0.0.1:47861/ (BL-1186 Notes).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "127.0.0.1");
        Diagnostics.Arrange("traces timer", true);
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", tracesHappyEyeballs: false, tracesTcp: false, tracesTimer: true);

        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[] { "  Trying 127.0.0.1:48763...", "[TIMER] [HAPPY_EYEBALLS] cleared" };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ConnectTimeoutOnAPlainConnect_WritesItsSetAndGivesLinesAndExpiresInOnlyUnderTheMulti(bool tracesTimerExpiry)
    {
        // curl -s -v --trace-config timer (and network) --connect-timeout 1 http://127.0.0.1:P/ (BL-1210 Notes).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("traces timer expiry", tracesTimerExpiry);
        Diagnostics.Arrange("connect timeout", TimeSpan.FromSeconds(1));
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", false, false, tracesTimer: true, tracesTimerExpiry, TimeSpan.FromSeconds(1));

        events.ConnectStarting();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        string[] expiry = tracesTimerExpiry ? ["[TIMER] [CONNECTTIMEOUT] expires in 1000000ns"] : [];
        var expected = new[] { "[TIMER] [CONNECTTIMEOUT] set for 1000000ns", "  Trying 127.0.0.1:48763..." }
            .Concat(expiry)
            .Concat(["[TIMER] [CONNECTTIMEOUT] gives multi timeout in 1000ms", "[TIMER] [HAPPY_EYEBALLS] cleared"])
            .ToArray();
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void TwoFamiliesWithAConnectTimeoutUnderTheMulti_WriteEveryTimersExpiresInAndTheNearestGivesTheTimeout()
    {
        // curl -s -v --trace-config timer,multi --connect-timeout 1 http://localhost:P/, IPv4 winning
        // (BL-1210 Notes); the elapsed time comes off the configured delays exactly (ADR-0357).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "localhost");
        Diagnostics.Arrange("connect timeout", TimeSpan.FromSeconds(1));
        Diagnostics.Arrange("second family delay", TimeSpan.FromMilliseconds(200));
        var events = new ConnectAttemptTraceEvents(inner, "localhost", false, false, tracesTimer: true, tracesTimerExpiry: true, TimeSpan.FromSeconds(1));

        events.ConnectStarting();
        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.SecondFamilyDue();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[]
        {
            "[TIMER] [CONNECTTIMEOUT] set for 1000000ns",
            "  Trying [::1]:48763...",
            "[TIMER] [HAPPY_EYEBALLS] set for 200000ns",
            "[TIMER] [HAPPY_EYEBALLS] expires in 200000ns",
            "[TIMER] [CONNECTTIMEOUT] expires in 1000000ns",
            "[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms",
            "  Trying 127.0.0.1:48763...",
            "[TIMER] [CONNECTTIMEOUT] expires in 800000ns",
            "[TIMER] [CONNECTTIMEOUT] gives multi timeout in 800ms",
            "[TIMER] [HAPPY_EYEBALLS] cleared",
        };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void MaxTimeOnAPlainConnect_WritesTheTimeoutsSetAndGivesLines()
    {
        // curl -s -v --trace-config timer -m 5 http://127.0.0.1:P/ (BL-1258 Notes).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "127.0.0.1");
        Diagnostics.Arrange("transfer timeout", TimeSpan.FromSeconds(5));
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", false, false, tracesTimer: true, transferTimeout: TimeSpan.FromSeconds(5));

        events.ConnectStarting();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[]
        {
            "[TIMER] [TIMEOUT] set for 5000000ns",
            "  Trying 127.0.0.1:48763...",
            "[TIMER] [TIMEOUT] gives multi timeout in 5000ms",
            "[TIMER] [HAPPY_EYEBALLS] cleared",
        };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    [DataRow(5, 1, "CONNECTTIMEOUT", 1000, "[TIMER] [CONNECTTIMEOUT] expires in 1000000ns", "[TIMER] [TIMEOUT] expires in 5000000ns")]
    [DataRow(1, 5, "TIMEOUT", 1000, "[TIMER] [TIMEOUT] expires in 1000000ns", "[TIMER] [CONNECTTIMEOUT] expires in 5000000ns")]
    public void MaxTimeAndAConnectTimeout_SetBothAndTheNearestGivesTheMultiTimeout(
        int maxTimeSeconds, int connectTimeoutSeconds, string nearest, int nearestMilliseconds, string firstExpiry, string secondExpiry)
    {
        // curl -s -v --trace-config network -m 5 --connect-timeout 1 (and -m 1 --connect-timeout 5) http://127.0.0.1:P/ (BL-1258 Notes).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("max time seconds", maxTimeSeconds);
        Diagnostics.Arrange("connect timeout seconds", connectTimeoutSeconds);
        Diagnostics.Arrange("nearest timer", nearest);
        Diagnostics.Arrange("nearest milliseconds", nearestMilliseconds);
        var events = new ConnectAttemptTraceEvents(
            inner, "127.0.0.1", false, false, tracesTimer: true, tracesTimerExpiry: true,
            TimeSpan.FromSeconds(connectTimeoutSeconds), TimeSpan.FromSeconds(maxTimeSeconds));

        events.ConnectStarting();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[]
        {
            $"[TIMER] [TIMEOUT] set for {maxTimeSeconds * 1000000}ns",
            $"[TIMER] [CONNECTTIMEOUT] set for {connectTimeoutSeconds * 1000000}ns",
            "  Trying 127.0.0.1:48763...",
            firstExpiry,
            secondExpiry,
            $"[TIMER] [{nearest}] gives multi timeout in {nearestMilliseconds}ms",
        };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void MaxTimeAcrossTwoFamilies_GivesTheTimeoutLessTheSecondFamilysDelay()
    {
        // curl -s -v --trace-config timer -m 5 http://localhost:P/, IPv4 winning: 4791ms measured,
        // 4800ms from the configured delays (BL-1258 Notes, ADR-0357).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "localhost");
        Diagnostics.Arrange("transfer timeout", TimeSpan.FromSeconds(5));
        Diagnostics.Arrange("second family delay", TimeSpan.FromMilliseconds(200));
        var events = new ConnectAttemptTraceEvents(inner, "localhost", false, false, tracesTimer: true, transferTimeout: TimeSpan.FromSeconds(5));

        events.ConnectStarting();
        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.SecondFamilyDue();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[]
        {
            "[TIMER] [TIMEOUT] set for 5000000ns",
            "  Trying [::1]:48763...",
            "[TIMER] [HAPPY_EYEBALLS] set for 200000ns",
            "[TIMER] [HAPPY_EYEBALLS] gives multi timeout in 200ms",
            "  Trying 127.0.0.1:48763...",
            "[TIMER] [TIMEOUT] gives multi timeout in 4800ms",
            "[TIMER] [HAPPY_EYEBALLS] cleared",
        };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void AConnectTimeoutShorterThanTheSecondFamilysDelay_GivesTheMultiTimeoutRoundedUp()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("connect timeout microseconds", 100500);
        Diagnostics.Arrange("second family delay", TimeSpan.FromMilliseconds(200));
        var events = new ConnectAttemptTraceEvents(inner, "localhost", false, false, tracesTimer: true, tracesTimerExpiry: false, TimeSpan.FromMicroseconds(100500));

        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        Diagnostics.Act("last call", inner.Calls[^1]);

        Diagnostics.Assert("last call", "[TIMER] [CONNECTTIMEOUT] gives multi timeout in 101ms", inner.Calls[^1]);
        Assert.AreEqual("[TIMER] [CONNECTTIMEOUT] gives multi timeout in 101ms", inner.Calls[^1]);
    }

    [TestMethod]
    public void ASecondFamilyStartedAfterTheFirstFailed_WritesNoFurtherConnectTimeoutLine()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "localhost");
        Diagnostics.Arrange("connect timeout", TimeSpan.FromSeconds(1));
        var events = new ConnectAttemptTraceEvents(inner, "localhost", false, false, tracesTimer: true, tracesTimerExpiry: true, TimeSpan.FromSeconds(1));

        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.AttemptFailing(IPv6);
        events.AttemptFailed(IPv6);
        var linesBefore = inner.Calls.Count;
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        var actual = inner.Calls.Skip(linesBefore).ToArray();
        Diagnostics.Act("lines before the second family", linesBefore);
        Diagnostics.Act("lines after", Lines(actual));

        var expected = new[] { "  Trying 127.0.0.1:48763..." };
        Diagnostics.Diff("lines after", Lines(expected), Lines(actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(false, null)]
    [DataRow(true, null)]
    [DataRow(false, 1000)]
    public void WithoutAConnectTimeoutOrTheTimer_ConnectStartingWritesNothing(bool tracesTimer, int? connectTimeoutMilliseconds)
    {
        // curl -s -v --trace-config timer http://127.0.0.1:P/ writes no CONNECTTIMEOUT line (BL-1210 Notes).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("traces timer", tracesTimer);
        Diagnostics.Arrange("connect timeout milliseconds", connectTimeoutMilliseconds);
        TimeSpan? connectTimeout = connectTimeoutMilliseconds is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", false, false, tracesTimer, tracesTimerExpiry: true, connectTimeout);

        events.ConnectStarting();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[] { "  Trying 127.0.0.1:48763..." };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "h");
        Diagnostics.Arrange("endpoint", IPv4);
        var events = new ConnectAttemptTraceEvents(inner, "h", tracesHappyEyeballs: true, tracesTcp: true);

        events.ReportInfo("Host h:80 was resolved.");
        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = IPv4, LocalEndPoint = IPv4, ConnectionNumber = 0 });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 0 });
        events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
        });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0303, ContentType = default, Bytes = new byte[] { 2 }, Sent = false });
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true });
        events.ReportCertificateVerifyResult(18, isProxy: true);
        events.ReportTlsEarlyData(-36);
        events.ReportRequestHeader([3]);
        events.ReportResponseHeader([4]);
        events.ReportDataSent([5]);
        events.ReportDataReceived([6]);
        Diagnostics.Act("calls", Lines(inner.Calls));

        var expected = new[] { "Host h:80 was resolved.", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" };
        Diagnostics.Diff("calls", Lines(expected), Lines(inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }
}
