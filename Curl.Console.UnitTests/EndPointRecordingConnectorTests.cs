using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

[TestClass]
public sealed class EndPointRecordingConnectorTests
{
    private static readonly ConnectTarget Target = new("h", 21, false);

    [TestMethod]
    public async Task ConnectAsync_Connected_RecordsTheLocalAndRemoteEndPoints()
    {
        IPEndPoint local = new(IPAddress.Loopback, 64513);
        IPEndPoint remote = new(IPAddress.Loopback, 21);
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new EndPointScriptedConnector(new EndPointScriptedConnector.Script(local, remote)), recorder);

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.IsNotNull(connect.Connection);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;
        Assert.AreEqual(local, report.LocalEndPoint);
        Assert.AreEqual(remote, report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_Failed_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new RecordingConnector(CurlExitCode.CouldntConnect, "Failed to connect"), recorder);
        TransferResult result = TransferResult.Success(0);

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, connect.ExitCode);
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_Connected_RecordsTheLocalAndRemoteEndPoints()
    {
        IPEndPoint local = new(IPAddress.Loopback, 64514);
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        ScriptedMultiplexedConnection quic = new(new ScriptedMultiplexedStream(0, [])) { LocalEndPoint = local, RemoteEndPoint = remote };
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ScriptedQuicConnector(MultiplexedConnectResult.Connected(quic, null), new RecordingConnector(CurlExitCode.CouldntConnect, "unused")), recorder);

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);

        Assert.AreSame(quic, connect.Connection);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;
        Assert.AreEqual(local, report.LocalEndPoint);
        Assert.AreEqual(remote, report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_Failed_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(
            new ScriptedQuicConnector(MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC failed"), new RecordingConnector(CurlExitCode.CouldntConnect, "unused")),
            recorder);
        TransferResult result = TransferResult.Success(0);

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, connect.ExitCode);
        Assert.AreSame(result, recorder.ReportOn(result));
    }
}
