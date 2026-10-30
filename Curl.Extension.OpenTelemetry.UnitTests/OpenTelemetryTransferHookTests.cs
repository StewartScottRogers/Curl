using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.OpenTelemetry.UnitTests;

[TestClass]
public sealed class OpenTelemetryTransferHookTests
{
    private const string Endpoint = "http://collector:4318/v1/traces";
    private const string FreshTraceParent = "00-abababababababababababababababab-abababababababab-01";
    private const string GivenTraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    private readonly ITransferHook _hook = new OpenTelemetryTransferHook(Endpoint, bytes => bytes.Fill(0xab));

    [TestMethod]
    public async Task ExecuteAsync_AnHttpTransfer_SendsAFreshSampledTraceParent_W3cTraceContextSection3Point2()
    {
        ITransferContext? sent = null;

        await _hook.ExecuteAsync(Context(), context => Capture(context, out sent));

        CollectionAssert.AreEqual(new[] { "X-Given: 1", "traceparent: " + FreshTraceParent }, sent!.Http!.Headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_IsLeftAlone()
    {
        var context = new TransferContext { Url = CurlUrl.Parse("ftp://example.com/"), Output = Stream.Null };
        var connector = new RecordedConnector(new RecordedConnection(string.Empty));
        ITransferContext? sent = null;
        ITransferContext? sentWithServices = null;

        await _hook.ExecuteAsync(context, next => Capture(next, out sent));
        await _hook.ExecuteAsync(context, new TransferHookServices(connector, connector), next => Capture(next, out sentWithServices));

        Assert.AreSame(context, sent);
        Assert.AreSame(context, sentWithServices);
        Assert.IsNull(connector.Target);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithServices_ExportsTheTransfersSpanWithTheIdsItSent_OtlpSpecificationOtlpHttp()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n");
        var connector = new RecordedConnector(connection);
        var log = new RecordedDiagnosticLog();
        var expected = TransferResult.Success(3) with { Report = new TransferReport { Method = "GET", ResponseCode = 200 } };
        ITransferContext? sent = null;

        var result = await _hook.ExecuteAsync(Context(log), new TransferHookServices(connector, connector), context => Capture(context, out sent, expected));

        Assert.AreSame(expected, result);
        Assert.Contains("traceparent: " + FreshTraceParent, sent!.Http!.Headers);
        Assert.AreEqual(new ConnectTarget("collector", 4318, false), connector.Target);
        StringAssert.Contains(connection.WrittenBody, "\"traceId\":\"abababababababababababababababab\",\"spanId\":\"abababababababab\",\"name\":\"GET\"");
        StringAssert.Contains(connection.WrittenBody, "\"startTimeUnixNano\":\"1000000000\",\"endTimeUnixNano\":\"2000000000\"");
        Assert.IsEmpty(log.Lines);
    }

    [TestMethod]
    public async Task ExecuteAsync_AValidTraceParentGivenByH_IsLeftAndTheSpanTakesItsIds_W3cTraceContextSection3Point2()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n");
        var connector = new RecordedConnector(connection);
        var context = Context(headers: ["Traceparent: " + GivenTraceParent]);
        ITransferContext? sent = null;

        await _hook.ExecuteAsync(context, new TransferHookServices(connector, connector), next => Capture(next, out sent));

        Assert.AreSame(context, sent);
        StringAssert.Contains(connection.WrittenBody, "\"traceId\":\"4bf92f3577b34da6a3ce929d0e0e4736\",\"spanId\":\"00f067aa0ba902b7\"");
    }

    [TestMethod]
    public async Task ExecuteAsync_AnInvalidTraceParentGivenByH_IsLeftAndTheSpanGetsFreshIds()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n");
        var connector = new RecordedConnector(connection);
        var context = Context(headers: ["traceparent: nonsense"]);
        ITransferContext? sent = null;

        await _hook.ExecuteAsync(context, new TransferHookServices(connector, connector), next => Capture(next, out sent));

        Assert.AreSame(context, sent);
        StringAssert.Contains(connection.WrittenBody, "\"traceId\":\"abababababababababababababababab\"");
    }

    [TestMethod]
    public async Task ExecuteAsync_TheExportFails_LogsAWarningAndKeepsTheTransfersResult()
    {
        var connector = new RecordedConnector(ConnectResult.Failed(CurlExitCode.CouldntConnect, "Connection refused"));
        var log = new RecordedDiagnosticLog();
        var expected = TransferResult.Success(3);

        var result = await _hook.ExecuteAsync(Context(log), new TransferHookServices(connector, connector), context => Capture(context, out _, expected));

        Assert.AreSame(expected, result);
        CollectionAssert.AreEqual(
            new[] { "Warning otel: OTLP export to http://collector:4318/v1/traces failed: could not connect to collector: Connection refused" },
            log.Lines);
    }

    private static TransferContext Context(IDiagnosticLog? log = null, IReadOnlyList<string>? headers = null) => new()
    {
        Url = CurlUrl.Parse("http://example.com/"),
        Output = Stream.Null,
        Http = new HttpRequestOptions { Headers = headers ?? ["X-Given: 1"] },
        TimeProvider = new SteppingTimeProvider(),
        DiagnosticLog = log ?? NoDiagnosticLog.Instance,
    };

    private static ValueTask<TransferResult> Capture(ITransferContext context, out ITransferContext? sent, TransferResult? result = null)
    {
        sent = context;
        return ValueTask.FromResult(result ?? TransferResult.Success(0));
    }

    /// <summary>A clock that reads one second after the epoch, then two, then three, one second further each time.</summary>
    private sealed class SteppingTimeProvider : TimeProvider
    {
        private int _reads;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddSeconds(++_reads);
    }
}
