using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

[TestClass]
public sealed class EndPointRecordingDatagramConnectorTests
{
    [TestMethod]
    public async Task OpenAsync_Opened_RecordsTheServerAndNoLocalEnd()
    {
        IPEndPoint server = new(IPAddress.Loopback, 69);
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingDatagramConnector connector = new(new ScriptedDatagramConnector(server, server), recorder);

        DatagramOpenResult opened = await connector.OpenAsync("h", 69, CancellationToken.None);

        Assert.IsNotNull(opened.Channel);
        TransferReport report = recorder.ReportOn(TransferResult.Success(0)).Report!;
        Assert.IsNull(report.LocalEndPoint);
        Assert.AreEqual(server, report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task OpenAsync_Failed_RecordsNothing()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointRecordingDatagramConnector connector = new(new RecordingDatagramConnector(CurlExitCode.CouldntResolveHost, "Could not resolve host: h"), recorder);
        TransferResult result = TransferResult.Success(0);

        DatagramOpenResult opened = await connector.OpenAsync("h", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, opened.ExitCode);
        Assert.AreSame(result, recorder.ReportOn(result));
    }
}
