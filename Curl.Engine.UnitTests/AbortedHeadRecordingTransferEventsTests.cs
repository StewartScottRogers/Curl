using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="AbortedHeadRecordingTransferEvents" />: a transfer that ends with exit 28 and no report gets one with
/// the response code, header size, request size and connection count libcurl still reports (BL-3321), and every event
/// goes on unchanged.
/// </summary>
[TestClass]
public sealed class AbortedHeadRecordingTransferEventsTests
{
    private static readonly ConnectionOpenedEvent Opened = new() { HostName = "h", RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 80), LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50000), ConnectionNumber = 1 };

    [TestMethod]
    public void ReportOn_TooSlowAfterTheHead_ReportsWhatWasRecorded()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportConnectionOpened(Opened);
        events.ReportRequestHeader(new byte[62]);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n"));
        events.ReportResponseHeader(new byte[59]);

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0, "slow"))!.Report!;

        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual(76, report.HeaderSize);
        Assert.AreEqual(62, report.RequestSize);
        Assert.AreEqual(1, report.ConnectionCount);
    }

    [TestMethod]
    public void ReportOn_NothingRecorded_ReturnsTheResultUnchanged()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        TransferResult result = new(CurlExitCode.OperationTimedOut, 0);

        Assert.AreSame(result, events.ReportOn(result));
    }

    [TestMethod]
    public void ReportOn_HeaderBytesThatAreNotAStatusLine_KeepTheCode()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\n"));
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("Content-Length: 5\r\n"));
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1\r\n"));

        Assert.AreEqual(404, events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!.ResponseCode);
    }

    [TestMethod]
    public void ReportOn_NotANumberAfterTheVersion_KeepsZero()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1 abc\r\n"));

        Assert.AreEqual(0, events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!.ResponseCode);
    }

    [TestMethod]
    public void ReportOn_OtherResults_AreReturnedUnchanged()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        TransferResult succeeded = new(CurlExitCode.Ok, 0);
        TransferResult withReport = new TransferResult(CurlExitCode.OperationTimedOut, 0) { Report = new TransferReport() };

        Assert.AreSame(succeeded, events.ReportOn(succeeded));
        Assert.AreSame(withReport, events.ReportOn(withReport));
        Assert.IsNull(events.ReportOn(null));
    }

    [TestMethod]
    public void EveryEvent_GoesOnToTheInnerEvents()
    {
        CountingEvents inner = new();
        AbortedHeadRecordingTransferEvents events = new(inner);

        events.ReportConnectionOpened(Opened);
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
