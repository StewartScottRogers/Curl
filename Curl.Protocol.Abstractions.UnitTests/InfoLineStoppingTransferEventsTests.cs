using System.Net;
using System.Security.Authentication;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="InfoLineStoppingTransferEvents" />: every event passed on, and no info line after
/// <see cref="InfoLineStoppingTransferEvents.StopInfoLines" />, as curl 8.21.0 writes no <c>[TCP]</c> line
/// for <c>QUIT</c> (measured, BL-1259 Notes).
/// </summary>
[TestClass]
public sealed class InfoLineStoppingTransferEventsTests
{
    private static readonly IPEndPoint EndPoint = new(IPAddress.Loopback, 21);

    [TestMethod]
    public void EveryReport_IsPassedOnToTheTransfersEvents()
    {
        var inner = new CallRecordingTransferEvents();
        var events = new InfoLineStoppingTransferEvents(inner);

        events.ReportInfo("info");
        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = EndPoint, LocalEndPoint = EndPoint, ConnectionNumber = 0 });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "ftp", IsProxy = false, HostName = "h", Port = 21, ConnectionNumber = 0 });
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
            new[] { "info", "opened", "reused", "handshake", "tls-data", "tls-message", "trust", "verify 18 True", "early-data -36", "request", "response", "sent", "received" },
            inner.Calls);
    }

    [TestMethod]
    public void ReportInfo_AfterStopInfoLines_IsDropped()
    {
        var inner = new CallRecordingTransferEvents();
        var events = new InfoLineStoppingTransferEvents(inner);

        events.ReportInfo("[TCP] send(len=16) -> 0, 16");
        events.StopInfoLines();
        events.ReportInfo("[TCP] send(len=6) -> 0, 6");

        CollectionAssert.AreEqual(new[] { "[TCP] send(len=16) -> 0, 16" }, inner.Calls);
    }

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
