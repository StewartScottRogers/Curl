using Curl.Protocol.Abstractions;
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
    public async Task StartReleased_FirstConnectionStayedOpen_StartsTheWaitersInUrlOrder()
    {
        Diagnostics.Arrange("queue", "no --parallel-max-host; /echo runs and its connection stays open; /default, /utf8, /four wait");
        ParallelHostQueue queue = new(0, parallelImmediate: false);
        List<string> started = [];
        await queue.WaitForHostAsync("http://h/echo", CancellationToken.None);
        Task[] waiters =
        [
            StartWhenFreeAsync(queue, "/default", started, []),
            StartWhenFreeAsync(queue, "/utf8", started, []),
            StartWhenFreeAsync(queue, "/four", started, []),
        ];

        queue.Leave("http://h/echo", connectionStayedOpen: true);
        queue.StartReleased();
        await Task.WhenAll(waiters);
        Diagnostics.Act("started", string.Join(' ', started));

        Diagnostics.Assert("start order", "/default /utf8 /four", string.Join(' ', started));
        Assert.AreEqual("/default /utf8 /four", string.Join(' ', started));
    }

    [TestMethod]
    public async Task StartReleased_OneTransferAtATimeAndConnectionStayedOpen_StartsTheWaitersInUrlOrder()
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 1 (one transfer at a time); /1 runs and keeps its connection; /2, /3 wait");
        ParallelHostQueue queue = new(1, parallelImmediate: false);
        List<string> started = [];
        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task[] waiters = [StartWhenFreeAsync(queue, "/2", started, []), StartWhenFreeAsync(queue, "/3", started, [])];

        queue.Leave("http://h/1", connectionStayedOpen: true);
        queue.StartReleased();
        queue.Leave("http://h/2", connectionStayedOpen: true);
        queue.StartReleased();
        await Task.WhenAll(waiters);
        Diagnostics.Act("started", string.Join(' ', started));

        Diagnostics.Assert("start order", "/2 /3", string.Join(' ', started));
        Assert.AreEqual("/2 /3", string.Join(' ', started));
    }

    [TestMethod]
    [DataRow(CurlExitCode.Ok, "1.1", "", true)]
    [DataRow(CurlExitCode.Ok, "1.1", "close", false)]
    [DataRow(CurlExitCode.Ok, "1.1", "Close", false)]
    [DataRow(CurlExitCode.Ok, "1.0", "", false)]
    [DataRow(CurlExitCode.Ok, "1.0", "keep-alive", true)]
    [DataRow(CurlExitCode.Ok, "2.0", "", true)]
    [DataRow(CurlExitCode.CouldntConnect, "1.1", "", false)]
    public void ConnectionStayedOpen_ByVersionAndConnectionHeader_ReflectsWhetherTheConnectionWasKept(
        CurlExitCode exitCode, string version, string connectionHeader, bool expected)
    {
        Diagnostics.Arrange("result", $"{exitCode} HTTP/{version} Connection: '{connectionHeader}'");
        List<KeyValuePair<string, string>> headers = [new("Content-Length", "0")];
        if (connectionHeader.Length > 0)
        {
            headers.Add(new("connection", connectionHeader));
        }

        TransferResult result = new(exitCode, 0) { Report = new TransferReport { HttpVersion = Version.Parse(version), ResponseHeaders = headers } };

        bool actual = ParallelHostQueue.ConnectionStayedOpen(result);

        Diagnostics.Assert("stayed open", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConnectionStayedOpen_NoReport_IsFalse()
    {
        Diagnostics.Arrange("result", "success with no report (a non-HTTP transfer)");

        bool actual = ParallelHostQueue.ConnectionStayedOpen(new TransferResult(CurlExitCode.Ok, 0));

        Diagnostics.Assert("stayed open", false, actual);
        Assert.IsFalse(actual);
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
