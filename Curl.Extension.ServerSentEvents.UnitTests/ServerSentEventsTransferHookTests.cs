using System.Collections.Concurrent;
using System.Text;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.ServerSentEvents;

[TestClass]
public sealed class ServerSentEventsTransferHookTests
{
    private const string EventStream = "text/event-stream";

    [TestMethod]
    public async Task ExecuteAsync_AStreamThatEnds_ReconnectsAfterRetryWithLastEventId_Sections923And924()
    {
        ScriptedHandler handler = new(
            (200, EventStream, "retry: 1500\nid: 41\ndata: first\n\ndata: cut off"),
            (200, "Text/Event-Stream; charset=utf-8", "data: second\n\n"),
            (204, null, string.Empty));
        ImmediateTimeProvider clock = new();
        MemoryStream output = new();

        TransferResult result = await Hooked(handler).ExecuteAsync(Context(output, clock));

        Assert.AreEqual(204, result.Report!.ResponseCode);
        Assert.HasCount(3, handler.Requests);
        CollectionAssert.AreEqual(new[] { "X-Own: 1", "Accept: text/event-stream", "Cache-Control: no-cache", "Pragma: no-cache" }, handler.Requests[0].ToArray());
        CollectionAssert.Contains(handler.Requests[1].ToArray(), "Last-Event-ID: 41");
        CollectionAssert.Contains(handler.Requests[2].ToArray(), "Last-Event-ID: 41");
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(1500), TimeSpan.FromMilliseconds(1500) }, clock.DueTimes.ToArray());
        Assert.AreEqual("event: message\nid: 41\ndata: first\n\nevent: message\nid: 41\ndata: second\n\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_NoRetryField_WaitsTheDefaultReconnectionTime_Section922()
    {
        ScriptedHandler handler = new((200, EventStream, "data: x\n\n"), (404, "text/html", "<p>gone</p>"));
        ImmediateTimeProvider clock = new();

        await Hooked(handler).ExecuteAsync(Context(new MemoryStream(), clock));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(ServerSentEventsTransferHook.DefaultReconnectionTimeMilliseconds) }, clock.DueTimes.ToArray());
        CollectionAssert.DoesNotContain(handler.Requests[0].ToArray(), "Last-Event-ID: ");
    }

    [TestMethod]
    public async Task ExecuteAsync_ARetryLongerThanTheTimerTakes_WaitsTheLongestItTakes()
    {
        ScriptedHandler handler = new((200, EventStream, "retry: 99999999999\n\n"), (204, null, string.Empty));
        ImmediateTimeProvider clock = new();

        await Hooked(handler).ExecuteAsync(Context(new MemoryStream(), clock));

        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(ServerSentEventsTransferHook.LongestReconnectionTimeMilliseconds) }, clock.DueTimes.ToArray());
    }

    [TestMethod]
    [DataRow(200, "text/plain")]
    [DataRow(200, null)]
    [DataRow(500, EventStream)]
    public void RepeatAfter_AResponseThatIsNotAnEventStream_FailsTheConnection_Section922(int code, string? contentType)
    {
        TransferResult result = TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = code, ContentType = contentType } };

        Assert.IsNull(new ServerSentEventsTransferHook().RepeatAfter(result));
    }

    [TestMethod]
    public void RepeatAfter_ATransferWithNoReport_IsNotRepeated()
    {
        Assert.IsNull(new ServerSentEventsTransferHook().RepeatAfter(TransferResult.Failure(CurlExitCode.CouldntConnect, "refused")));
    }

    [TestMethod]
    public async Task ExecuteAsync_ATransferThatIsNotHttp_RunsUnchanged()
    {
        TransferContext context = new() { Url = CurlUrl.Parse("ftp://example.com/f"), Output = new MemoryStream() };
        ITransferContext? sent = null;

        await new ServerSentEventsTransferHook().ExecuteAsync(context, given =>
        {
            sent = given;
            return ValueTask.FromResult(TransferResult.Success(0));
        });

        Assert.AreSame(context, sent);
    }

    private static IProtocolHandler Hooked(IProtocolHandler handler) => new TransferHooks([new ServerSentEventsTransferHook()]).Around(handler);

    private static TransferContext Context(Stream output, TimeProvider clock) => new()
    {
        Url = CurlUrl.Parse("http://example.com/events"),
        Output = output,
        Http = new HttpRequestOptions { Headers = ["X-Own: 1"] },
        TimeProvider = clock,
    };

    /// <summary>A handler that answers each transfer with the next scripted response, recording the request headers.</summary>
    private sealed class ScriptedHandler(params (int Code, string? ContentType, string Body)[] responses) : IProtocolHandler
    {
        public List<IReadOnlyList<string>> Requests { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes => ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            (int code, string? contentType, string body) = responses[Requests.Count];
            Requests.Add(context.Http!.Headers);
            await context.Output.WriteAsync(Encoding.UTF8.GetBytes(body));
            return TransferResult.Success(body.Length) with { Report = new TransferReport { ResponseCode = code, ContentType = contentType } };
        }
    }

    /// <summary>A fake clock whose every timer fires at once, recording the time it was set for.</summary>
    private sealed class ImmediateTimeProvider : TimeProvider
    {
        private readonly ConcurrentQueue<TimeSpan> dueTimes = new();

        public IReadOnlyList<TimeSpan> DueTimes => [.. dueTimes];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            dueTimes.Enqueue(dueTime);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
