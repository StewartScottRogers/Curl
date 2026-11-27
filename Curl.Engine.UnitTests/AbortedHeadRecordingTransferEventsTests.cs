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
        events.ReportRequestHeader(Encoding.ASCII.GetBytes("GET /x HTTP/1.1\r\nHost: h\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n"));
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n"));
        events.ReportResponseHeader(new byte[59]);

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0, "slow"))!.Report!;

        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual(76, report.HeaderSize);
        Assert.AreEqual(66, report.RequestSize);
        Assert.AreEqual(1, report.ConnectionCount);
    }

    [TestMethod]
    public void ReportOn_ReplyLinesThatStartNoResponseHead_CountNoHeaderSize()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n"));
        events.ReportConnectionOpened(Opened);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("220 hi: there\r\n"));

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!;

        Assert.AreEqual(37, report.HeaderSize);
    }

    [TestMethod]
    [DataRow("USER anonymous\r\n")]
    [DataRow("RETR f.txt\r\n")]
    [DataRow("SIZE f.txt")]
    public void ReportOn_AnFtpCommand_CountsNoRequestSize(string command)
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportConnectionOpened(Opened);
        events.ReportRequestHeader(Encoding.ASCII.GetBytes(command));

        Assert.AreEqual(0, events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!.RequestSize);
    }

    [TestMethod]
    [DataRow("GET / HTTP/1.1\r\nHost: h\r\n\r\n")]
    [DataRow("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n")]
    [DataRow("GET / HTTP/2")]
    public void ReportOn_AnHttpOrRtspRequestHead_CountsItsBytes(string head)
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportRequestHeader(Encoding.ASCII.GetBytes(head));

        Assert.AreEqual(head.Length, events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!.RequestSize);
    }

    [TestMethod]
    public void ReportOn_AnRtspHead_CountsItsBytes()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("RTSP/1.0 200 OK\r\n"));
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("CSeq: 1\r\n"));

        Assert.AreEqual(26, events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!.HeaderSize);
    }

    [TestMethod]
    public void ReportOn_TooSlowAfterTheHead_ReportsTheFirstConnectionEndPointsAndTheBodyBytes()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportConnectionOpened(Opened);
        events.ReportConnectionOpened(Opened with { LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 50001), ConnectionNumber = 2 });
        events.ReportDataReceived(new byte[3]);
        events.ReportDataReceived(new byte[4]);

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!;

        Assert.AreEqual(7, report.DownloadSize);
        Assert.AreEqual(Opened.LocalEndPoint, report.LocalEndPoint);
        Assert.AreEqual(Opened.RemoteEndPoint, report.RemoteEndPoint);
    }

    [TestMethod]
    public void ReportOn_OnlyBodyBytes_StillGetsAReport()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportDataReceived(new byte[3]);

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0))!.Report!;

        Assert.AreEqual(3, report.DownloadSize);
        Assert.IsNull(report.LocalEndPoint);
    }

    [TestMethod]
    public void ReportOn_ReportAlreadyThere_KeepsItsTimingsAndTakesTheLargerCounts()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        events.ReportConnectionOpened(Opened);
        events.ReportResponseHeader(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n"));
        events.ReportDataReceived(new byte[51200]);
        TransferReport carried = new()
        {
            HttpVersion = new Version(2, 0),
            LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 1),
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, 2),
            ConnectionCount = 1,
            DownloadSize = 0,
        };

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0) { Report = carried })!.Report!;

        Assert.AreEqual(51200, report.DownloadSize);
        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual(17, report.HeaderSize);
        Assert.AreEqual(new Version(2, 0), report.HttpVersion);
        Assert.AreEqual(1, report.LocalEndPoint!.Port);
        Assert.AreEqual(2, report.RemoteEndPoint!.Port);
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
