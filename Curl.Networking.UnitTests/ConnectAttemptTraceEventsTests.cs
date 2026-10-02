using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void OneAttemptThatConnects_WritesCurlsLinesForAPlainTransfer()
    {
        // curl -s -v --trace-config happy-eyeballs,tcp http://127.0.0.1:48761/
        var inner = new CountingTransferEvents();
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", tracesHappyEyeballs: true, tracesTcp: true);

        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);

        CollectionAssert.AreEqual(
            new[]
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
            },
            inner.Calls);
    }

    [TestMethod]
    public void TwoFamiliesRacing_WritesCurlsLinesForLocalhost()
    {
        // curl -s -v --trace-config happy-eyeballs,tcp http://localhost:48763/, IPv4 winning.
        var inner = new CountingTransferEvents();
        var events = new ConnectAttemptTraceEvents(inner, "localhost", tracesHappyEyeballs: true, tracesTcp: true);

        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.SecondFamilyDue();
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv4);

        CollectionAssert.AreEqual(
            new[]
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
            },
            inner.Calls);
    }

    [TestMethod]
    public void ARefusedAttempt_WritesCurlsLinesAroundTheFailure()
    {
        // curl -s -v --trace-config happy-eyeballs,tcp http://127.0.0.1:1/, one poll round kept.
        var inner = new CountingTransferEvents();
        var events = new ConnectAttemptTraceEvents(inner, "127.0.0.1", tracesHappyEyeballs: true, tracesTcp: true);
        var refused = new IPEndPoint(IPAddress.Loopback, 1);

        events.ReportInfo("  Trying 127.0.0.1:1...");
        events.AttemptFailing(refused);
        events.ReportInfo("connect to 127.0.0.1 port 1 from 0.0.0.0 port 0 failed: Connection refused");
        events.AttemptFailed(refused);
        events.NoMoreAttempts();

        CollectionAssert.AreEqual(
            new[]
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
            },
            inner.Calls.Skip(5).ToArray());
    }

    [TestMethod]
    [DataRow(true, false, "[HAPPY-EYEBALLS]")]
    [DataRow(false, true, "[TCP]")]
    public void OneComponentAlone_WritesOnlyItsOwnLines(bool tracesHappyEyeballs, bool tracesTcp, string prefix)
    {
        var inner = new CountingTransferEvents();
        var events = new ConnectAttemptTraceEvents(inner, "localhost", tracesHappyEyeballs, tracesTcp);

        events.RaceStarting(TimeSpan.FromMilliseconds(200));
        events.ReportInfo("  Trying [::1]:48763...");
        events.ReportInfo("  Trying 127.0.0.1:48763...");
        events.AttemptConnected(IPv6);

        Assert.AreEqual(tracesHappyEyeballs, events.TracesHappyEyeballs);
        Assert.IsTrue(inner.Calls.Where(line => !line.StartsWith("  Trying ", StringComparison.Ordinal)).All(line => line.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.IsGreaterThan(2, inner.Calls.Count);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
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

        CollectionAssert.AreEqual(
            new[] { "Host h:80 was resolved.", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
    }
}
