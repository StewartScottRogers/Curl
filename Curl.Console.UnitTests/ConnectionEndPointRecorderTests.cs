using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

[TestClass]
public sealed class ConnectionEndPointRecorderTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 64513);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 47515);

    [TestMethod]
    public void ReportOn_NothingRecorded_ReturnsTheResultUnchanged()
    {
        ConnectionEndPointRecorder recorder = new();
        TransferResult result = TransferResult.Success(5);

        TransferResult reported = recorder.ReportOn(result);

        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_NoRemoteRecorded_ReturnsTheResultUnchanged()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, null);
        TransferResult result = TransferResult.Success(5);

        TransferResult reported = recorder.ReportOn(result);

        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_ResultWithoutReport_GivesItAReportWithTheEndPointsAndTheBytesAsDownloadSize()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);

        TransferResult reported = recorder.ReportOn(TransferResult.Success(5));

        Assert.AreEqual(new TransferReport { DownloadSize = 5, LocalEndPoint = Local, RemoteEndPoint = Remote }, reported.Report);
    }

    [TestMethod]
    public void ReportOn_ReportWithoutEndPoints_KeepsTheReportAndAddsThem()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { ResponseCode = 226 } };

        TransferResult reported = recorder.ReportOn(result);

        Assert.AreEqual(new TransferReport { ResponseCode = 226, LocalEndPoint = Local, RemoteEndPoint = Remote }, reported.Report);
    }

    [TestMethod]
    public void ReportOn_ReportWithARemoteEndPoint_KeepsTheHandlersOwn()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { RemoteEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 80) } };

        TransferResult reported = recorder.ReportOn(result);

        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_ReportWithALocalEndPoint_KeepsTheHandlersOwn()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { LocalEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 80) } };

        TransferResult reported = recorder.ReportOn(result);

        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_ReportWithAUnixSocketRemoteIp_KeepsTheHandlersOwn()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { UnixSocketRemoteIp = "/tmp/other.sock" } };

        TransferResult reported = recorder.ReportOn(result);

        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void Record_UnixSocketConnection_ReportsThePathCutTo45CharactersAndNoEndPoints()
    {
        // curl --unix-socket "C:\Users\Stewart Rogers\AppData\Local\Temp\bl793.sock" -w
        // "[%{remote_ip}|%{remote_port}|%{local_ip}|%{local_port}]" printed
        // "[C:\Users\Stewart Rogers\AppData\Local\Temp\bl|-1||-1]" (BL-793 Notes).
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(null, new UnixDomainSocketEndPoint("/tmp/a-socket-path-long-enough-to-be-cut-by-curl/bl793.sock"));

        TransferResult reported = recorder.ReportOn(TransferResult.Success(2));

        Assert.AreEqual(
            new TransferReport { DownloadSize = 2, UnixSocketRemoteIp = "/tmp/a-socket-path-long-enough-to-be-cut-by-c" },
            reported.Report);
    }

    [TestMethod]
    public void Record_ShortUnixSocketPath_ReportsItWhole()
    {
        // curl --unix-socket Z:\b793.sock -w "%{remote_ip}" printed Z:\b793.sock (BL-793 Notes).
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(null, new UnixDomainSocketEndPoint("/tmp/b793.sock"));

        Assert.AreEqual("/tmp/b793.sock", recorder.ReportOn(TransferResult.Success(0)).Report!.UnixSocketRemoteIp);
    }

    [TestMethod]
    public void Record_AbstractUnixSocket_ReportsAnEmptyRemoteIp()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(null, new UnixDomainSocketEndPoint("\0curl-abstract"));

        Assert.AreEqual(string.Empty, recorder.ReportOn(TransferResult.Success(0)).Report!.UnixSocketRemoteIp);
    }

    [TestMethod]
    public void Record_NeitherAnIPNorAUnixSocketEndPoint_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, new DnsEndPoint("example.com", 80));
        TransferResult result = TransferResult.Success(0);

        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public void Record_ConnectionAfterAUnixSocketOne_KeepsTheFirst()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(null, new UnixDomainSocketEndPoint("/tmp/b793.sock"));
        recorder.Record(Local, Remote);

        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Assert.AreEqual("/tmp/b793.sock", report.UnixSocketRemoteIp);
        Assert.IsNull(report.RemoteEndPoint);
        Assert.IsNull(report.LocalEndPoint);
    }

    [TestMethod]
    public void Record_SecondConnection_KeepsTheFirst()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        recorder.Record(new IPEndPoint(IPAddress.Loopback, 64514), new IPEndPoint(IPAddress.Loopback, 64512));

        TransferResult reported = recorder.ReportOn(TransferResult.Success(0));

        Assert.AreEqual(Local, reported.Report!.LocalEndPoint);
        Assert.AreEqual(Remote, reported.Report.RemoteEndPoint);
    }

    [TestMethod]
    public void Clear_AfterARecord_ForgetsIt()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(0);

        recorder.Clear();

        Assert.AreSame(result, recorder.ReportOn(result));
    }
}
