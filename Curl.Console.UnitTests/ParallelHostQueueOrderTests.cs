using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the order in which <see cref="ParallelHostQueue" /> starts the transfers waiting for a host
/// (ADR-0768): when the host's first transfer ends, the first to have waited starts last and the rest in
/// the order they waited, and <see cref="ParallelHostQueue.StartReleased" /> starts them one at a time,
/// each running until its first wait before the next starts. No time passes: the order owes nothing to
/// timing.
/// </summary>
[TestClass]
public sealed class ParallelHostQueueOrderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task StartReleased_FirstTransferEnded_StartsTheFirstWaiterLastOneAtATime()
    {
        Diagnostics.Arrange("queue", "no --parallel-max-host, no --parallel-immediate; /echo runs, /default, /utf8, /four wait");
        ManualTimeProvider time = new();
        long startedAt = time.GetTimestamp();
        ParallelHostQueue queue = new(0, parallelImmediate: false);
        List<string> started = [];
        List<string> runningWhenStarted = [];
        await queue.WaitForHostAsync("http://h/echo", CancellationToken.None);
        Task[] waiters =
        [
            StartWhenFreeAsync(queue, "/default", started, runningWhenStarted),
            StartWhenFreeAsync(queue, "/utf8", started, runningWhenStarted),
            StartWhenFreeAsync(queue, "/four", started, runningWhenStarted),
        ];

        queue.Leave("http://h/echo");
        Diagnostics.Act("started before StartReleased", string.Join(' ', started));
        bool noneBefore = started.Count == 0;
        queue.StartReleased();
        await Task.WhenAll(waiters);
        Diagnostics.Act("started", string.Join(' ', started));

        Diagnostics.Assert("none started at Leave", true, noneBefore);
        Assert.IsTrue(noneBefore);
        Diagnostics.Assert("start order", "/utf8 /four /default", string.Join(' ', started));
        Assert.AreEqual("/utf8 /four /default", string.Join(' ', started));
        Assert.AreEqual("|/utf8|/utf8 /four", string.Join('|', runningWhenStarted));
        Assert.AreEqual(startedAt, time.GetTimestamp());
    }

    [TestMethod]
    public async Task WaitForHostAsync_AbortedAfterLeaveFreedIt_GivesBackItsCountAndIsNotStarted()
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 1, no --parallel-immediate; /2 freed by Leave, then aborted");
        ParallelHostQueue queue = new(1, parallelImmediate: false);
        using CancellationTokenSource abort = new();
        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", abort.Token);
        queue.Leave("http://h/1");

        await abort.CancelAsync();
        queue.StartReleased();
        bool thirdStarts = queue.WaitForHostAsync("http://h/3", CancellationToken.None).IsCompleted;
        Diagnostics.Act("third started at once", thirdStarts);

        await Assert.ThrowsAsync<OperationCanceledException>(() => second);
        Diagnostics.Assert("third started at once", true, thirdStarts);
        Assert.IsTrue(thirdStarts);
    }

    /// <summary>Waits for the host, then records the transfer as started, with those started before it.</summary>
    private static async Task StartWhenFreeAsync(ParallelHostQueue queue, string path, List<string> started, List<string> runningWhenStarted)
    {
        await queue.WaitForHostAsync("http://h" + path, CancellationToken.None);
        runningWhenStarted.Add(string.Join(' ', started));
        started.Add(path);
    }
}
