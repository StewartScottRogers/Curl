using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// <see cref="HttpsConnectFilterTraceEvents" /> writes curl 8.21.0's <c>[HTTPS-CONNECT]</c> filter
/// lines around the connect lines it passes on (measured, BL-1192 Notes).
/// </summary>
[TestClass]
public sealed class HttpsConnectFilterTraceEventsTests
{
    private const string Connecting = "[HTTPS-CONNECT] connect -> 0, done=0";
    private const string Pollset = "[HTTPS-CONNECT] adjust_pollset -> 0, 1 socks";

    private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 443);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportInfo_TheFirstTryingLine_IsPrecededByTheInitLinesAndFollowedByAPollRound()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h1");
        ArrangeFilter("h1", null, "ReportInfo \"  Trying [::1]:443...\", then \"  Trying 127.0.0.1:443...\"");

        events.ReportInfo("  Trying [::1]:443...");
        events.ReportInfo("  Trying 127.0.0.1:443...");

        var expected = new[]
        {
            "[HTTPS-CONNECT] connect, init",
            "[HTTPS-CONNECT] 1st attempt uses h1 from wanted versions",
            "  Trying [::1]:443...",
            Connecting,
            Pollset,
            "  Trying 127.0.0.1:443...",
            Connecting,
            Pollset,
        };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_TheSetupFiltersHappyEyeballingLine_IsPrecededByTheInitLinesOnce()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h2");
        ArrangeFilter("h2", null, "ReportInfo \"[SETUP] happy eyeballing to origin h:443\", then \"  Trying 127.0.0.1:443...\"");

        events.ReportInfo("[SETUP] happy eyeballing to origin h:443");
        events.ReportInfo("  Trying 127.0.0.1:443...");

        var expected = new[]
        {
            "[HTTPS-CONNECT] connect, init",
            "[HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
            "[SETUP] happy eyeballing to origin h:443",
            "  Trying 127.0.0.1:443...",
            Connecting,
            Pollset,
        };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportTlsHandshake_Finished_IsPrecededByTwoPollRounds()
    {
        var inner = new CountingTransferEvents();
        ArrangeFilter("h2", null, "ReportTlsHandshake, finished");

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportTlsHandshake(Handshake(failed: false));

        var expected = new[] { Connecting, Pollset, Connecting, Pollset, "handshake" };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportTlsHandshake_Failed_IsPrecededByOnePollRound()
    {
        var inner = new CountingTransferEvents();
        ArrangeFilter("h2", null, "ReportTlsHandshake, failed");

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportTlsHandshake(Handshake(failed: true));

        var expected = new[] { Connecting, Pollset, "handshake" };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportConnectionOpened_IsBracketedByDoneAndTheFiltersRemoval()
    {
        var inner = new CountingTransferEvents();
        ArrangeFilter("h2", null, "ReportConnectionOpened to h");

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });

        var expected = new[] { "[HTTPS-CONNECT] connect -> 0, done=1", "opened", "[HTTPS-CONNECT] removing connected setup filter", "[HTTPS-CONNECT] destroy" };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportConnectFailed_OnceConnecting_WritesAllAttemptsFailedAndTheExitCode()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h2");
        events.ReportInfo("  Trying 127.0.0.1:443...");
        inner.Calls.Clear();
        ArrangeFilter("h2", null, "after a Trying line, ReportConnectFailed PeerFailedVerification (60)");

        events.ReportConnectFailed(CurlExitCode.PeerFailedVerification);

        var expected = new[] { "[HTTPS-CONNECT] connect, all attempts failed", "[HTTPS-CONNECT] connect -> 60, done=0" };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_WithASecondAttemptVersion_NamesTheSecondAttemptAfterTheFirst()
    {
        // curl 8.22.0 --http3 (BL-1284 Notes).
        var inner = new CountingTransferEvents();
        ArrangeFilter("h3", "h2", "ReportInfo \"  Trying 127.0.0.1:443...\"");

        new HttpsConnectFilterTraceEvents(inner, "h3", "h2").ReportInfo("  Trying 127.0.0.1:443...");

        var expected = new[]
        {
            "[HTTPS-CONNECT] connect, init",
            "[HTTPS-CONNECT] 1st attempt uses h3 from wanted versions",
            "[HTTPS-CONNECT] 2nd attempt uses h2 from wanted versions",
            "  Trying 127.0.0.1:443...",
            Connecting,
            Pollset,
        };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportTlsHandshake_AFinishedQuicHandshake_IsPrecededByOnePollRound()
    {
        var inner = new CountingTransferEvents();
        ArrangeFilter("h3", null, "ReportTlsHandshake, a finished QUIC handshake");

        new HttpsConnectFilterTraceEvents(inner, "h3").ReportTlsHandshake(Handshake(failed: false) with { IsQuic = true });

        var expected = new[] { Connecting, Pollset, "handshake" };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ContinueAfterFirstAttempt_AfterAFailedQuicAttempt_StartsH2AndReportsTheQuicFailureAsTheConnects()
    {
        // curl 8.22.0 --http3, QUIC refused, both attempts failed: connect -> 56 (BL-1284 Notes).
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h3");
        ArrangeFilter("h3", null, "ContinueAfterFirstAttempt h3 -> h2 after RecvError at 200 ms, a Trying line, ReportConnectFailed CouldntConnect");

        events.ContinueAfterFirstAttempt("h3", "h2", CurlExitCode.RecvError, TimeSpan.FromMilliseconds(200), tracesSetup: true);
        events.ReportInfo("  Trying 127.0.0.1:443...");
        events.ReportConnectFailed(CurlExitCode.CouldntConnect);

        var expected = new[]
        {
            "[HTTPS-CONNECT] h3 baller failed, starting h2",
            "  Trying 127.0.0.1:443...",
            Connecting,
            Pollset,
            "[HTTPS-CONNECT] connect, all attempts failed",
            "[HTTPS-CONNECT] connect -> 56, done=0",
        };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ContinueAfterFirstAttempt_WhileTheQuicAttemptIsConnecting_PollsTwiceMoreAndCountsBothSockets()
    {
        // curl 8.22.0 --http3 against a silent QUIC peer (BL-1284 Notes).
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h3");
        ArrangeFilter("h3", null, "ContinueAfterFirstAttempt h3 -> h2 still connecting at 200 ms, a Trying line, ReportConnectionOpened");

        events.ContinueAfterFirstAttempt("h3", "h2", null, TimeSpan.FromMilliseconds(200), tracesSetup: true);
        events.ReportInfo("  Trying 127.0.0.1:443...");
        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });

        var expected = new[]
        {
            Connecting,
            Pollset,
            Connecting,
            Pollset,
            "[HTTPS-CONNECT] h3 inconclusive after 200, starting h2",
            "  Trying 127.0.0.1:443...",
            Connecting,
            "[HTTPS-CONNECT] adjust_pollset -> 0, 2 socks",
            "[HTTPS-CONNECT] connect -> 0, done=1",
            "opened",
            "[HTTPS-CONNECT] removing connected setup filter",
            "[HTTPS-CONNECT] destroy",
            "[SETUP] destroy",
        };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ContinueAfterFirstAttempt_WithoutTheSetupFilter_WritesNoSetupDestroyOnceConnected()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h3");
        ArrangeFilter("h3", null, "ContinueAfterFirstAttempt h3 -> h2 after RecvError, no setup filter, then ReportConnectionOpened");

        events.ContinueAfterFirstAttempt("h3", "h2", CurlExitCode.RecvError, TimeSpan.FromMilliseconds(200), tracesSetup: false);
        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });

        Diagnostics.Act("calls", string.Join(" | ", inner.Calls));
        Diagnostics.Assert("last call", "[HTTPS-CONNECT] destroy", inner.Calls[^1]);
        Assert.AreEqual("[HTTPS-CONNECT] destroy", inner.Calls[^1]);
    }

    [TestMethod]
    public void ContinueAfterFirstAttempt_WhileAPreferredTcpAttemptIsConnecting_CallsItInconclusiveAndStartsH3()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h1", "h3", firstAttemptIsPreferred: true);
        ArrangeFilter("h1 (preferred)", "h3", "ContinueAfterFirstAttempt h1 -> h3 still connecting at 200 ms");

        events.ContinueAfterFirstAttempt("h1", "h3", null, TimeSpan.FromMilliseconds(200), tracesSetup: true);

        Diagnostics.Act("calls", string.Join(" | ", inner.Calls));
        Diagnostics.Assert("last call", "[HTTPS-CONNECT] h1 inconclusive after 200, starting h3", inner.Calls[^1]);
        Assert.AreEqual("[HTTPS-CONNECT] h1 inconclusive after 200, starting h3", inner.Calls[^1]);
    }

    [TestMethod]
    public void ReportInfo_WithAPreferredFirstAttempt_NamesItFromPreferredVersionAndH3Second()
    {
        // curl 8.22.0 --http3 with an --alt-svc entry naming the origin with h2 (BL-1320 Notes).
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h2", "h3", firstAttemptIsPreferred: true);
        ArrangeFilter("h2 (preferred)", "h3", "ReportInfo \"  Trying 127.0.0.1:443...\"");

        events.ReportInfo("  Trying 127.0.0.1:443...");

        var expected = new[] { "[HTTPS-CONNECT] connect, init", "[HTTPS-CONNECT] 1st attempt uses h2 from preferred version", "[HTTPS-CONNECT] 2nd attempt uses h3 from wanted versions" };
        AssertCalls(expected, inner.Calls[..3]);
        CollectionAssert.AreEqual(expected, inner.Calls[..3]);
    }

    [TestMethod]
    public void ReportConnectFailed_BeforeConnecting_WritesNothing()
    {
        var inner = new CountingTransferEvents();
        ArrangeFilter("h2", null, "ReportConnectFailed CouldntResolveHost before any Trying line");

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportConnectFailed(CurlExitCode.CouldntResolveHost);

        AssertCalls([], inner.Calls);
        Assert.IsEmpty(inner.Calls);
    }

    [TestMethod]
    public void ReportRequestHeader_AProxysConnectRequest_IsFollowedByAPollRound()
    {
        var inner = new CountingTransferEvents();
        ArrangeFilter("h2", null, "ReportRequestHeader \"CONNECT h:443 HTTP/1.1\"");

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportRequestHeader("CONNECT h:443 HTTP/1.1\r\n\r\n"u8);

        var expected = new[] { "request", Connecting, Pollset };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_TheOpenedSocksConnectionLine_IsPrecededByTwoPollRounds()
    {
        var inner = new CountingTransferEvents();
        const string Opened = "Opened SOCKS connection from 127.0.0.1 port 5 to h port 443 (via 127.0.0.1 port 1080)";
        ArrangeFilter("h2", null, $"ReportInfo \"{Opened}\"");

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportInfo(Opened);

        var expected = new[] { Connecting, Pollset, Connecting, Pollset, Opened };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h2");
        ArrangeFilter("h2", null, "one of each other ITransferEvents report, in order");

        events.ReportInfo("Host h:443 was resolved.");
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "https", IsProxy = false, HostName = "h", Port = 443, ConnectionNumber = 0 });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0303, ContentType = default, Bytes = new byte[] { 2 }, Sent = false });
        events.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true });
        events.ReportCertificateVerifyResult(18, isProxy: true);
        events.ReportTlsEarlyData(-36);
        events.ReportRequestHeader([3]);
        events.ReportResponseHeader([4]);
        events.ReportDataSent([5]);
        events.ReportDataReceived([6]);

        var expected = new[] { "Host h:443 was resolved.", "reused", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", Connecting, Pollset, "response", "sent", "received" };
        AssertCalls(expected, inner.Calls);
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    private static TlsHandshakeEvent Handshake(bool failed) => new()
    {
        ProtocolVersion = SslProtocols.Tls12,
        CipherSuite = null,
        NegotiatedApplicationProtocol = null,
        OfferedApplicationProtocols = [],
        ServerCertificate = null,
        CertificateVerified = true,
        Failed = failed,
    };

    private void ArrangeFilter(string firstAttempt, string? secondAttempt, string reports)
    {
        Diagnostics.Arrange("first attempt", firstAttempt);
        Diagnostics.Arrange("second attempt", secondAttempt ?? "none");
        Diagnostics.Arrange("reports", reports);
    }

    private void AssertCalls(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        Diagnostics.Act("calls", string.Join(" | ", actual));
        Diagnostics.Assert("call count", expected.Count, actual.Count);
        Diagnostics.Diff("calls", string.Join("\n", expected), string.Join("\n", actual));
    }
}
