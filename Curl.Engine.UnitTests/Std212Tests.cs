using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins STD-212's fix (BL-5893): the status of a proxy's reply to the CONNECT that opened a tunnel reaches
/// <c>%{http_connect}</c> for every scheme through <see cref="EndPointRecordingConnector" /> and
/// <see cref="ConnectionEndPointRecorder" />, not only for HTTP, and a transfer that times out after the tunnel opened
/// does not report the CONNECT reply's status as <c>%{response_code}</c> (<see cref="AbortedHeadRecordingTransferEvents" />).
/// Measured on curl 8.22.0 Schannel: an FTP download through <c>-x ... -p</c> prints <c>200|226|2</c> for
/// <c>%{http_connect}|%{response_code}|%{num_connects}</c>, and a DICT one that times out <c>200|000|000</c> for
/// <c>%{http_connect}|%{response_code}|%{http_code}</c>.
/// </summary>
[TestClass]
public sealed class Std212Tests
{
    private static readonly ConnectTarget Target = new("h", 21, false);

    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 50000);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 21);

    [TestMethod]
    public async Task ReportOn_TunnelledFtpTransfer_ReportsTheConnectStatusBesideTheFtpResponseCode()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ProxyConnectReplyConnector(200, 0), recorder);
        await connector.ConnectAsync(Target, CancellationToken.None);
        await connector.ConnectAsync(Target, CancellationToken.None);

        TransferReport report = recorder.ReportOn(TransferResult.Success(10) with { Report = new TransferReport { ResponseCode = 226 } }).Report!;

        Assert.AreEqual(200, report.ProxyConnectResponseCode);
        Assert.AreEqual(226, report.ResponseCode);
    }

    [TestMethod]
    public async Task ReportOn_TunnelledTransferWithoutReport_BuildsOneWithTheConnectStatus()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ProxyConnectReplyConnector(200), recorder);
        await connector.ConnectAsync(Target, CancellationToken.None);

        TransferReport report = recorder.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0, "timed out")).Report!;

        Assert.AreEqual(200, report.ProxyConnectResponseCode);
        Assert.AreEqual(0, report.ResponseCode);
    }

    [TestMethod]
    public async Task ReportOn_HandlerReportedItsOwnConnectStatus_KeepsIt()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ProxyConnectReplyConnector(200), recorder);
        await connector.ConnectAsync(Target, CancellationToken.None);

        TransferReport report = recorder.ReportOn(TransferResult.Success(0) with { Report = new TransferReport { ProxyConnectResponseCode = 407 } }).Report!;

        Assert.AreEqual(407, report.ProxyConnectResponseCode);
    }

    [TestMethod]
    public async Task ReportOn_RefusedTunnel_ReportsTheRefusingStatus()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ProxyConnectReplyConnector(-407), recorder);
        await connector.ConnectAsync(Target, CancellationToken.None);

        TransferReport report = recorder.ReportOn(new TransferResult(CurlExitCode.CouldntConnect, 0, "refused")).Report!;

        Assert.AreEqual(407, report.ProxyConnectResponseCode);
    }

    [TestMethod]
    public async Task ReportOn_TunnelledMultiplexedSession_ReportsTheConnectStatus()
    {
        ConnectionEndPointRecorder recorder = new();
        IConnector connector = new EndPointRecordingConnector(new ProxyConnectReplyConnector(200), recorder);
        await connector.ConnectMultiplexedSessionAsync(Target, _ => throw new InvalidOperationException("no QUIC"), CancellationToken.None);

        Assert.AreEqual(200, recorder.ReportOn(TransferResult.Success(0)).Report!.ProxyConnectResponseCode);
    }

    [TestMethod]
    public async Task Clear_AfterATunnel_ForgetsTheConnectStatus()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ProxyConnectReplyConnector(200), recorder);
        await connector.ConnectAsync(Target, CancellationToken.None);

        recorder.Clear();

        TransferResult result = new(CurlExitCode.OperationTimedOut, 0, "timed out");
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public void ReportOn_TimedOutAfterTheConnectReply_ReportsNoResponseCode()
    {
        AbortedHeadRecordingTransferEvents events = new(NoTransferEvents.Instance);
        byte[] statusLine = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n");
        events.ReportResponseHeader(statusLine);
        events.ReportResponseHeader("\r\n"u8);
        events.ReportConnectionOpened(new ConnectionOpenedEvent { HostName = "h", RemoteEndPoint = Remote, LocalEndPoint = Local, ConnectionNumber = 1 });

        TransferReport report = events.ReportOn(new TransferResult(CurlExitCode.OperationTimedOut, 0, "timed out"))!.Report!;

        Assert.AreEqual(0, report.ResponseCode);
        Assert.IsNull(report.HttpVersion);
        Assert.AreEqual(statusLine.Length + 2, report.HeaderSize);
    }

    /// <summary>
    /// Answers each connect as a proxy tunnel would: connected with the CONNECT reply's status, or, for a negative
    /// status, failed with the status the proxy refused the tunnel with.
    /// </summary>
    /// <param name="statuses">Each connect's CONNECT reply status, in order; the last repeats.</param>
    private sealed class ProxyConnectReplyConnector(params int[] statuses) : IConnector
    {
        private int connects;

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
        {
            int status = statuses[Math.Min(connects++, statuses.Length - 1)];
            return ValueTask.FromResult(status < 0
                ? ConnectResult.Failed(CurlExitCode.CouldntConnect, "refused", null, proxyConnectResponseCode: -status)
                : ConnectResult.Connected(new EndPointOnlyConnection(), null, Local, proxyConnectResponseCode: status));
        }

        public ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(
            ConnectTarget target,
            Func<IMultiplexedConnection, IConnection> openSession,
            CancellationToken cancellationToken) => ConnectAsync(target, cancellationToken);
    }

    /// <summary>A connection that has a remote end point and carries nothing.</summary>
    private sealed class EndPointOnlyConnection : IConnection
    {
        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => Remote;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => ValueTask.FromResult(0);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
