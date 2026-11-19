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