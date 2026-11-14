using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.OpenTelemetry.UnitTests;

[TestClass]
public sealed class OpenTelemetryChainEndTests
{
    private const string Endpoint = "http://collector:4318/v1/traces";

    [TestMethod]
    public async Task TransferChainFinishedAsync_TheFollowerRefusedTheHopAfterA302_ExportsOneSpanCarryingThe302_BL5162()
    {
        var (hook, connection, services) = Fixture();
        var first = Hop(0, new TransferProgress());

        await hook.ExecuteAsync(first, services, c => ValueTask.FromResult(Redirect()));
        Assert.IsEmpty(connection.Written);
        await hook.TransferChainFinishedAsync(first, TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Protocol \"ftp\" not supported or disabled"));

        Assert.AreEqual(1, Count(connection.Written, "POST "));
        StringAssert.Contains(connection.WrittenBody, "\"http.response.status_code\"");
        StringAssert.Contains(connection.WrittenBody, "302");
        StringAssert.Contains(connection.WrittenBody, "not supported or disabled");
    }

    [TestMethod]
    public async Task TransferChainFinishedAsync_TheChainResultCarriesItsOwnReport_ExportsThatReport()
    {
        var (hook, connection, services) = Fixture();
        var first = Hop(0, new TransferProgress());

        await hook.ExecuteAsync(first, services, c => ValueTask.FromResult(Redirect()));
        await hook.TransferChainFinishedAsync(first, TransferResult.Failure(CurlExitCode.UrlMalformat, "bad target") with { Report = new TransferReport { Method = "GET", ResponseCode = 307 } });

        StringAssert.Contains(connection.WrittenBody, "307");
    }

    [TestMethod]
    public async Task TransferChainFinishedAsync_TheLastHopAlreadyExported_ExportsNothingMore()
    {
        var (hook, connection, services) = Fixture();
        var first = Hop(0, new TransferProgress());

        await hook.ExecuteAsync(first, services, c => ValueTask.FromResult(Redirect()));
        await hook.ExecuteAsync(Hop(1, first.Progress), services, c => ValueTask.FromResult(Final()));
        await hook.TransferChainFinishedAsync(first, Final());

        Assert.AreEqual(1, Count(connection.Written, "POST "));
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoFollowedTransfersInterleaved_EachKeepsItsOwnTraceIdAndStart_BL5162()
    {
        var connectionA = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n");
        var connectionB = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n");
        var connectorA = new RecordedConnector(connectionA);
        var connectorB = new RecordedConnector(connectionB);
        byte fill = 0x10;
        var hook = new OpenTelemetryTransferHook(Endpoint, bytes => bytes.Fill(fill++));
        var clock = new SteppingTimeProvider();
        var progressA = new TransferProgress();
        var progressB = new TransferProgress();
        var sentA = new List<string?>();
        var sentB = new List<string?>();

        await hook.ExecuteAsync(Hop(0, progressA, clock), new TransferHookServices(connectorA, connectorA), c => Respond(c, sentA, Redirect()));
        await hook.ExecuteAsync(Hop(0, progressB, clock), new TransferHookServices(connectorB, connectorB), c => Respond(c, sentB, Redirect()));
        await hook.ExecuteAsync(Hop(1, progressA, clock), new TransferHookServices(connectorA, connectorA), c => Respond(c, sentA, Final()));
        await hook.ExecuteAsync(Hop(1, progressB, clock), new TransferHookServices(connectorB, connectorB), c => Respond(c, sentB, Final()));

        Assert.AreEqual(sentA[0], sentA[1]);
        Assert.AreEqual(sentB[0], sentB[1]);
        Assert.AreNotEqual(sentA[0], sentB[0]);
        Assert.AreEqual(1, Count(connectionA.Written, "POST "));
        Assert.AreEqual(1, Count(connectionB.Written, "POST "));
        StringAssert.Contains(connectionA.WrittenBody, TraceIdOf(sentA[0]));
        StringAssert.Contains(connectionB.WrittenBody, TraceIdOf(sentB[0]));
        StringAssert.Contains(connectionA.WrittenBody, "\"startTimeUnixNano\":\"1000000000\"");
        StringAssert.Contains(connectionB.WrittenBody, "\"startTimeUnixNano\":\"2000000000\"");
    }

    private static (OpenTelemetryTransferHook Hook, RecordedConnection Connection, TransferHookServices Services) Fixture()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n");
        var connector = new RecordedConnector(connection);
        return (new OpenTelemetryTransferHook(Endpoint, bytes => bytes.Fill(0xab)), connection, new TransferHookServices(connector, connector));
    }

    private static TransferContext Hop(int followed, ITransferProgress progress, TimeProvider? clock = null) => new()
    {
        Url = CurlUrl.Parse("http://example.com/"),
        Output = Stream.Null,
        Progress = progress,
        Http = new HttpRequestOptions { FollowRedirects = true, RedirectsFollowed = followed, MaxRedirects = 50 },
        TimeProvider = clock ?? new SteppingTimeProvider(),
    };

    private static TransferResult Redirect() =>
        TransferResult.Success(0) with { Report = new TransferReport { Method = "GET", ResponseCode = 302, RedirectUrl = "ftp://example.com/next" } };

    private static TransferResult Final() =>
        TransferResult.Success(0) with { Report = new TransferReport { Method = "GET", ResponseCode = 200 } };

    private static ValueTask<TransferResult> Respond(ITransferContext context, List<string?> sent, TransferResult result)
    {
        sent.Add(context.Http!.Headers.FirstOrDefault(h => h.StartsWith("traceparent:", StringComparison.OrdinalIgnoreCase)));
        return ValueTask.FromResult(result);
    }

    private static string TraceIdOf(string? traceParent) =>
        traceParent!.Split('-')[1];

    private static int Count(string text, string part) =>
        text.Split(part).Length - 1;

    private sealed class TransferProgress : ITransferProgress
    {
        public void ReportTransferStarted()
        {
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
        {
        }

        public void ReportUploaded(long bytesSoFar, long? expectedTotal)
        {
        }
    }

    private sealed class SteppingTimeProvider : TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1 + _calls++);
    }
}
