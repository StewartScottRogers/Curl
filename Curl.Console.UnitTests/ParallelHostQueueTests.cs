namespace Curl.Console;

/// <summary>
/// Pins the <c>-Z</c> host queue (BL-520): at most <c>--parallel-max-host</c> transfers run to one host,
/// other hosts unaffected, and without <c>--parallel-immediate</c> an <c>http://</c> transfer waits until
/// the first to its host has ended, as curl 8.21.0 does (measured 2026-09-28, BL-520 Notes).
/// </summary>
[TestClass]
public sealed class ParallelHostQueueTests
{
    [TestMethod]
    [DataRow("file:///dir/x")]
    [DataRow("http://a b/x")]
    public async Task WaitForHostAsync_UrlWithoutAHost_NeverWaits(string url)
    {
        ParallelHostQueue queue = new(1, parallelImmediate: false);

        await queue.WaitForHostAsync(url, CancellationToken.None);
        Task second = queue.WaitForHostAsync(url, CancellationToken.None);
        queue.Leave(url);

        Assert.IsTrue(second.IsCompleted);
    }

    [TestMethod]
    public async Task WaitForHostAsync_HttpWithoutParallelImmediate_WaitsForTheHostsFirstToEnd()
    {
        ParallelHostQueue queue = new(0, parallelImmediate: false);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://H:80/2", CancellationToken.None);
        Task third = queue.WaitForHostAsync("http://h/3", CancellationToken.None);
        Assert.IsFalse(second.IsCompleted);
        queue.Leave("http://h/1");
        await Task.WhenAll(second, third);

        Task fourth = queue.WaitForHostAsync("http://h/4", CancellationToken.None);
        Assert.IsTrue(fourth.IsCompleted);
    }

    [TestMethod]
    public async Task WaitForHostAsync_HttpWithoutParallelImmediate_DoesNotHoldBackOtherHostsOrPorts()
    {
        ParallelHostQueue queue = new(0, parallelImmediate: false);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);

        Assert.IsTrue(queue.WaitForHostAsync("http://other/2", CancellationToken.None).IsCompleted);
        Assert.IsTrue(queue.WaitForHostAsync("http://h:8080/3", CancellationToken.None).IsCompleted);
    }

    [TestMethod]
    [DataRow("https://h/2", false)]
    [DataRow("ftp://h/2", false)]
    [DataRow("http://h/2", true)]
    public async Task WaitForHostAsync_WithoutAHostLimit_StartsAtOnceUnlessHttpWaitsForTheFirst(string second, bool parallelImmediate)
    {
        ParallelHostQueue queue = new(0, parallelImmediate);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);

        Assert.IsTrue(queue.WaitForHostAsync(second, CancellationToken.None).IsCompleted);
    }

    [TestMethod]
    public async Task WaitForHostAsync_ParallelMaxHostOne_RunsOneAtATimePerHostInTheOrderTheyWaited()
    {
        ParallelHostQueue queue = new(1, parallelImmediate: true);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", CancellationToken.None);
        Task third = queue.WaitForHostAsync("http://h/3", CancellationToken.None);
        Task otherHost = queue.WaitForHostAsync("http://other/4", CancellationToken.None);
        Assert.IsTrue(otherHost.IsCompleted);
        Assert.IsFalse(second.IsCompleted);

        queue.Leave("http://h/1");
        await second;
        Assert.IsFalse(third.IsCompleted);
        queue.Leave("http://h/2");
        await third;
    }

    [TestMethod]
    public async Task WaitForHostAsync_ParallelMaxHostTwoWithoutParallelImmediate_RunsTwoOnceTheFirstHasEnded()
    {
        ParallelHostQueue queue = new(2, parallelImmediate: false);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", CancellationToken.None);
        Task third = queue.WaitForHostAsync("http://h/3", CancellationToken.None);
        Task fourth = queue.WaitForHostAsync("http://h/4", CancellationToken.None);
        queue.Leave("http://h/1");
        await Task.WhenAll(second, third);

        Assert.IsFalse(fourth.IsCompleted);
        queue.Leave("http://h/2");
        await fourth;
    }

    [TestMethod]
    public async Task WaitForHostAsync_Aborted_ThrowsAndLeavesTheQueue()
    {
        ParallelHostQueue queue = new(1, parallelImmediate: true);
        using CancellationTokenSource abort = new();

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", abort.Token);
        await abort.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => second);
        queue.Leave("http://h/1");

        Assert.IsTrue(queue.WaitForHostAsync("http://h/3", CancellationToken.None).IsCompleted);
    }

    [TestMethod]
    public async Task WaitForHostAsync_AlreadyAborted_Throws()
    {
        ParallelHostQueue queue = new(1, parallelImmediate: true);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.WaitForHostAsync("http://h/2", new CancellationToken(canceled: true)));
    }
}
