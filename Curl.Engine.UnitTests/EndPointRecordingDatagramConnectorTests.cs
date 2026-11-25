using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Engine;

namespace Curl.Engine;

[TestClass]
public sealed class EndPointRecordingDatagramConnectorTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task OpenAsync_Opened_RecordsTheServerAndNoLocalEnd()
    {
        IPEndPoint server = new(IPAddress.Loopback, 69);
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingDatagramConnector connector = new(new ScriptedDatagramConnector(server, server), recorder);
        Diagnostics.Arrange("scripted server", server);
        Diagnostics.Arrange("open target", "h:69");

        DatagramOpenResult opened = await connector.OpenAsync("h", 69, CancellationToken.None);
        Diagnostics.Act("channel opened", opened.Channel is not null);

        Diagnostics.Assert("channel opened", true, opened.Channel is not null);
        Assert.IsNotNull(opened.Channel);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;
        Diagnostics.Act("reported local end point", report.LocalEndPoint?.ToString() ?? "null");
        Diagnostics.Act("reported remote end point", report.RemoteEndPoint?.ToString() ?? "null");
        Diagnostics.Assert("reported local end point", "null", report.LocalEndPoint?.ToString() ?? "null");
        Diagnostics.Assert("reported remote end point", server.ToString(), report.RemoteEndPoint?.ToString() ?? "null");
        Assert.IsNull(report.LocalEndPoint);
        Assert.AreEqual(server, report.RemoteEndPoint);
        Assert.IsNotNull(recorder.DatagramOpenedAt);
    }

    [TestMethod]
    public async Task OpenAsync_Failed_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingDatagramConnector connector = new(new RecordingDatagramConnector(CurlExitCode.CouldntResolveHost, "Could not resolve host: h"), recorder);
        TransferResult result = TransferResult.Success(0);
        Diagnostics.Arrange("inner connector fails with", CurlExitCode.CouldntResolveHost);
        Diagnostics.Arrange("open target", "h:69");

        DatagramOpenResult opened = await connector.OpenAsync("h", 69, CancellationToken.None);
        TransferResult reported = recorder.ReportOn(result);
        Diagnostics.Act("open exit code", opened.ExitCode);
        Diagnostics.Act("result returned unchanged", ReferenceEquals(result, reported));

        Diagnostics.Assert("open exit code", CurlExitCode.CouldntResolveHost, opened.ExitCode);
        Diagnostics.Assert("result returned unchanged", true, ReferenceEquals(result, reported));
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, opened.ExitCode);
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public async Task ReceiveAsync_ChannelMovedToTheNextAddress_ReportsTheEndPointsOfTheSecondAddress()
    {
        IPEndPoint refused = new(IPAddress.IPv6Loopback, 69);
        IPEndPoint second = new(IPAddress.Loopback, 69);
        IPEndPoint secondLocal = new(IPAddress.Loopback, 40001);
        SwitchingChannel channel = new(refused, second, secondLocal);
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingDatagramConnector connector = new(new SingleChannelConnector(channel), recorder);

        DatagramOpenResult opened = await connector.OpenAsync("localhost", 69, CancellationToken.None);
        await opened.Channel!.SendAsync(new byte[1], refused, CancellationToken.None);
        await opened.Channel.ReceiveAsync(new byte[16], CancellationToken.None);
        await opened.Channel.ReceiveAsync(new byte[16], CancellationToken.None);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Assert.AreEqual(second, report.RemoteEndPoint);
        Assert.AreEqual(secondLocal, recorder.DatagramLocalEndPoint);
        Assert.AreEqual(second, opened.Channel.ServerEndPoint);
        Assert.AreEqual(secondLocal, opened.Channel.LocalEndPoint);
        await opened.Channel.DisposeAsync();
        Assert.IsTrue(channel.Disposed);
    }

    [TestMethod]
    public void ReRecordDatagramChannel_NoChannelRecorded_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();

        recorder.ReRecordDatagramChannel(new IPEndPoint(IPAddress.Loopback, 69), null);

        Assert.IsNull(recorder.DatagramLocalEndPoint);
        TransferResult result = TransferResult.Success(0);
        Assert.AreSame(result, recorder.ReportOn(result));
    }

    [TestMethod]
    public void ReRecordDatagramChannel_NoIpServerEndPoint_KeepsTheRecordedOnes()
    {
        ConnectionEndPointRecorder recorder = new();
        IPEndPoint local = new(IPAddress.Loopback, 5);
        recorder.RecordDatagramChannel(local, 1);

        recorder.ReRecordDatagramChannel(null, null);

        Assert.AreEqual(local, recorder.DatagramLocalEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_TcpConnectionRecordedFirst_KeepsTheTcpPeer()
    {
        IPEndPoint tcpPeer = new(IPAddress.Loopback, 80);
        SwitchingChannel channel = new(new IPEndPoint(IPAddress.IPv6Loopback, 69), new IPEndPoint(IPAddress.Loopback, 69), new IPEndPoint(IPAddress.Loopback, 40001));
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(null, tcpPeer);
        EndPointRecordingDatagramConnector connector = new(new SingleChannelConnector(channel), recorder);

        DatagramOpenResult opened = await connector.OpenAsync("localhost", 69, CancellationToken.None);
        await opened.Channel!.ReceiveAsync(new byte[16], CancellationToken.None);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Assert.AreEqual(tcpPeer, report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ReceiveAsync_SecondChannelMovedAddress_KeepsTheFirstChannelsEndPoints()
    {
        IPEndPoint firstServer = new(IPAddress.Loopback, 69);
        IPEndPoint firstLocal = new(IPAddress.Loopback, 40000);
        SwitchingChannel first = new(firstServer, firstServer, firstLocal);
        SwitchingChannel second = new(new IPEndPoint(IPAddress.IPv6Loopback, 70), new IPEndPoint(IPAddress.Loopback, 70), new IPEndPoint(IPAddress.Loopback, 40001));
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingDatagramConnector connector = new(new QueueConnector(first, second), recorder);

        DatagramOpenResult one = await connector.OpenAsync("localhost", 69, CancellationToken.None);
        DatagramOpenResult two = await connector.OpenAsync("localhost", 70, CancellationToken.None);
        await two.Channel!.ReceiveAsync(new byte[16], CancellationToken.None);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Assert.IsNotNull(one.Channel);
        Assert.AreEqual(firstServer, report.RemoteEndPoint);
        Assert.IsNull(recorder.DatagramLocalEndPoint);
    }

    [TestMethod]
    public void RecordDatagramChannel_SecondChannel_IsNotRecorded()
    {
        ConnectionEndPointRecorder recorder = new();

        Assert.IsTrue(recorder.RecordDatagramChannel(null, 1));
        Assert.IsFalse(recorder.RecordDatagramChannel(new IPEndPoint(IPAddress.Loopback, 5), 2));
        Assert.IsNull(recorder.DatagramLocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectToServer_FirstChannel_ReachesTheInnerChannel()
    {
        IPEndPoint server = new(IPAddress.Loopback, 5683);
        SwitchingChannel inner = new(server, server, server);
        EndPointRecordingDatagramConnector connector = new(new SingleChannelConnector(inner), new ConnectionEndPointRecorder());

        DatagramOpenResult opened = await connector.OpenAsync("h", 5683, CancellationToken.None);
        opened.Channel!.ConnectToServer();

        Assert.IsTrue(inner.Connected);
    }

    [TestMethod]
    public async Task ReceiveAsync_ChannelMovesAgainAfterTheFirstReceive_KeepsTheEndPointsRecordedAtTheFirst()
    {
        IPEndPoint opening = new(IPAddress.IPv6Loopback, 69);
        IPEndPoint afterFirst = new(IPAddress.Loopback, 69);
        IPEndPoint afterSecond = new(IPAddress.Loopback, 70);
        MovingChannel channel = new(opening, afterFirst, afterSecond);
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingDatagramConnector connector = new(new SingleChannelConnector(channel), recorder);

        DatagramOpenResult opened = await connector.OpenAsync("localhost", 69, CancellationToken.None);
        await opened.Channel!.ReceiveAsync(new byte[16], CancellationToken.None);
        await opened.Channel.ReceiveAsync(new byte[16], CancellationToken.None);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;

        Assert.AreEqual(afterFirst, report.RemoteEndPoint);
        Assert.AreEqual(afterFirst, recorder.DatagramLocalEndPoint);
    }

    private sealed class MovingChannel(IPEndPoint opening, IPEndPoint afterFirst, IPEndPoint afterSecond) : IDatagramChannel
    {
        private IPEndPoint _server = opening;
        private IPEndPoint? _local;
        private int _receives;

        public EndPoint ServerEndPoint => _server;

        public EndPoint? LocalEndPoint => _local;

        public void ConnectToServer()
        {
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            _server = _receives++ == 0 ? afterFirst : afterSecond;
            _local = _server;

            return ValueTask.FromResult(new DatagramReceived(1, _server));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class QueueConnector(params IDatagramChannel[] channels) : IDatagramConnector
    {
        private int _next;

        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DatagramOpenResult.Opened(channels[_next++]));
    }

    private sealed class SingleChannelConnector(IDatagramChannel channel) : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DatagramOpenResult.Opened(channel));
    }

    private sealed class SwitchingChannel(IPEndPoint first, IPEndPoint second, IPEndPoint secondLocal) : IDatagramChannel
    {
        private IPEndPoint _server = first;
        private IPEndPoint? _local;

        public bool Disposed { get; private set; }

        public bool Connected { get; private set; }

        public void ConnectToServer() => Connected = true;

        public EndPoint ServerEndPoint => _server;

        public EndPoint? LocalEndPoint => _local;

        public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            _server = second;
            _local = secondLocal;

            return ValueTask.FromResult(new DatagramReceived(1, second));
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
