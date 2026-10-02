using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;

using CountingTransferEvents = Curl.Networking.HandshakeCapturingTransferEventsTests.CountingTransferEvents;

namespace Curl.Networking;

/// <summary>
/// <see cref="AsyncResolveTeardownTraceEvents" /> writes curl 8.21.0's <c>[DNS] [1] destroy async</c>
/// after the <c>closing connection #N</c> of a transfer whose resolve failed (measured, BL-1157 Notes).
/// </summary>
[TestClass]
public sealed class AsyncResolveTeardownTraceEventsTests
{
    private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 80);

    [TestMethod]
    public void ReportInfo_ClosingTheConnectionAfterTheShutdownLine_IsFollowedOnceByTheDestroyLine()
    {
        var inner = new CountingTransferEvents();
        var events = new AsyncResolveTeardownTraceEvents(inner);

        events.ReportInfo(AsyncResolveTeardownTraceEvents.ShutdownLine);
        events.ReportInfo("Could not resolve host: x");
        events.ReportInfo("closing connection #0");
        events.ReportInfo("closing connection #1");

        CollectionAssert.AreEqual(
            new[] { "[DNS] [1] shutdown async", "Could not resolve host: x", "closing connection #0", "[DNS] [1] destroy async", "closing connection #1" },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_ClosingTheConnectionWithoutAShutdown_IsPassedOnAlone()
    {
        var inner = new CountingTransferEvents();

        new AsyncResolveTeardownTraceEvents(inner).ReportInfo("closing connection #0");

        CollectionAssert.AreEqual(new[] { "closing connection #0" }, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
        var events = new AsyncResolveTeardownTraceEvents(inner);

        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });
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
            new[] { "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
    }
}
