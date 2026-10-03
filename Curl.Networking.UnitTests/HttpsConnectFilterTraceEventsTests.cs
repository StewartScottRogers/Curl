using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void ReportInfo_TheFirstTryingLine_IsPrecededByTheInitLinesAndFollowedByAPollRound()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h1");

        events.ReportInfo("  Trying [::1]:443...");
        events.ReportInfo("  Trying 127.0.0.1:443...");

        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] connect, init",
                "[HTTPS-CONNECT] 1st attempt uses h1 from wanted versions",
                "  Trying [::1]:443...",
                Connecting,
                Pollset,
                "  Trying 127.0.0.1:443...",
                Connecting,
                Pollset,
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_TheSetupFiltersHappyEyeballingLine_IsPrecededByTheInitLinesOnce()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h2");

        events.ReportInfo("[SETUP] happy eyeballing to origin h:443");
        events.ReportInfo("  Trying 127.0.0.1:443...");

        CollectionAssert.AreEqual(
            new[]
            {
                "[HTTPS-CONNECT] connect, init",
                "[HTTPS-CONNECT] 1st attempt uses h2 from wanted versions",
                "[SETUP] happy eyeballing to origin h:443",
                "  Trying 127.0.0.1:443...",
                Connecting,
                Pollset,
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportTlsHandshake_Finished_IsPrecededByTwoPollRounds()
    {
        var inner = new CountingTransferEvents();

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportTlsHandshake(Handshake(failed: false));

        CollectionAssert.AreEqual(new[] { Connecting, Pollset, Connecting, Pollset, "handshake" }, inner.Calls);
    }

    [TestMethod]
    public void ReportTlsHandshake_Failed_IsPrecededByOnePollRound()
    {
        var inner = new CountingTransferEvents();

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportTlsHandshake(Handshake(failed: true));

        CollectionAssert.AreEqual(new[] { Connecting, Pollset, "handshake" }, inner.Calls);
    }

    [TestMethod]
    public void ReportConnectionOpened_IsBracketedByDoneAndTheFiltersRemoval()
    {
        var inner = new CountingTransferEvents();

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });

        CollectionAssert.AreEqual(
            new[] { "[HTTPS-CONNECT] connect -> 0, done=1", "opened", "[HTTPS-CONNECT] removing connected setup filter", "[HTTPS-CONNECT] destroy" },
            inner.Calls);
    }

    [TestMethod]
    public void ReportConnectFailed_OnceConnecting_WritesAllAttemptsFailedAndTheExitCode()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h2");
        events.ReportInfo("  Trying 127.0.0.1:443...");
        inner.Calls.Clear();

        events.ReportConnectFailed(CurlExitCode.PeerFailedVerification);

        CollectionAssert.AreEqual(new[] { "[HTTPS-CONNECT] connect, all attempts failed", "[HTTPS-CONNECT] connect -> 60, done=0" }, inner.Calls);
    }

    [TestMethod]
    public void ReportConnectFailed_BeforeConnecting_WritesNothing()
    {
        var inner = new CountingTransferEvents();

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportConnectFailed(CurlExitCode.CouldntResolveHost);

        Assert.IsEmpty(inner.Calls);
    }

    [TestMethod]
    public void ReportRequestHeader_AProxysConnectRequest_IsFollowedByAPollRound()
    {
        var inner = new CountingTransferEvents();

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportRequestHeader("CONNECT h:443 HTTP/1.1\r\n\r\n"u8);

        CollectionAssert.AreEqual(new[] { "request", Connecting, Pollset }, inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_TheOpenedSocksConnectionLine_IsPrecededByTwoPollRounds()
    {
        var inner = new CountingTransferEvents();
        const string Opened = "Opened SOCKS connection from 127.0.0.1 port 5 to h port 443 (via 127.0.0.1 port 1080)";

        new HttpsConnectFilterTraceEvents(inner, "h2").ReportInfo(Opened);

        CollectionAssert.AreEqual(new[] { Connecting, Pollset, Connecting, Pollset, Opened }, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
        var events = new HttpsConnectFilterTraceEvents(inner, "h2");

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

        CollectionAssert.AreEqual(
            new[] { "Host h:443 was resolved.", "reused", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", Connecting, Pollset, "response", "sent", "received" },
            inner.Calls);
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
}
