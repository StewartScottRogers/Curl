using System.Net;
using System.Security.Authentication;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="HandshakeCapturingTransferEvents" /> passes every event on unchanged and keeps
/// the last handshake (BL-920).
/// </summary>
[TestClass]
public sealed class HandshakeCapturingTransferEventsTests
{
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

        CollectionAssert.AreEqual(
            new[] { "info", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
        Assert.AreSame(handshake, capturing.Handshake);
    }

    [TestMethod]
    public void Handshake_BeforeAnyIsReported_IsNull()
    {
        var capturing = new HandshakeCapturingTransferEvents(new CountingTransferEvents());

        Assert.IsNull(capturing.Handshake);
    }

    [TestMethod]
    public void Route_OfEachProvider_IsTheOneTlsClientRoutingNames()
    {
        IHandshakeReportingTlsProvider sslStream = new SslStreamTlsProvider(new TlsClientOptions());
        IHandshakeReportingTlsProvider handBuilt = new HandBuiltTlsProvider(new TlsClientOptions(), TimeProvider.System);

        Assert.AreEqual(TlsClientRoute.SslStream, sslStream.Route);
        Assert.AreEqual(TlsClientRoute.HandBuilt, handBuilt.Route);
    }

    private sealed class CountingTransferEvents : ITransferEvents
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
