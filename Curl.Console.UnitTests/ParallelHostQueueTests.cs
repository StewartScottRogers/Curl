using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-Z</c> host queue (BL-520): at most <c>--parallel-max-host</c> transfers run to one host,
/// other hosts unaffected, and without <c>--parallel-immediate</c> an <c>http://</c> transfer waits until
/// the first to its host has ended, as curl 8.21.0 does (measured 2026-09-28, BL-520 Notes).
/// </summary>
[TestClass]
public sealed class ParallelHostQueueTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("file:///dir/x")]
    [DataRow("http://a b/x")]
    public async Task WaitForHostAsync_UrlWithoutAHost_NeverWaits(string url)
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 1, no --parallel-immediate");
        Diagnostics.Arrange("url (twice)", url);
        ParallelHostQueue queue = new(1, parallelImmediate: false);

        await queue.WaitForHostAsync(url, CancellationToken.None);
        Task second = queue.WaitForHostAsync(url, CancellationToken.None);
        queue.Leave(url);
        Diagnostics.Act("second wait completed", second.IsCompleted);

        Diagnostics.Assert("second wait completed", true, second.IsCompleted);
        Assert.IsTrue(second.IsCompleted);
    }

    [TestMethod]
    public async Task WaitForHostAsync_HttpWithoutParallelImmediate_WaitsForTheHostsFirstToEnd()
    {
        Diagnostics.Arrange("queue", "no host limit, no --parallel-immediate");
        Diagnostics.Arrange("urls", "http://h/1 then http://H:80/2, http://h/3, then http://h/4 after the first leaves");
        ParallelHostQueue queue = new(0, parallelImmediate: false);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://H:80/2", CancellationToken.None);
        Task third = queue.WaitForHostAsync("http://h/3", CancellationToken.None);
        Diagnostics.Act("second completed before the first left", second.IsCompleted);
        Diagnostics.Assert("second completed before the first left", false, second.IsCompleted);
        Assert.IsFalse(second.IsCompleted);
        queue.Leave("http://h/1");
        await Task.WhenAll(second, third);

        Task fourth = queue.WaitForHostAsync("http://h/4", CancellationToken.None);
        Diagnostics.Act("fourth completed", fourth.IsCompleted);
        Diagnostics.Assert("fourth completed", true, fourth.IsCompleted);
        Assert.IsTrue(fourth.IsCompleted);
    }

    [TestMethod]
    public async Task WaitForHostAsync_HttpSpeakingHttp2FromTheStart_DoesNotWaitForTheHostsFirstToEnd()
    {
        Diagnostics.Arrange("queue", "no host limit, no --parallel-immediate");
        Diagnostics.Arrange("urls", "http://h/1 and http://h/2, both speaking HTTP/2 from the start");
        ParallelHostQueue queue = new(0, parallelImmediate: false);

        await queue.WaitForHostAsync("http://h/1", speaksHttp2FromTheStart: true, CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", speaksHttp2FromTheStart: true, CancellationToken.None);
        Diagnostics.Act("second completed", second.IsCompleted);

        Diagnostics.Assert("second completed", true, second.IsCompleted);
        Assert.IsTrue(second.IsCompleted);
    }

    [TestMethod]
    public async Task WaitForHostAsync_HttpWithoutParallelImmediate_DoesNotHoldBackOtherHostsOrPorts()
    {
        Diagnostics.Arrange("queue", "no host limit, no --parallel-immediate");
        Diagnostics.Arrange("urls", "http://h/1 running, then http://other/2 and http://h:8080/3");
        ParallelHostQueue queue = new(0, parallelImmediate: false);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);

        bool otherHost = queue.WaitForHostAsync("http://other/2", CancellationToken.None).IsCompleted;
        bool otherPort = queue.WaitForHostAsync("http://h:8080/3", CancellationToken.None).IsCompleted;
        Diagnostics.Act("other host completed", otherHost);
        Diagnostics.Act("other port completed", otherPort);
        Diagnostics.Assert("other host completed", true, otherHost);
        Diagnostics.Assert("other port completed", true, otherPort);
        Assert.IsTrue(otherHost);
        Assert.IsTrue(otherPort);
    }

    [TestMethod]
    [DataRow("https://h/2", false)]
    [DataRow("ftp://h/2", false)]
    [DataRow("http://h/2", true)]
    public async Task WaitForHostAsync_WithoutAHostLimit_StartsAtOnceUnlessHttpWaitsForTheFirst(string second, bool parallelImmediate)
    {
        Diagnostics.Arrange("parallel immediate", parallelImmediate);
        Diagnostics.Arrange("urls", $"http://h/1 running, then {second}");
        ParallelHostQueue queue = new(0, parallelImmediate);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);

        bool completed = queue.WaitForHostAsync(second, CancellationToken.None).IsCompleted;
        Diagnostics.Act("second completed", completed);
        Diagnostics.Assert("second completed", true, completed);
        Assert.IsTrue(completed);
    }

    [TestMethod]
    public async Task WaitForHostAsync_ParallelMaxHostOne_RunsOneAtATimePerHostInTheOrderTheyWaited()
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 1, --parallel-immediate");
        Diagnostics.Arrange("urls", "http://h/1 running, then http://h/2, http://h/3 and http://other/4");
        ParallelHostQueue queue = new(1, parallelImmediate: true);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", CancellationToken.None);
        Task third = queue.WaitForHostAsync("http://h/3", CancellationToken.None);
        Task otherHost = queue.WaitForHostAsync("http://other/4", CancellationToken.None);
        Diagnostics.Act("other host completed", otherHost.IsCompleted);
        Diagnostics.Act("second completed while the first runs", second.IsCompleted);
        Diagnostics.Assert("other host completed", true, otherHost.IsCompleted);
        Assert.IsTrue(otherHost.IsCompleted);
        Diagnostics.Assert("second completed while the first runs", false, second.IsCompleted);
        Assert.IsFalse(second.IsCompleted);

        queue.Leave("http://h/1");
        await second;
        Diagnostics.Assert("third completed while the second runs", false, third.IsCompleted);
        Assert.IsFalse(third.IsCompleted);
        queue.Leave("http://h/2");
        await third;
    }

    [TestMethod]
    public async Task WaitForHostAsync_ParallelMaxHostTwoWithoutParallelImmediate_RunsTwoOnceTheFirstHasEnded()
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 2, no --parallel-immediate");
        Diagnostics.Arrange("urls", "http://h/1 running, then http://h/2, http://h/3 and http://h/4");
        ParallelHostQueue queue = new(2, parallelImmediate: false);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", CancellationToken.None);
        Task third = queue.WaitForHostAsync("http://h/3", CancellationToken.None);
        Task fourth = queue.WaitForHostAsync("http://h/4", CancellationToken.None);
        queue.Leave("http://h/1");
        await Task.WhenAll(second, third);
        Diagnostics.Act("fourth completed while two run", fourth.IsCompleted);

        Diagnostics.Assert("fourth completed while two run", false, fourth.IsCompleted);
        Assert.IsFalse(fourth.IsCompleted);
        queue.Leave("http://h/2");
        await fourth;
    }

    [TestMethod]
    public async Task WaitForHostAsync_Aborted_ThrowsAndLeavesTheQueue()
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 1, --parallel-immediate");
        Diagnostics.Arrange("urls", "http://h/1 running, http://h/2 waiting then aborted, then http://h/3");
        ParallelHostQueue queue = new(1, parallelImmediate: true);
        using CancellationTokenSource abort = new();

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);
        Task second = queue.WaitForHostAsync("http://h/2", abort.Token);
        await abort.CancelAsync();
        Diagnostics.Assert("aborted wait throws", nameof(OperationCanceledException), "thrown below");
        await Assert.ThrowsAsync<OperationCanceledException>(() => second);
        queue.Leave("http://h/1");

        bool third = queue.WaitForHostAsync("http://h/3", CancellationToken.None).IsCompleted;
        Diagnostics.Act("third completed", third);
        Diagnostics.Assert("third completed", true, third);
        Assert.IsTrue(third);
    }

    [TestMethod]
    public async Task WaitForHostAsync_AlreadyAborted_Throws()
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 1, --parallel-immediate");
        Diagnostics.Arrange("urls", "http://h/1 running, then http://h/2 with a cancelled token");
        ParallelHostQueue queue = new(1, parallelImmediate: true);

        await queue.WaitForHostAsync("http://h/1", CancellationToken.None);

        Diagnostics.Act("second wait", "started with an already cancelled token");
        Diagnostics.Assert("exception", nameof(OperationCanceledException), "checked by Assert.ThrowsAsync");
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => queue.WaitForHostAsync("http://h/2", new CancellationToken(canceled: true)));
    }
}
