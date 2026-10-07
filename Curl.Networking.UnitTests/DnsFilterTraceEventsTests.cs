using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Start_WritesTheFilterCreationLinesForTheHostAndPort()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host", "127.0.0.1");
        Diagnostics.Arrange("port", 47110);

        DnsFilterTraceEvents.Start(inner, "127.0.0.1", 47110);

        var expected = new[]
        {
            "[DNS] created DNS filter for 127.0.0.1:47110, transport=3, queries=3",
            "[DNS] added",
            "[DNS] cf_dns_start host 127.0.0.1:47110",
        };
        AssertLines(expected, inner);
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
        Diagnostics.Arrange("host and port", "h:1");
        Diagnostics.Arrange("family", family);

        DnsFilterTraceEvents.Start(inner, "h", 1, family);

        WriteLines(inner);
        Diagnostics.Diff("first line", $"[DNS] created DNS filter for h:1, transport=3, queries={queries}", inner.Calls[0]);
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
        Diagnostics.Arrange("host and port", "nonexistent.invalid:47114");
        Diagnostics.Arrange("family", family);

        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine("nonexistent.invalid", 47114));

        var expected = new[]
        {
            $"[DNS] cache negative name resolve for nonexistent.invalid:47114 type={types}",
            "Could not resolve: nonexistent.invalid:47114",
            "[DNS] error resolving: 6",
            "[DNS] Curl_conn_connect(block=0) -> 6, done=0",
            "[DNS] Curl_conn_connect(), filter returned 6",
            "[DNS] [1] shutdown async",
        };
        AssertLines(expected, inner);
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
        Diagnostics.Arrange("info line", "  Trying 127.0.0.1:47110...");

        events.ReportInfo("  Trying 127.0.0.1:47110...");

        AssertLines(new[] { "  Trying 127.0.0.1:47110...", "[DNS] Curl_conn_connect(block=0) -> 0, done=0" }, inner);
        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:47110...", "[DNS] Curl_conn_connect(block=0) -> 0, done=0" }, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_TheFailedToConnectLine_IsFollowedByTheFiltersExit7()
    {
        var inner = new CountingTransferEvents();
        var events = new DnsFilterTraceEvents(inner);
        Diagnostics.Arrange("info line", "Failed to connect to 127.0.0.1:47199 after 2026 ms: Could not connect to server");

        events.ReportInfo("Failed to connect to 127.0.0.1:47199 after 2026 ms: Could not connect to server");

        var expected = new[]
        {
            "Failed to connect to 127.0.0.1:47199 after 2026 ms: Could not connect to server",
            "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
            "[DNS] Curl_conn_connect(), filter returned 7",
        };
        AssertLines(expected, inner);
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
    public void StartOverUnixSocket_WritesTheUnixSocketFiltersCreation_AndNoProgressAfterTrying()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("socket path", "/run/app.sock");
        Diagnostics.Arrange("info line", "  Trying /run/app.sock:0...");

        var events = DnsFilterTraceEvents.StartOverUnixSocket(inner, "/run/app.sock");
        events.ReportInfo("  Trying /run/app.sock:0...");

        var expected = new[]
        {
            "[DNS] created DNS filter for /run/app.sock:0, transport=6, queries=3",
            "[DNS] added",
            "[DNS] cf_dns_start unix-domain-socket /run/app.sock:0",
            "  Trying /run/app.sock:0...",
        };
        AssertLines(expected, inner);
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] created DNS filter for /run/app.sock:0, transport=6, queries=3",
                "[DNS] added",
                "[DNS] cf_dns_start unix-domain-socket /run/app.sock:0",
                "  Trying /run/app.sock:0...",
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_ANegativeCacheEntry_WritesItsTypeAndEndsWithExit6AfterTheHostAlone()
    {
        var inner = new CountingTransferEvents();
        var events = new DnsFilterTraceEvents(inner, "AAAA", "foo", 47500);
        Diagnostics.Arrange("negative type", "AAAA");
        Diagnostics.Arrange("host and port", "foo:47500");

        events.ReportInfo("Negative DNS entry");
        events.ReportInfo("Could not resolve host: foo");
        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine("foo", 47500));
        events.ReportInfo(DnsFilterTraceEvents.CouldNotResolveLine("foo"));

        var expected = new[]
        {
            "[DNS] cache entry does not have type=AAAA addresses",
            "Negative DNS entry",
            "Could not resolve host: foo",
            "Could not resolve: foo:47500",
            "Could not resolve: foo",
            "[DNS] error resolving: 6",
            "[DNS] Curl_conn_connect(block=0) -> 6, done=0",
            "[DNS] Curl_conn_connect(), filter returned 6",
        };
        AssertLines(expected, inner);
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] cache entry does not have type=AAAA addresses",
                "Negative DNS entry",
                "Could not resolve host: foo",
                "Could not resolve: foo:47500",
                "Could not resolve: foo",
                "[DNS] error resolving: 6",
                "[DNS] Curl_conn_connect(block=0) -> 6, done=0",
                "[DNS] Curl_conn_connect(), filter returned 6",
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_ALookedUpNameThenARefusedDial_CompletesTheResolveAndShutsItDown()
    {
        var inner = new CountingTransferEvents();
        var events = new DnsFilterTraceEvents(inner, host: "example.test", port: 47113);
        Diagnostics.Arrange("host and port", "example.test:47113");

        events.ReportInfo("Host example.test:47113 was resolved.");
        events.ReportInfo("Failed to connect to example.test:47113 after 0 ms: Could not connect to server");

        var expected = new[]
        {
            "[DNS] resolve complete for example.test:47113",
            "Host example.test:47113 was resolved.",
            "Failed to connect to example.test:47113 after 0 ms: Could not connect to server",
            "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
            "[DNS] Curl_conn_connect(), filter returned 7",
            "[DNS] [1] shutdown async",
        };
        AssertLines(expected, inner);
        CollectionAssert.AreEqual(
            new[]
            {
                "[DNS] resolve complete for example.test:47113",
                "Host example.test:47113 was resolved.",
                "Failed to connect to example.test:47113 after 0 ms: Could not connect to server",
                "[DNS] Curl_conn_connect(block=0) -> 7, done=0",
                "[DNS] Curl_conn_connect(), filter returned 7",
                "[DNS] [1] shutdown async",
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_ANameFoundInTheDnsCache_WritesNoResolveCompletion()
    {
        var inner = new CountingTransferEvents();
        var events = new DnsFilterTraceEvents(inner, host: "foo", port: 80);
        Diagnostics.Arrange("host and port", "foo:80");

        events.ReportInfo("Hostname foo was found in DNS cache");
        events.ReportInfo("Host foo:80 was resolved.");

        AssertLines(new[] { "Hostname foo was found in DNS cache", "Host foo:80 was resolved." }, inner);
        CollectionAssert.AreEqual(new[] { "Hostname foo was found in DNS cache", "Host foo:80 was resolved." }, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_LocalhostResolved_WritesNoResolveCompletion()
    {
        // curl answers localhost itself (measured, BL-1181 Notes).
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host and port", "localhost:80");

        new DnsFilterTraceEvents(inner, host: "localhost", port: 80).ReportInfo("Host localhost:80 was resolved.");

        AssertLines(new[] { "Host localhost:80 was resolved." }, inner);
        CollectionAssert.AreEqual(new[] { "Host localhost:80 was resolved." }, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_AnyOtherLine_IsPassedOnAlone()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("host and port", "(none)");

        new DnsFilterTraceEvents(inner).ReportInfo("Host localhost:80 was resolved.");

        AssertLines(new[] { "Host localhost:80 was resolved." }, inner);
        CollectionAssert.AreEqual(new[] { "Host localhost:80 was resolved." }, inner.Calls);
    }

    [TestMethod]
    public void ReportConnectionOpened_IsBracketedByTheChainsConnectedLines()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("opened connection", $"h at {EndPoint}, number 0");

        new DnsFilterTraceEvents(inner).ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });

        var expected = new[]
        {
            "[DNS] connected filter chain below",
            "[DNS] Curl_conn_connect(block=0) -> 0, done=1",
            "opened",
            "[DNS] removing connected setup filter",
            "[DNS] destroy",
        };
        AssertLines(expected, inner);
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
        Diagnostics.Arrange("reports", "reused, handshake, TLS data, TLS message, trust, verify result, early data, request, response, sent, received");

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

        AssertLines(new[] { "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" }, inner);
        CollectionAssert.AreEqual(
            new[] { "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
    }

    private void WriteLines(CountingTransferEvents inner)
    {
        Diagnostics.Act("line count", inner.Calls.Count);
        for (var index = 0; index < inner.Calls.Count; index++)
        {
            Diagnostics.Act($"line {index}", inner.Calls[index]);
        }
    }

    private void AssertLines(string[] expected, CountingTransferEvents inner)
    {
        WriteLines(inner);
        Diagnostics.Diff("lines", string.Join('\n', expected), string.Join('\n', inner.Calls));
    }
}
