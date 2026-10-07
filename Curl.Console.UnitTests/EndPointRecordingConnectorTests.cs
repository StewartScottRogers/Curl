using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

[TestClass]
public sealed class EndPointRecordingConnectorTests
{
    private static readonly ConnectTarget Target = new("h", 21, false);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_Connected_RecordsTheLocalAndRemoteEndPoints()
    {
        IPEndPoint local = new(IPAddress.Loopback, 64513);
        IPEndPoint remote = new(IPAddress.Loopback, 21);
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new EndPointScriptedConnector(new EndPointScriptedConnector.Script(local, remote)), recorder);
        Diagnostics.Arrange("target", $"{Target.Host}:{Target.Port}");
        Diagnostics.Arrange("scripted end points", $"{local} -> {remote}");

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);
        Diagnostics.Act("connected", connect.Connection is not null);

        Diagnostics.Assert("connected", true, connect.Connection is not null);
        Assert.IsNotNull(connect.Connection);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;
        Diagnostics.Act("reported end points", $"{report.LocalEndPoint} -> {report.RemoteEndPoint}");
        Diagnostics.Assert("reported end points", $"{local} -> {remote}", $"{report.LocalEndPoint} -> {report.RemoteEndPoint}");
        Assert.AreEqual(local, report.LocalEndPoint);
        Assert.AreEqual(remote, report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_Failed_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new RecordingConnector(CurlExitCode.CouldntConnect, "Failed to connect"), recorder);
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("target", $"{Target.Host}:{Target.Port}");
        Diagnostics.Arrange("inner connector fails with", CurlExitCode.CouldntConnect);

        ConnectResult connect = await connector.ConnectAsync(Target, CancellationToken.None);
        Diagnostics.Act("connect exit code", connect.ExitCode);

        Diagnostics.Assert("connect exit code", CurlExitCode.CouldntConnect, connect.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, connect.ExitCode);
        Diagnostics.Act("result returned unchanged", ReferenceEquals(result, recorder.ReportOn(result)));
        Diagnostics.Assert("result returned unchanged", true, ReferenceEquals(result, recorder.ReportOn(result)));
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
        Diagnostics.Arrange("target", $"{Target.Host}:{Target.Port}");
        Diagnostics.Arrange("scripted QUIC end points", $"{local} -> {remote}");

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);
        Diagnostics.Act("connection is the scripted one", ReferenceEquals(quic, connect.Connection));

        Diagnostics.Assert("connection is the scripted one", true, ReferenceEquals(quic, connect.Connection));
        Assert.AreSame(quic, connect.Connection);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;
        Diagnostics.Act("reported end points", $"{report.LocalEndPoint} -> {report.RemoteEndPoint}");
        Diagnostics.Assert("reported end points", $"{local} -> {remote}", $"{report.LocalEndPoint} -> {report.RemoteEndPoint}");
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
        Diagnostics.Arrange("target", $"{Target.Host}:{Target.Port}");
        Diagnostics.Arrange("QUIC connect fails with", CurlExitCode.RecvError);

        MultiplexedConnectResult connect = await connector.ConnectMultiplexedAsync(Target, CancellationToken.None);
        Diagnostics.Act("connect exit code", connect.ExitCode);

        Diagnostics.Assert("connect exit code", CurlExitCode.RecvError, connect.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, connect.ExitCode);
        Diagnostics.Act("result returned unchanged", ReferenceEquals(result, recorder.ReportOn(result)));
        Diagnostics.Assert("result returned unchanged", true, ReferenceEquals(result, recorder.ReportOn(result)));
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_Connected_RecordsTheLocalAndRemoteEndPoints()
    {
        IPEndPoint local = new(IPAddress.Loopback, 64514);
        IPEndPoint remote = new(IPAddress.Loopback, 443);
        ScriptedMultiplexedConnection quic = new(new ScriptedMultiplexedStream(0, [])) { LocalEndPoint = local };
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(new ScriptedQuicConnector(MultiplexedConnectResult.Connected(quic, null), new RecordingConnector(CurlExitCode.CouldntConnect, "unused")), recorder);
        SessionOverQuic session = new(remote);
        Diagnostics.Arrange("target", $"{Target.Host}:{Target.Port}");
        Diagnostics.Arrange("QUIC local end point", local);
        Diagnostics.Arrange("session remote end point", remote);

        ConnectResult connect = await connector.ConnectMultiplexedSessionAsync(Target, _ => session, CancellationToken.None);
        Diagnostics.Act("connection is the session", ReferenceEquals(session, connect.Connection));

        Diagnostics.Assert("connection is the session", true, ReferenceEquals(session, connect.Connection));
        Assert.AreSame(session, connect.Connection);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;
        Diagnostics.Act("reported end points", $"{report.LocalEndPoint} -> {report.RemoteEndPoint}");
        Diagnostics.Assert("reported end points", $"{local} -> {remote}", $"{report.LocalEndPoint} -> {report.RemoteEndPoint}");
        Assert.AreEqual(local, report.LocalEndPoint);
        Assert.AreEqual(remote, report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectMultiplexedSessionAsync_Failed_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingConnector connector = new(
            new ScriptedQuicConnector(MultiplexedConnectResult.Failed(CurlExitCode.RecvError, "QUIC failed"), new RecordingConnector(CurlExitCode.CouldntConnect, "unused")),
            recorder);
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("target", $"{Target.Host}:{Target.Port}");
        Diagnostics.Arrange("QUIC connect fails with", CurlExitCode.RecvError);

        ConnectResult connect = await connector.ConnectMultiplexedSessionAsync(Target, _ => throw new AssertFailedException("opened"), CancellationToken.None);
        Diagnostics.Act("connect exit code", connect.ExitCode);

        Diagnostics.Assert("connect exit code", CurlExitCode.RecvError, connect.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, connect.ExitCode);
        Diagnostics.Act("result returned unchanged", ReferenceEquals(result, recorder.ReportOn(result)));
        Diagnostics.Assert("result returned unchanged", true, ReferenceEquals(result, recorder.ReportOn(result)));
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    /// <summary>A session over a QUIC connection that only names the server's endpoint.</summary>
    private sealed class SessionOverQuic(EndPoint remoteEndPoint) : IConnection
    {
        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => remoteEndPoint;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
