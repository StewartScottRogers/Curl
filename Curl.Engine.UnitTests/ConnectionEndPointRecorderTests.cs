using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

[TestClass]
public sealed class ConnectionEndPointRecorderTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 64513);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 47515);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ReportOn_NothingRecorded_ReturnsTheResultUnchanged()
    {
        ConnectionEndPointRecorder recorder = new();
        TransferResult result = TransferResult.Success(5);
        Diagnostics.Arrange("recorded end points", "none");

        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("same result returned", ReferenceEquals(result, reported));
        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, reported));
        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_NoRemoteRecorded_ReturnsTheResultUnchanged()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, null);
        TransferResult result = TransferResult.Success(5);
        Diagnostics.Arrange("recorded end points", $"local {Local}, no remote");

        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("same result returned", ReferenceEquals(result, reported));
        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, reported));
        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_ResultWithoutReport_GivesItAReportWithTheEndPointsAndTheBytesAsDownloadSize()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        Diagnostics.Arrange("recorded end points", $"local {Local}, remote {Remote}");

        TransferResult reported = recorder.ReportOn(TransferResult.Success(5));

        Diagnostics.Act("report", reported.Report);
        Diagnostics.Assert(
            "report",
            new TransferReport { DownloadSize = 5, LocalEndPoint = Local, RemoteEndPoint = Remote },
            reported.Report);
        Assert.AreEqual(new TransferReport { DownloadSize = 5, LocalEndPoint = Local, RemoteEndPoint = Remote }, reported.Report);
    }

    [TestMethod]
    public void ReportOn_ReportWithoutEndPoints_KeepsTheReportAndAddsThem()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { ResponseCode = 226 } };
        Diagnostics.Arrange("recorded end points", $"local {Local}, remote {Remote}");

        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("report", reported.Report);
        Diagnostics.Assert(
            "report",
            new TransferReport { ResponseCode = 226, LocalEndPoint = Local, RemoteEndPoint = Remote },
            reported.Report);
        Assert.AreEqual(new TransferReport { ResponseCode = 226, LocalEndPoint = Local, RemoteEndPoint = Remote }, reported.Report);
    }

    [TestMethod]
    public void ReportOn_ReportWithARemoteEndPoint_KeepsTheHandlersOwn()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { RemoteEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 80) } };
        Diagnostics.Arrange("recorded end points", $"local {Local}, remote {Remote}");

        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("same result returned", ReferenceEquals(result, reported));
        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, reported));
        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_ReportWithALocalEndPoint_KeepsTheHandlersOwn()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { LocalEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 80) } };
        Diagnostics.Arrange("recorded end points", $"local {Local}, remote {Remote}");

        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("same result returned", ReferenceEquals(result, reported));
        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, reported));
        Assert.AreSame(result, reported);
    }

    [TestMethod]
    public void ReportOn_ReportWithAUnixSocketRemoteIp_KeepsTheHandlersOwn()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(5) with { Report = new TransferReport { UnixSocketRemoteIp = "/tmp/other.sock" } };
        Diagnostics.Arrange("recorded end points", $"local {Local}, remote {Remote}");

        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("same result returned", ReferenceEquals(result, reported));
        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, reported));
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
        Diagnostics.Arrange("unix socket path", "/tmp/a-socket-path-long-enough-to-be-cut-by-curl/bl793.sock");

        TransferResult reported = recorder.ReportOn(TransferResult.Success(2));

        Diagnostics.Act("report", reported.Report);
        Diagnostics.Assert(
            "report",
            new TransferReport { DownloadSize = 2, UnixSocketRemoteIp = "/tmp/a-socket-path-long-enough-to-be-cut-by-c" },
            reported.Report);
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
        Diagnostics.Arrange("unix socket path", "/tmp/b793.sock");

        string? remoteIp = recorder.ReportOn(TransferResult.Success(0)).Report!.UnixSocketRemoteIp;

        Diagnostics.Act("remote ip", remoteIp);
        Diagnostics.Assert("remote ip", "/tmp/b793.sock", remoteIp);
        Assert.AreEqual("/tmp/b793.sock", recorder.ReportOn(TransferResult.Success(0)).Report!.UnixSocketRemoteIp);
    }

    [TestMethod]
    public void Record_AbstractUnixSocket_ReportsAnEmptyRemoteIp()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(null, new UnixDomainSocketEndPoint("\0curl-abstract"));
        Diagnostics.Arrange("unix socket path", "abstract socket curl-abstract");

        string? remoteIp = recorder.ReportOn(TransferResult.Success(0)).Report!.UnixSocketRemoteIp;

        Diagnostics.Act("remote ip length", remoteIp?.Length);
        Diagnostics.Assert("remote ip", string.Empty, remoteIp);
        Assert.AreEqual(string.Empty, recorder.ReportOn(TransferResult.Success(0)).Report!.UnixSocketRemoteIp);
    }

    [TestMethod]
    public void Record_NeitherAnIPNorAUnixSocketEndPoint_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, new DnsEndPoint("example.com", 80));
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("recorded end points", $"local {Local}, remote DNS example.com:80");

        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("same result returned", ReferenceEquals(result, reported));
        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, reported));
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public void Record_ConnectionAfterAUnixSocketOne_KeepsTheFirst()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(null, new UnixDomainSocketEndPoint("/tmp/b793.sock"));
        recorder.Record(Local, Remote);
        Diagnostics.Arrange("recorded connections", $"unix socket /tmp/b793.sock, then local {Local} remote {Remote}");

        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Diagnostics.Act("report", report);
        Diagnostics.Assert("unix socket remote ip", "/tmp/b793.sock", report.UnixSocketRemoteIp);
        Diagnostics.Assert("remote end point", null, report.RemoteEndPoint);
        Diagnostics.Assert("local end point", null, report.LocalEndPoint);
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
        Diagnostics.Arrange("recorded connections", $"first local {Local} remote {Remote}, then local 127.0.0.1:64514 remote 127.0.0.1:64512");

        TransferResult reported = recorder.ReportOn(TransferResult.Success(0));

        Diagnostics.Act("report", reported.Report);
        Diagnostics.Assert("local end point", Local, reported.Report!.LocalEndPoint);
        Diagnostics.Assert("remote end point", Remote, reported.Report.RemoteEndPoint);
        Assert.AreEqual(Local, reported.Report!.LocalEndPoint);
        Assert.AreEqual(Remote, reported.Report.RemoteEndPoint);
    }

    [TestMethod]
    public void Clear_AfterARecord_ForgetsIt()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("recorded end points", $"local {Local}, remote {Remote}");

        recorder.Clear();
        TransferResult reported = recorder.ReportOn(result);

        Diagnostics.Act("same result returned", ReferenceEquals(result, reported));
        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, reported));
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public void ReportOn_FailedConnectRecordedWithoutAReport_BuildsOneCarryingTheOsErrno()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.RecordFailedConnect(SocketError.ConnectionRefused);
        Diagnostics.Arrange("failed connect", SocketError.ConnectionRefused);

        TransferReport report = recorder.ReportOn(TransferResult.Success(5)).Report!;

        Diagnostics.Assert("os errno", ConnectionEndPointRecorder.OsErrnoOf(SocketError.ConnectionRefused), report.OsErrno);
        Assert.AreEqual(ConnectionEndPointRecorder.OsErrnoOf(SocketError.ConnectionRefused), report.OsErrno);
        Assert.AreEqual(5, report.DownloadSize);
    }

    [TestMethod]
    public void ReportOn_FailedConnectThenConnection_ReportsTheOsErrnoAndTheEndPointsOnTheHandlersReport()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.RecordFailedConnect(SocketError.TimedOut);
        recorder.Record(Local, Remote);
        TransferResult result = TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 200 } };
        Diagnostics.Arrange("failed connect", SocketError.TimedOut);

        TransferReport report = recorder.ReportOn(result).Report!;

        Diagnostics.Assert("os errno", ConnectionEndPointRecorder.OsErrnoOf(SocketError.TimedOut), report.OsErrno);
        Assert.AreEqual(ConnectionEndPointRecorder.OsErrnoOf(SocketError.TimedOut), report.OsErrno);
        Assert.AreEqual(200, report.ResponseCode);
        Assert.AreEqual(Remote, report.RemoteEndPoint);
    }

    [TestMethod]
    public void ReportOn_FailedSocksHandshakeRecorded_ReportsItsProxyErrorAndNoOsErrno()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.RecordFailedConnect(SocketError.Success, CurlProxyCode.ReplyConnectionRefused);
        recorder.RecordFailedConnect(SocketError.Success, CurlProxyCode.Ok);
        Diagnostics.Arrange("failed connects", "a refused SOCKS5 request, then a failure with no proxy error");

        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Diagnostics.Assert("proxy error", CurlProxyCode.ReplyConnectionRefused, report.ProxyError);
        Assert.AreEqual(CurlProxyCode.ReplyConnectionRefused, report.ProxyError);
        Assert.AreEqual(0, report.OsErrno);
    }

    [TestMethod]
    public void Clear_AfterAFailedSocksHandshake_ForgetsTheProxyError()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.RecordFailedConnect(SocketError.Success, CurlProxyCode.Closed);
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("failed connect", CurlProxyCode.Closed);

        recorder.Clear();

        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, recorder.ReportOn(result)));
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public void RecordFailedConnect_Success_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("failed connect", SocketError.Success);

        recorder.RecordFailedConnect(SocketError.Success);

        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, recorder.ReportOn(result)));
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public void Clear_AfterAFailedConnect_ForgetsTheOsErrno()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.RecordFailedConnect(SocketError.ConnectionRefused);
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("failed connect", SocketError.ConnectionRefused);

        recorder.Clear();

        Diagnostics.Assert("same result returned", true, ReferenceEquals(result, recorder.ReportOn(result)));
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void OsErrnoOf_ConnectionRefusedOnWindows_IsWsaeConnRefused()
    {
        // The Schannel build's CURLINFO_OS_ERRNO is the Winsock code: WSAECONNREFUSED 10061.
        Diagnostics.Arrange("socket error", SocketError.ConnectionRefused);

        long errno = ConnectionEndPointRecorder.OsErrnoOf(SocketError.ConnectionRefused);

        Diagnostics.Assert("os errno", 10061L, errno);
        Assert.AreEqual(10061L, errno);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void OsErrnoOf_ConnectionRefusedOnLinux_IsEconnRefused()
    {
        // The OpenSSL build's CURLINFO_OS_ERRNO is errno: ECONNREFUSED is 111 on Linux.
        Diagnostics.Arrange("socket error", SocketError.ConnectionRefused);

        long errno = ConnectionEndPointRecorder.OsErrnoOf(SocketError.ConnectionRefused);

        Diagnostics.Assert("os errno", 111L, errno);
        Assert.AreEqual(111L, errno);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.OSX)]
    public void OsErrnoOf_ConnectionRefusedOnMacOS_IsEconnRefused()
    {
        // The OpenSSL build's CURLINFO_OS_ERRNO is errno: ECONNREFUSED is 61 on macOS.
        Diagnostics.Arrange("socket error", SocketError.ConnectionRefused);

        long errno = ConnectionEndPointRecorder.OsErrnoOf(SocketError.ConnectionRefused);

        Diagnostics.Assert("os errno", 61L, errno);
        Assert.AreEqual(61L, errno);
    }

    [TestMethod]
    public void ReportOn_RedirectHopFailsBeforeConnecting_KeepsTheEarlierHopsStatusAndEndPoints()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        recorder.ReportOn(TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 302 } });
        Diagnostics.Arrange("first hop", "302 from " + Remote);

        recorder.Clear(followingRedirect: true);
        TransferReport report = recorder.ReportOn(TransferResult.Failure(CurlExitCode.CouldntConnect, "refused")).Report!;

        Diagnostics.Assert("response code", 302, report.ResponseCode);
        Assert.AreEqual(302, report.ResponseCode);
        Assert.AreEqual(Remote, report.RemoteEndPoint);
        Assert.AreEqual(Local, report.LocalEndPoint);
    }

    [TestMethod]
    public void ReportOn_NewTransferFailsBeforeConnecting_KeepsNothingOfTheEarlierOne()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        recorder.ReportOn(TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 302 } });

        recorder.Clear();
        TransferResult failed = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");

        Assert.AreSame(failed, recorder.ReportOn(failed));
    }
}
