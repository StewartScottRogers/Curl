using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

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
}
