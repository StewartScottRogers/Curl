using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

[TestClass]
public sealed class EndPointReportingProtocolHandlerTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 64515);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 2628);

    [TestMethod]
    public void SupportedSchemes_Always_AreTheWrappedHandlers()
    {
        RecordingProtocolHandler inner = RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "x");

        EndPointReportingProtocolHandler handler = new(inner, new ConnectionEndPointRecorder());

        Assert.AreSame(inner.SupportedSchemes, handler.SupportedSchemes);
        Assert.AreSame(inner, handler.Handler);
    }

    [TestMethod]
    public async Task ExecuteAsync_HandlerOpensAConnection_ReportsItsEndPoints()
    {
        ConnectionEndPointRecorder recorder = new();
        EndPointReportingProtocolHandler handler = new(
            new RecordingProtocolHandler("dict", _ =>
            {
                recorder.Record(Local, Remote);
                return ValueTask.FromResult(TransferResult.Success(3));
            }),
            recorder);

        TransferResult result = await handler.ExecuteAsync(null!);

        Assert.AreEqual(Local, result.Report!.LocalEndPoint);
        Assert.AreEqual(Remote, result.Report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ExecuteAsync_EarlierTransferRecorded_ReportsNothingOfIt()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        EndPointReportingProtocolHandler handler = new(RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "x"), recorder);

        TransferResult result = await handler.ExecuteAsync(null!);

        Assert.IsNull(result.Report);
    }
}
