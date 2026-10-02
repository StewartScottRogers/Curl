using System.Net;
using System.Security.Authentication;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// <see cref="ConnectionOpenedCapturingTransferEvents" /> passes every event on unchanged and
/// keeps the last first-connection <see cref="ConnectionOpenedEvent" /> (BL-1058).
/// </summary>
[TestClass]
public sealed class ConnectionOpenedCapturingTransferEventsTests
{
    private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 25);

    [TestMethod]
    public void EveryReport_IsPassedOnToTheInnerEvents()
    {
        var inner = new CallRecordingTransferEvents();
        var capturing = new ConnectionOpenedCapturingTransferEvents(inner);

        capturing.ReportInfo("info");
        capturing.ReportConnectionOpened(Opened(isSecondConnection: false));
        capturing.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "smtp", IsProxy = false, HostName = "h", Port = 25, ConnectionNumber = 0 });
        capturing.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls12,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
        });
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
    }

    [TestMethod]
    public void Opened_BeforeAnyConnectionIsReported_IsNull()
    {
        var capturing = new ConnectionOpenedCapturingTransferEvents(new CallRecordingTransferEvents());

        Assert.IsNull(capturing.Opened);
    }

    [TestMethod]
    public void Opened_AfterAFirstAndASecondConnection_IsTheFirst()
    {
        var capturing = new ConnectionOpenedCapturingTransferEvents(new CallRecordingTransferEvents());
        ConnectionOpenedEvent first = Opened(isSecondConnection: false);

        capturing.ReportConnectionOpened(first);
        capturing.ReportConnectionOpened(Opened(isSecondConnection: true));

        Assert.AreSame(first, capturing.Opened);
    }

    private static ConnectionOpenedEvent Opened(bool isSecondConnection) =>
        new() { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0, IsSecondConnection = isSecondConnection };

    private sealed class CallRecordingTransferEvents : ITransferEvents
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
