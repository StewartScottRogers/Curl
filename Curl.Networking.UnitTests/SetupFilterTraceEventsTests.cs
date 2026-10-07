using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// <see cref="SetupFilterTraceEvents" /> writes curl 8.21.0's <c>[SETUP]</c> filter lines around the
/// connect lines it passes on (measured, BL-1103 Notes).
/// </summary>
[TestClass]
public sealed class SetupFilterTraceEventsTests
{
    private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 80);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportInfo_TheFirstTryingLine_IsPrecededByTheHappyEyeballingLine()
    {
        var inner = new CountingTransferEvents();
        var events = new SetupFilterTraceEvents(inner, "localhost", 47400);

        Diagnostics.Arrange("origin", "localhost:47400");

        events.ReportInfo("Host localhost:47400 was resolved.");
        events.ReportInfo("  Trying [::1]:47400...");
        events.ReportInfo("  Trying 127.0.0.1:47400...");

        Diagnostics.Act("calls passed on", string.Join(" | ", inner.Calls));
        Diagnostics.Assert("second call", "[SETUP] happy eyeballing to origin localhost:47400", inner.Calls.ElementAtOrDefault(1));

        CollectionAssert.AreEqual(
            new[]
            {
                "Host localhost:47400 was resolved.",
                "[SETUP] happy eyeballing to origin localhost:47400",
                "  Trying [::1]:47400...",
                "  Trying 127.0.0.1:47400...",
            },
            inner.Calls);
    }

    [TestMethod]
    public void ReportConnectionOpened_IsFollowedByTheFiltersRemoval()
    {
        var inner = new CountingTransferEvents();

        Diagnostics.Arrange("origin, end point", $"h:80, {EndPoint}");

        new SetupFilterTraceEvents(inner, "h", 80).ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });

        Diagnostics.Act("calls passed on", string.Join(" | ", inner.Calls));
        Diagnostics.Assert("calls passed on", "opened | [SETUP] removing connected setup filter | [SETUP] destroy", string.Join(" | ", inner.Calls));

        CollectionAssert.AreEqual(new[] { "opened", "[SETUP] removing connected setup filter", "[SETUP] destroy" }, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
        var events = new SetupFilterTraceEvents(inner, "h", 80);

        Diagnostics.Arrange("origin", "h:80");
        Diagnostics.Arrange("reports", 11);

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

        Diagnostics.Act("calls passed on", string.Join(" | ", inner.Calls));
        Diagnostics.Assert("calls passed on", 11, inner.Calls.Count);

        CollectionAssert.AreEqual(
            new[] { "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
    }
}
