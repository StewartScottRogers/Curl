using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="HandshakeCapturingTransferEvents" /> passes every event on unchanged and keeps
/// the last handshake (BL-920).
/// </summary>
[TestClass]
public sealed class HandshakeCapturingTransferEventsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void EveryReport_IsPassedOnToTheInnerEvents()
    {
        var inner = new CountingTransferEvents();
        var capturing = new HandshakeCapturingTransferEvents(inner);
        var handshake = new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
        };
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        Diagnostics.Arrange("handshake", "TLS 1.2, verified, no cipher suite or ALPN");
        Diagnostics.Arrange("reports", "one of each of the 13 ITransferEvents reports, in order");

        capturing.ReportInfo("info");
        capturing.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = endPoint, LocalEndPoint = endPoint, ConnectionNumber = 0 });
        capturing.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 0 });
        capturing.ReportTlsHandshake(handshake);
        capturing.ReportTlsData([1], sent: true);
        capturing.ReportTlsMessage(new TlsMessageEvent { ProtocolVersion = 0x0303, ContentType = default, Bytes = new byte[] { 2 }, Sent = false });
        capturing.ReportTlsTrust(new TlsTrustEvent { VerifiesPeer = true });
        capturing.ReportCertificateVerifyResult(18, isProxy: true);
        capturing.ReportTlsEarlyData(-36);
        capturing.ReportRequestHeader([3]);
        capturing.ReportResponseHeader([4]);
        capturing.ReportDataSent([5]);
        capturing.ReportDataReceived([6]);

        Diagnostics.Act("inner calls", string.Join(" | ", inner.Calls));
        Diagnostics.Act("captured handshake is the reported one", ReferenceEquals(handshake, capturing.Handshake));
        Diagnostics.Assert(
            "inner calls",
            "info | opened | reused | handshake | tls-data | tls-message | trust | verify 18 True | early-data -36 | request | response | sent | received",
            string.Join(" | ", inner.Calls));
        Diagnostics.Assert("captured handshake is the reported one", true, ReferenceEquals(handshake, capturing.Handshake));
        CollectionAssert.AreEqual(
            new[] { "info", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
        Assert.AreSame(handshake, capturing.Handshake);
    }

    [TestMethod]
    public void Handshake_BeforeAnyIsReported_IsNull()
    {
        var capturing = new HandshakeCapturingTransferEvents(new CountingTransferEvents());
        Diagnostics.Arrange("reports", "none");

        var handshake = capturing.Handshake;

        Diagnostics.Act("handshake is null", handshake is null);
        Diagnostics.Assert("handshake is null", true, handshake is null);
        Assert.IsNull(capturing.Handshake);
    }

    [TestMethod]
    public void Route_OfEachProvider_IsTheOneTlsClientRoutingNames()
    {
        IHandshakeReportingTlsProvider sslStream = new SslStreamTlsProvider(new TlsClientOptions());
        IHandshakeReportingTlsProvider handBuilt = new HandBuiltTlsProvider(new TlsClientOptions(), TimeProvider.System);
        Diagnostics.Arrange("options", "default TlsClientOptions for both providers");

        Diagnostics.Act("SslStream provider route", sslStream.Route);
        Diagnostics.Act("hand-built provider route", handBuilt.Route);

        Diagnostics.Assert("SslStream provider route", TlsClientRoute.SslStream, sslStream.Route);
        Diagnostics.Assert("hand-built provider route", TlsClientRoute.HandBuilt, handBuilt.Route);
        Assert.AreEqual(TlsClientRoute.SslStream, sslStream.Route);
        Assert.AreEqual(TlsClientRoute.HandBuilt, handBuilt.Route);
    }

    [TestMethod]
    public void RouteReason_OfEachProvider_IsTheOneTlsClientRoutingGives()
    {
        IHandshakeReportingTlsProvider sslStream = new SslStreamTlsProvider(new TlsClientOptions());
        IHandshakeReportingTlsProvider handBuilt = new HandBuiltTlsProvider(new TlsClientOptions(MaximumVersion: TlsVersion.Tls11), TimeProvider.System);
        Diagnostics.Arrange("SslStream provider options", "default");
        Diagnostics.Arrange("hand-built provider options", "MaximumVersion: Tls11");

        Diagnostics.Act("SslStream provider route reason", sslStream.RouteReason ?? "null");
        Diagnostics.Act("hand-built provider route reason", handBuilt.RouteReason);

        Diagnostics.Assert("SslStream provider route reason", "null", sslStream.RouteReason ?? "null");
        Diagnostics.Assert("hand-built provider route reason", "--tls-max caps the versions below TLS 1.2", handBuilt.RouteReason);
        Assert.IsNull(sslStream.RouteReason);
        Assert.AreEqual("--tls-max caps the versions below TLS 1.2", handBuilt.RouteReason);
    }

    [TestMethod]
    public void RevocationCheckIncomplete_IsFalseUntilReportedAndPassesNothingOn()
    {
        var inner = new CountingTransferEvents();
        var capturing = new HandshakeCapturingTransferEvents(inner);
        Diagnostics.Arrange("reports", "ReportRevocationCheckIncomplete once");

        Diagnostics.Act("incomplete before the report", capturing.RevocationCheckIncomplete);
        Diagnostics.Assert("incomplete before the report", false, capturing.RevocationCheckIncomplete);
        Assert.IsFalse(capturing.RevocationCheckIncomplete);
        capturing.ReportRevocationCheckIncomplete();

        Diagnostics.Act("incomplete after the report", capturing.RevocationCheckIncomplete);
        Diagnostics.Act("inner calls", inner.Calls.Count);
        Diagnostics.Assert("incomplete after the report", true, capturing.RevocationCheckIncomplete);
        Diagnostics.Assert("inner calls", 0, inner.Calls.Count);
        Assert.IsTrue(capturing.RevocationCheckIncomplete);
        Assert.IsEmpty(inner.Calls);
    }

    internal sealed class CountingTransferEvents : ITransferEvents
    {
        public List<string> Calls { get; } = [];

        public void ReportInfo(string text) => Calls.Add(text);

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Calls.Add("opened");

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Calls.Add("reused");

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Calls.Add("handshake");

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Calls.Add("tls-data");

        public void ReportTlsMessage(TlsMessageEvent message) => Calls.Add("tls-message");

        public void ReportTlsTrust(TlsTrustEvent trust) => Calls.Add("trust");

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Calls.Add($"verify {verifyResult} {isProxy}");

        public void ReportTlsEarlyData(long bytes) => Calls.Add($"early-data {bytes}");

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Calls.Add("request");

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Calls.Add("response");

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Calls.Add("sent");

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Calls.Add("received");
    }
}
