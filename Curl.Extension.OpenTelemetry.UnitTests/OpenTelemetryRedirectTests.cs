using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.OpenTelemetry.UnitTests;

[TestClass]
public sealed class OpenTelemetryRedirectTests
{
    private const string Endpoint = "http://collector:4318/v1/traces";

    [TestMethod]
    public async Task ExecuteAsync_A302ThenA200_SendsOneTraceIdOnBothHopsAndExportsOneSpanWithStatus200_Adr1134()
    {
        var (hook, connection, connector) = Fixture();
        var sent = new List<ITransferContext>();

        await hook.ExecuteAsync(Hop(0), new TransferHookServices(connector, connector), c => Respond(c, sent, Redirect()));
        Assert.IsNull(connector.Target);
        await hook.ExecuteAsync(Hop(1), new TransferHookServices(connector, connector), c => Respond(c, sent, Final()));

        Assert.AreEqual(TraceParentOf(sent[0]), TraceParentOf(sent[1]));
        Assert.AreEqual(1, Count(connection.Written, "POST "));
        Assert.AreEqual(1, Count(connection.WrittenBody, "\"traceId\""));
        StringAssert.Contains(connection.WrittenBody, "\"http.response.status_code\"");
        StringAssert.Contains(connection.WrittenBody, "200");
    }

    [TestMethod]
    public async Task ExecuteAsync_AThreeHopChain_KeepsOneTraceIdAndTheFirstHopsStartAndExportsOnce()
    {
        var (hook, connection, connector) = Fixture();
        var sent = new List<ITransferContext>();
        var services = new TransferHookServices(connector, connector);

        await hook.ExecuteAsync(Hop(0), services, c => Respond(c, sent, Redirect()));
        await hook.ExecuteAsync(Hop(1), services, c => Respond(c, sent, Redirect()));
        await hook.ExecuteAsync(Hop(2), services, c => Respond(c, sent, Final()));

        Assert.AreEqual(1, sent.Select(TraceParentOf).Distinct().Count());
        Assert.AreEqual(1, Count(connection.Written, "POST "));
        StringAssert.Contains(connection.WrittenBody, "\"startTimeUnixNano\":\"1000000000\"");
    }

    [TestMethod]
    public async Task ExecuteAsync_ARedirectThatIsNotFollowed_ExportsItsOwnSpan()
    {
        var (hook, connection, connector) = Fixture();

        await hook.ExecuteAsync(Hop(0, follow: false), new TransferHookServices(connector, connector), c => Respond(c, [], Redirect()));

        Assert.AreEqual(1, Count(connection.Written, "POST "));
    }

    [TestMethod]
    public async Task ExecuteAsync_TheRedirectLimitIsReached_ExportsTheLastHopsSpan()
    {
        var (hook, connection, connector) = Fixture();

        await hook.ExecuteAsync(Hop(1, maxRedirects: 1), new TransferHookServices(connector, connector), c => Respond(c, [], Redirect()));

        Assert.AreEqual(1, Count(connection.Written, "POST "));
    }

    [TestMethod]
    public async Task ExecuteAsync_AnUnlimitedRedirectCountAndARedirect_DefersTheExportToTheLastHop()
    {
        var (hook, connection, connector) = Fixture();

        await hook.ExecuteAsync(Hop(5, maxRedirects: -1), new TransferHookServices(connector, connector), c => Respond(c, [], Redirect()));

        Assert.AreEqual(0, Count(connection.Written, "POST "));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferWithNoRedirect_ExportsOneSpanAsBefore()
    {
        var (hook, connection, connector) = Fixture();

        await hook.ExecuteAsync(Hop(0), new TransferHookServices(connector, connector), c => Respond(c, [], Final()));

        Assert.AreEqual(1, Count(connection.Written, "POST "));
    }

    [TestMethod]
    public async Task ExecuteAsync_AGivenTraceParentOnARedirectChain_ExportsOnceAtTheEnd()
    {
        var (hook, connection, connector) = Fixture();
        var services = new TransferHookServices(connector, connector);
        string[] given = ["traceparent: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"];

        await hook.ExecuteAsync(Hop(0, headers: given), services, c => Respond(c, [], Redirect()));
        await hook.ExecuteAsync(Hop(1, headers: given), services, c => Respond(c, [], Final()));

        Assert.AreEqual(1, Count(connection.Written, "POST "));
        StringAssert.Contains(connection.WrittenBody, "4bf92f3577b34da6a3ce929d0e0e4736");
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutServices_ASecondHopReusesNothingAndStillSendsATraceParent()
    {
        var (hook, _, _) = Fixture();
        var sent = new List<ITransferContext>();

        await hook.ExecuteAsync(Hop(1), c => Respond(c, sent, Final()));

        Assert.IsNotNull(TraceParentOf(sent[0]));
    }

    private static (OpenTelemetryTransferHook Hook, RecordedConnection Connection, RecordedConnector Connector) Fixture()
    {
        var connection = new RecordedConnection("HTTP/1.1 200 OK\r\n\r\n");
        return (new OpenTelemetryTransferHook(Endpoint, bytes => bytes.Fill(0xab)), connection, new RecordedConnector(connection));
    }

    private static TransferContext Hop(int followed, bool follow = true, int maxRedirects = 50, IReadOnlyList<string>? headers = null) => new()
    {
        Url = CurlUrl.Parse("http://example.com/"),
        Output = Stream.Null,
        Http = new HttpRequestOptions { Headers = headers ?? [], FollowRedirects = follow, RedirectsFollowed = followed, MaxRedirects = maxRedirects },
        TimeProvider = new FixedTimeProvider(),
    };

    private static TransferResult Redirect() =>
        TransferResult.Success(0) with { Report = new TransferReport { Method = "GET", ResponseCode = 302, RedirectUrl = "http://example.com/next" } };

    private static TransferResult Final() =>
        TransferResult.Success(0) with { Report = new TransferReport { Method = "GET", ResponseCode = 200 } };

    private static ValueTask<TransferResult> Respond(ITransferContext context, List<ITransferContext> sent, TransferResult result)
    {
        sent.Add(context);
        return ValueTask.FromResult(result);
    }

    private static string? TraceParentOf(ITransferContext context) =>
        context.Http!.Headers.FirstOrDefault(h => h.StartsWith("traceparent:", StringComparison.OrdinalIgnoreCase));

    private static int Count(string text, string part) =>
        text.Split(part).Length - 1;

    private sealed class FixedTimeProvider : TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1 + _calls++);
    }
}
