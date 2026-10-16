using System.Net;
using System.Security.Authentication;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="TlsVerifyResultRecordingTransferEvents" />: the origin's and the HTTPS proxy's verify codes
/// are kept apart, <see cref="TlsVerifyResultRecordingTransferEvents.ReportOn" /> puts them on the finished
/// transfer's report (a new one when it has none) or leaves the result as it is when none was reported, and
/// every event goes on unchanged.
/// </summary>
[TestClass]
public sealed class TlsVerifyResultRecordingTransferEventsTests
{
    [TestMethod]
    public void ReportCertificateVerifyResult_OriginAndProxy_KeepsEachApart()
    {
        TlsVerifyResultRecordingTransferEvents events = new(NoTransferEvents.Instance);

        events.ReportCertificateVerifyResult(19, isProxy: false);
        events.ReportCertificateVerifyResult(20, isProxy: true);

        Assert.AreEqual(19, events.SslVerifyResult);
        Assert.AreEqual(20, events.ProxySslVerifyResult);
    }

    [TestMethod]
    public void ReportOn_NothingReported_ReturnsTheResultUnchanged()
    {
        TlsVerifyResultRecordingTransferEvents events = new(NoTransferEvents.Instance);
        TransferResult result = new(CurlExitCode.Ok, 0);

        TransferResult? reported = events.ReportOn(result);

        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_AbortedTransfer_AnswersNull()
    {
        TlsVerifyResultRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportCertificateVerifyResult(19, isProxy: false);

        TransferResult? reported = events.ReportOn(null);

        Assert.IsNull(reported);
    }

    [TestMethod]
    public void ReportOn_ResultWithReport_PutsTheCodesOnItKeepingTheRest()
    {
        TlsVerifyResultRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportCertificateVerifyResult(19, isProxy: false);

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport { ResponseCode = 200 } })!.Report!;

        Assert.AreEqual(19, report.SslVerifyResult);
        Assert.AreEqual(0, report.ProxySslVerifyResult);
        Assert.AreEqual(200, report.ResponseCode);
    }

    [TestMethod]
    public void ReportOn_ResultWithoutReport_PutsTheCodesOnANewOne()
    {
        TlsVerifyResultRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportCertificateVerifyResult(20, isProxy: true);

        TransferReport? report = events.ReportOn(new TransferResult(CurlExitCode.PeerFailedVerification, 0))!.Report;

        Assert.IsNotNull(report);
        Assert.AreEqual(20, report.ProxySslVerifyResult);
    }

    [TestMethod]
    public void EveryEvent_GoesOnToTheInnerEvents()
    {
        CountingEvents inner = new();
        TlsVerifyResultRecordingTransferEvents events = new(inner);

        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80), LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000), ConnectionNumber = 1 });
        events.ReportConnectionReused(new ConnectionReusedEvent { Scheme = "http", IsProxy = false, HostName = "h", Port = 80, ConnectionNumber = 1 });
        events.ReportCertificateVerifyResult(0, isProxy: false);
        events.ReportTlsEarlyData(0);
        events.ReportInfo("i");
        events.ReportTlsHandshake(new TlsHandshakeEvent { ProtocolVersion = SslProtocols.Tls13, CipherSuite = null, NegotiatedApplicationProtocol = null, OfferedApplicationProtocols = [], ServerCertificate = null, CertificateVerified = false });
        events.ReportTlsData([1], sent: true);
        events.ReportTlsMessage(null!);
        events.ReportTlsTrust(null!);
        events.ReportRequestHeader([1]);
        events.ReportResponseHeader([1]);
        events.ReportDataSent([1]);
        events.ReportDataReceived([1]);

        Assert.AreEqual(13, inner.Count);
    }

    /// <summary>Counts every event it is given.</summary>
    private sealed class CountingEvents : ITransferEvents
    {
        public int Count { get; private set; }

        public void ReportConnectionOpened(ConnectionOpenedEvent opened) => Count++;

        public void ReportConnectionReused(ConnectionReusedEvent reused) => Count++;

        public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => Count++;

        public void ReportTlsEarlyData(long bytes) => Count++;

        public void ReportInfo(string text) => Count++;

        public void ReportTlsHandshake(TlsHandshakeEvent handshake) => Count++;

        public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => Count++;

        public void ReportTlsMessage(TlsMessageEvent message) => Count++;

        public void ReportTlsTrust(TlsTrustEvent trust) => Count++;

        public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportDataSent(ReadOnlySpan<byte> bytes) => Count++;

        public void ReportDataReceived(ReadOnlySpan<byte> bytes) => Count++;
    }
}
