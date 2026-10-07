using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportInfo_ClosingTheConnectionAfterTheShutdownLine_IsFollowedOnceByTheDestroyLine()
    {
        var inner = new CountingTransferEvents();
        var events = new AsyncResolveTeardownTraceEvents(inner);
        Diagnostics.Arrange("shutdown line", AsyncResolveTeardownTraceEvents.ShutdownLine);
        Diagnostics.Arrange("reports", "shutdown, resolve failure, closing #0, closing #1");

        events.ReportInfo(AsyncResolveTeardownTraceEvents.ShutdownLine);
        events.ReportInfo("Could not resolve host: x");
        events.ReportInfo("closing connection #0");
        events.ReportInfo("closing connection #1");
        Diagnostics.Act("inner calls", string.Join(" | ", inner.Calls));

        var expected = new[] { "[DNS] [1] shutdown async", "Could not resolve host: x", "closing connection #0", "[DNS] [1] destroy async", "closing connection #1" };
        Diagnostics.Assert("inner calls", string.Join(" | ", expected), string.Join(" | ", inner.Calls));
        CollectionAssert.AreEqual(
            expected,
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_ClosingTheConnectionWithoutAShutdown_IsPassedOnAlone()
    {
        var inner = new CountingTransferEvents();
        Diagnostics.Arrange("report", "closing connection #0");

        new AsyncResolveTeardownTraceEvents(inner).ReportInfo("closing connection #0");
        Diagnostics.Act("inner calls", string.Join(" | ", inner.Calls));

        var expected = new[] { "closing connection #0" };
        Diagnostics.Assert("inner calls", string.Join(" | ", expected), string.Join(" | ", inner.Calls));
        CollectionAssert.AreEqual(expected, inner.Calls);
    }

    [TestMethod]
    public void EveryOtherReport_IsPassedOnUnchanged()
    {
        var inner = new CountingTransferEvents();
        var events = new AsyncResolveTeardownTraceEvents(inner);
        Diagnostics.Arrange("end point", EndPoint);

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
        Diagnostics.Act("inner calls", string.Join(" | ", inner.Calls));

        var expected = new[] { "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" };
        Diagnostics.Assert("inner calls", string.Join(" | ", expected), string.Join(" | ", inner.Calls));
        CollectionAssert.AreEqual(
            expected,
            inner.Calls);
    }
}
