using System.Runtime.ExceptionServices;

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
        using ManualResetEventSlim callerCancelled = new();
        int runs = 0;

        // The callback must not finish before the caller's wait has thrown, or the caller wakes to a run
        // already done and never reaches the cancelled-after-taken path; the throw is the event it waits for.
        void SignalCallerCancelled(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception is OperationCanceledException thrown && thrown.CancellationToken == cancellation.Token)
            {
                callerCancelled.Set();
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += SignalCallerCancelled;
        try
        {
            Task<int> caller = Task.Run(() => queue.Run(
                () =>
                {
                    started.Set();
                    callerCancelled.Wait(Limit);
                    return Interlocked.Increment(ref runs);
                },
                cancellation.Token));
            await WaitUntilQueuedAsync(queue);
            Task perform = Task.Run(queue.RunQueued);
            Assert.IsTrue(started.Wait(Limit));

            await cancellation.CancelAsync();
            int result = await caller.WaitAsync(Limit);
            await perform.WaitAsync(Limit);

            Assert.IsTrue(callerCancelled.IsSet);
            Assert.AreEqual(1, result);
            Assert.AreEqual(1, runs);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= SignalCallerCancelled;
        }
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

    [TestMethod]
    public async Task Run_CancelledByTheCallbackWhileTheCallerWaits_WaitsForThatRun()
    {
        MultiCallbackQueue queue = new();
        using CancellationTokenSource cancellation = new();
        int runs = 0;
        int result = 0;
        Thread caller = new(() => result = queue.Run(() => Interlocked.Increment(ref runs), cancellation.Token));
        caller.Start();
        await WaitUntilQueuedAsync(queue);
        DateTime deadline = DateTime.UtcNow + Limit;
        while (!caller.ThreadState.HasFlag(ThreadState.WaitSleepJoin) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(1);
        }

        await cancellation.CancelAsync();
        queue.RunQueued();
        Assert.IsTrue(caller.Join(Limit));

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
