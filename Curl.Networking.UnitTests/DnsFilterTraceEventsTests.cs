using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// <see cref="DnsFilterTraceEvents" /> writes curl 8.21.0's <c>[DNS]</c> filter lines around the
/// connect lines it passes on (measured, BL-1102 Notes).
/// </summary>
[TestClass]
public sealed class DnsFilterTraceEventsTests
{
    private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 80);

    [TestMethod]
    public void Start_WritesTheFilterCreationLinesForTheHostAndPort()
    {
        var inner = new CountingTransferEvents();

        DnsFilterTraceEvents.Start(inner, "127.0.0.1", 47110);

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for 127.0.0.1:47110, transport=3, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start host 127.0.0.1:47110",
            },
            inner.Calls);
    }

    [TestMethod]
    [DataRow(AddressFamily.InterNetwork, 1)]
    [DataRow(AddressFamily.InterNetworkV6, 2)]
    public void Start_UnderOneFamily_CountsOnlyItsQuery(AddressFamily family, int queries)
    {
        // curl -s -v -4 --trace-config dns http://nonexistent.invalid:47114/ -> queries=1; -6 -> queries=2.
        var inner = new CountingTransferEvents();

        DnsFilterTraceEvents.Start(inner, "h", 1, family);

        Assert.AreEqual($"[DNS] created DNS filter for h:1, transport=3, queries={queries}", inner.Calls[0]);
    }

    [TestMethod]
    [DataRow(AddressFamily.Unspecified, "A+AAAA")]
    [DataRow(AddressFamily.InterNetwork, "A")]
    [DataRow(AddressFamily.InterNetworkV6, "AAAA")]
    public void ReportInfo_TheCouldNotResolveLine_IsBracketedByTheNegativeCacheEntryAndTheExit6(AddressFamily family, string types)
    {
        // curl 8.21.0 -s -v --trace-config dns http://nonexistent.invalid:47114/ (BL-1157 Notes).
        var inner = new CountingTransferEvents();
        var events = DnsFilterTraceEvents.Start(inner, "nonexistent.invalid", 47114, family);
        inner.Calls.Clear();

        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine("nonexistent.invalid", 47114));

        CollectionAssert.AreEqual(
            new[]
            {
                $"[DNS] cache negative name resolve for nonexistent.invalid:47114 type={types}",
                "Could not resolve: nonexistent.invalid:47114",
                "[DNS] error resolving: 6",
                "[DNS] Curl_conn_connect(block=0) -> 6, done=0",
                "[DNS] Curl_conn_connect(), filter returned 6",
                "[DNS] [1] shutdown async",
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_ATryingLine_IsFollowedByTheUnfinishedConnectLine()
    {
        var inner = new CountingTransferEvents();
        var events = new DnsFilterTraceEvents(inner);

        events.ReportInfo("  Trying 127.0.0.1:47110...");

        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:47110...", "[DNS] Curl_conn_connect(block=0) -> 0, done=0" }, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_TheFailedToConnectLine_IsFollowedByTheFiltersExit7()
    {
        var inner = new CountingTransferEvents();
        var events = new DnsFilterTraceEvents(inner);

        events.ReportInfo("Failed to connect to 127.0.0.1:47199 after 2026 ms: Could not connect to server");

        CollectionAssert.AreEqual(
            new[]
            {
                "Failed to connect to 127.0.0.1:47199 after 2026 ms: Could not connect to server",
                "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "[DNS] Curl_conn_connect(), filter returned 7",
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_AnyOtherLine_IsPassedOnAlone()
    {
        var inner = new CountingTransferEvents();

        new DnsFilterTraceEvents(inner).ReportInfo("Host localhost:80 was resolved.");

        CollectionAssert.AreEqual(new[] { "Host localhost:80 was resolved." }, inner.Calls);
    }

    [TestMethod]
    public void ReportConnectionOpened_IsBracketedByTheChainsConnectedLines()
    {
        var inner = new CountingTransferEvents();

        new DnsFilterTraceEvents(inner).ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });

        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] connected filter chain below",
                "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
                "opened",
                "[DNS] removing connected setup filter",
                "[DNS] destroy",
            },
            inner.Calls);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
        var events = new DnsFilterTraceEvents(inner);

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
            new[] { "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
    }
}
