using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

[TestClass]
public sealed class EndPointReportingProtocolHandlerTests
{
    private static readonly IPEndPoint Local = new(IPAddress.Loopback, 64515);

    private static readonly IPEndPoint Remote = new(IPAddress.Loopback, 2628);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void SupportedSchemes_Always_AreTheWrappedHandlers()
    {
        RecordingProtocolHandler inner = RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "x");
        Diagnostics.Arrange("wrapped handler schemes", string.Join(",", inner.SupportedSchemes));

        EndPointReportingProtocolHandler handler = new(inner, new ConnectionEndPointRecorder());
        Diagnostics.Act("reporting handler schemes", string.Join(",", handler.SupportedSchemes));

        Diagnostics.Assert("same schemes instance", true, ReferenceEquals(inner.SupportedSchemes, handler.SupportedSchemes));
        Diagnostics.Assert("same wrapped handler", true, ReferenceEquals(inner, handler.Handler));
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
        Diagnostics.Arrange("end points the handler records", $"{Local} -> {Remote}");

        TransferResult result = await handler.ExecuteAsync(ContextOf(0));
        Diagnostics.Act("reported local end point", result.Report?.LocalEndPoint?.ToString() ?? "null");
        Diagnostics.Act("reported remote end point", result.Report?.RemoteEndPoint?.ToString() ?? "null");

        Diagnostics.Assert("reported local end point", Local.ToString(), result.Report?.LocalEndPoint?.ToString() ?? "null");
        Diagnostics.Assert("reported remote end point", Remote.ToString(), result.Report?.RemoteEndPoint?.ToString() ?? "null");
        Assert.AreEqual(Local, result.Report!.LocalEndPoint);
        Assert.AreEqual(Remote, result.Report.RemoteEndPoint);
    }

    [TestMethod]
    public async Task ExecuteAsync_EarlierTransferRecorded_ReportsNothingOfIt()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        EndPointReportingProtocolHandler handler = new(RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "x"), recorder);
        Diagnostics.Arrange("end points recorded before the transfer", $"{Local} -> {Remote}");
        Diagnostics.Arrange("handler fails with", CurlExitCode.CouldntConnect);

        TransferResult result = await handler.ExecuteAsync(ContextOf(0));
        Diagnostics.Act("report", result.Report?.ToString() ?? "null");

        Diagnostics.Assert("report", "null", result.Report?.ToString() ?? "null");
        Assert.IsNull(result.Report);
    }

    [TestMethod]
    public void Handler_GivenALazyHandler_IsTheBuiltHandler()
    {
        RecordingProtocolHandler built = RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "x");
        LazyProtocolHandler lazy = new(built.SupportedSchemes, () => built);
        Diagnostics.Arrange("wrapped handler", "a lazy handler that builds the recording handler");

        EndPointReportingProtocolHandler handler = new(lazy, new ConnectionEndPointRecorder());
        Diagnostics.Act("handler is the built one", ReferenceEquals(built, handler.Handler));

        Diagnostics.Assert("handler is the built one", true, ReferenceEquals(built, handler.Handler));
        Assert.AreSame(built, handler.Handler);
        Assert.AreSame(lazy, handler.GivenHandler);
    }

    [TestMethod]
    public async Task ExecuteAsync_RedirectHopFailsBeforeConnecting_ReportsTheEarlierHopsEndPoints()
    {
        ConnectionEndPointRecorder recorder = new();
        recorder.Record(Local, Remote);
        recorder.ReportOn(TransferResult.Success(0));
        EndPointReportingProtocolHandler handler = new(RecordingProtocolHandler.Failing("dict", CurlExitCode.CouldntConnect, "x"), recorder);
        Diagnostics.Arrange("first hop connected to", Remote);

        TransferResult result = await handler.ExecuteAsync(ContextOf(1));

        Assert.AreEqual(Remote, result.Report!.RemoteEndPoint);
    }

    private static TransferContext ContextOf(int redirectsFollowed) => new()
    {
        Url = CurlUrl.Parse("http://127.0.0.1/"),
        Output = new MemoryStream(),
        Http = new Curl.Protocol.Abstractions.HttpRequestOptions { RedirectsFollowed = redirectsFollowed },
    };
}
