using System.Net;
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
