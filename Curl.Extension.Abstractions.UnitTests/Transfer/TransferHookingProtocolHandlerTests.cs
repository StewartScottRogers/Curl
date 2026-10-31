using System.Text;

namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed partial class TransferHookingProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithNoHookAskingToRepeat_RunsTheHandlerOnceAndWaitsForNothing()
    {
        CountingHandler handler = new();
        RecordingTimeProvider clock = new();

        TransferResult result = await new TransferHooks([new RepeatingHook([]), new RepeatingHook([])]).Around(handler).ExecuteAsync(ContextOn(clock, CancellationToken.None));

        Assert.AreEqual(1, handler.Runs);
        Assert.IsEmpty(clock.Delays);
        Assert.AreEqual(1, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAHookAskingToRepeat_WaitsItsDelayOnTheTransfersClockAndAsksTheHeadersAfresh()
    {
        CountingHandler handler = new();
        RecordingTimeProvider clock = new();
        MemoryStream output = new();
        RepeatingHook silent = new([]);
        RepeatingHook repeating = new([TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(3)]);
        TransferContext context = ContextOn(clock, CancellationToken.None, output);

        TransferResult result = await new TransferHooks([silent, repeating]).Around(handler).ExecuteAsync(context);

        Assert.AreEqual(3, handler.Runs);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(3) }, clock.Delays.ToArray());
        CollectionAssert.AreEqual(new[] { "X-Curl: own", "X-Run: 1", "X-Run: 1" }, handler.Headers[0]);
        CollectionAssert.AreEqual(new[] { "X-Curl: own", "X-Run: 3", "X-Run: 3" }, handler.Headers[2]);
        Assert.AreEqual("123", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(3, result.BytesTransferred);
        Assert.AreSame(result, repeating.Finished.Last());
        Assert.HasCount(3, silent.Finished);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancelledWhileWaitingToRepeat_ThrowsOperationCanceledNotTheEarlierResult()
    {
        CountingHandler handler = new();
        using CancellationTokenSource cancellation = new();
        RecordingTimeProvider clock = new(fires: false, onTimerCreated: cancellation.Cancel);

        IProtocolHandler hooked = new TransferHooks([new RepeatingHook([TimeSpan.FromHours(1)])]).Around(handler);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await hooked.ExecuteAsync(ContextOn(clock, cancellation.Token)));

        Assert.AreEqual(1, handler.Runs);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromHours(1) }, clock.Delays.ToArray());
    }

    private static TransferContext ContextOn(TimeProvider clock, CancellationToken cancellationToken, Stream? output = null) => new()
    {
        Url = CurlUrl.Parse("http://example.com/events"),
        Output = output ?? new MemoryStream(),
        Http = new HttpRequestOptions { Headers = ["X-Curl: own"] },
        TimeProvider = clock,
        CancellationToken = cancellationToken,
    };

    private sealed class CountingHandler : IProtocolHandler
    {
        public int Runs { get; private set; }

        public List<string[]> Headers { get; } = [];

        public IReadOnlyCollection<string> SupportedSchemes { get; } = ["http"];

        public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
        {
            Runs++;
            Headers.Add([.. context.Http!.Headers]);
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes(Runs.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return TransferResult.Success(Runs);
        }
    }

    private sealed class RepeatingHook(IReadOnlyList<TimeSpan> delays) : ITransferHook
    {
        public List<TransferResult> Finished { get; } = [];

        public IEnumerable<string> RequestHeadersFor(ITransferContext context) => [$"X-Run: {Finished.Count + 1}"];

        public void TransferFinished(TransferResult result) => Finished.Add(result);

        public TimeSpan? RepeatAfter(TransferResult result) => Finished.Count <= delays.Count ? delays[Finished.Count - 1] : null;
    }

    /// <summary>Records each delay asked of it and, unless told not to, fires the timer at once on the thread pool.</summary>
    private sealed class RecordingTimeProvider(bool fires = true, Action? onTimerCreated = null) : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            if (fires)
            {
                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }

            onTimerCreated?.Invoke();
            return new IdleTimer();
        }

        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
