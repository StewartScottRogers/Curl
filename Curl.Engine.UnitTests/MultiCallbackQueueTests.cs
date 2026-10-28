namespace Curl.Engine;

[TestClass]
public sealed class MultiCallbackQueueTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [TestMethod]
    public void Run_OnThePerformingThread_RunsAtOnce()
    {
        MultiCallbackQueue queue = new();
        int previous = queue.EnterPerform();

        int result = queue.Run(() => 7, CancellationToken.None);
        queue.LeavePerform(previous);

        Assert.AreEqual(7, result);
        Assert.IsFalse(queue.HasQueued);
    }

    [TestMethod]
    public void Run_TokenAlreadyCancelled_RunsAtOnceOnTheCallingThread()
    {
        MultiCallbackQueue queue = new();

        int result = queue.Run(() => Environment.CurrentManagedThreadId, new CancellationToken(canceled: true));

        Assert.AreEqual(Environment.CurrentManagedThreadId, result);
    }

    [TestMethod]
    public async Task Run_FromAnotherThread_WaitsUntilRunQueuedRunsItOnTheRunningThread()
    {
        MultiCallbackQueue queue = new();
        Task<int>[] callers = [Task.Run(() => queue.Run(() => Environment.CurrentManagedThreadId, CancellationToken.None)), Task.Run(() => queue.Run(() => Environment.CurrentManagedThreadId, CancellationToken.None))];
        await WaitUntilQueuedAsync(queue);
        Task arrived = queue.Arrived;

        while (!Task.WhenAll(callers).IsCompleted)
        {
            queue.RunQueued();
        }

        Assert.IsTrue(arrived.IsCompleted);
        CollectionAssert.AreEqual(new[] { Environment.CurrentManagedThreadId, Environment.CurrentManagedThreadId }, await Task.WhenAll(callers));
        Assert.IsFalse(queue.HasQueued);
        Assert.IsFalse(queue.Arrived.IsCompleted);
    }

    [TestMethod]
    public async Task Run_CallbackThrows_ThrowsOnTheCallingThread()
    {
        MultiCallbackQueue queue = new();
        Task<int> caller = Task.Run(() => queue.Run<int>(() => throw new InvalidOperationException("from the callback"), CancellationToken.None));
        await WaitUntilQueuedAsync(queue);

        queue.RunQueued();

        InvalidOperationException thrown = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => caller);
        Assert.AreEqual("from the callback", thrown.Message);
    }

    [TestMethod]
    public async Task Run_CancelledWhileQueued_RunsOnTheCallingThreadAndNotAgainOnThePerform()
    {
        MultiCallbackQueue queue = new();
        using CancellationTokenSource cancellation = new();
        int runs = 0;
        Task<int> caller = Task.Run(() => queue.Run(() => Interlocked.Increment(ref runs), cancellation.Token));
        await WaitUntilQueuedAsync(queue);

        await cancellation.CancelAsync();
        int result = await caller.WaitAsync(Limit);
        queue.RunQueued();

        Assert.AreEqual(1, result);
        Assert.AreEqual(1, runs);
    }

    [TestMethod]
    public async Task Run_CancelledWhileThePerformRunsIt_WaitsForThatRun()
    {
        MultiCallbackQueue queue = new();
        using CancellationTokenSource cancellation = new();
        using ManualResetEventSlim started = new();
        using ManualResetEventSlim release = new();
        int runs = 0;
        Task<int> caller = Task.Run(() => queue.Run(
            () =>
            {
                started.Set();
                release.Wait(Limit);
                return Interlocked.Increment(ref runs);
            },
            cancellation.Token));
        await WaitUntilQueuedAsync(queue);
        Task perform = Task.Run(queue.RunQueued);
        Assert.IsTrue(started.Wait(Limit));

        await cancellation.CancelAsync();
        release.Set();
        int result = await caller.WaitAsync(Limit);
        await perform.WaitAsync(Limit);

        Assert.AreEqual(1, result);
        Assert.AreEqual(1, runs);
    }

    [TestMethod]
    public async Task Run_CancelledByTheCallbackAfterThePerformTookIt_WaitsForThatRun()
    {
        MultiCallbackQueue queue = new();
        using CancellationTokenSource cancellation = new();
        int runs = 0;
        Task<int> caller = Task.Run(() => queue.Run(
            () =>
            {
                cancellation.Cancel();
                return Interlocked.Increment(ref runs);
            },
            cancellation.Token));
        await WaitUntilQueuedAsync(queue);

        queue.RunQueued();
        int result = await caller.WaitAsync(Limit);

        Assert.AreEqual(1, result);
        Assert.AreEqual(1, runs);
    }

    private static async Task WaitUntilQueuedAsync(MultiCallbackQueue queue)
    {
        DateTime deadline = DateTime.UtcNow + Limit;
        while (!queue.HasQueued && DateTime.UtcNow < deadline)
        {
            await Task.Delay(1);
        }
    }
}
