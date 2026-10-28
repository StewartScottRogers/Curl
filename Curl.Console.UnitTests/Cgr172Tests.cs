using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins CGR-172 (BL-3701, ADR-0934): under <c>-Z</c> without <c>--parallel-immediate</c>, a transfer
/// waiting for its host's first transfer to end is freed when any transfer ends, at another host or
/// after a failed connect, as curl 8.22.0 retries its pending transfers whenever one is done.
/// </summary>
[TestClass]
public sealed class Cgr172Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task Leave_TransferAtAnotherHostEnded_FreesTheWaiterForTheFirstToEnd()
    {
        Diagnostics.Arrange("queue", "/a runs at 22000, /q runs at 22001, /c waits at 22000; /q ends");
        ParallelHostQueue queue = new(0, parallelImmediate: false);
        await queue.WaitForHostAsync("http://127.0.0.1:22000/a", CancellationToken.None);
        await queue.WaitForHostAsync("http://127.0.0.1:22001/q", CancellationToken.None);
        Task c = queue.WaitForHostAsync("http://127.0.0.1:22000/c", CancellationToken.None);
        bool waitedBefore = !c.IsCompleted;

        queue.Leave("http://127.0.0.1:22001/q");
        queue.StartReleased();
        Diagnostics.Act("c started", c.IsCompleted);

        Assert.IsTrue(waitedBefore);
        Diagnostics.Assert("c started", true, c.IsCompleted);
        Assert.IsTrue(c.IsCompleted);
    }

    [TestMethod]
    public async Task Leave_RefusedConnectEnded_FreesTheWaiterAndLaterTransfersStartAtOnce()
    {
        Diagnostics.Arrange("queue", "/a runs at 22000, /x at closed 22099 fails, /c waits at 22000; then /d");
        ParallelHostQueue queue = new(0, parallelImmediate: false);
        await queue.WaitForHostAsync("http://127.0.0.1:22000/a", CancellationToken.None);
        await queue.WaitForHostAsync("http://127.0.0.1:22099/x", CancellationToken.None);
        Task c = queue.WaitForHostAsync("http://127.0.0.1:22000/c", CancellationToken.None);

        queue.Leave("http://127.0.0.1:22099/x");
        queue.StartReleased();
        bool dStartsAtOnce = queue.WaitForHostAsync("http://127.0.0.1:22000/d", CancellationToken.None).IsCompleted;
        Diagnostics.Act("c started, d at once", $"{c.IsCompleted} {dStartsAtOnce}");

        Diagnostics.Assert("c started, d at once", "True True", $"{c.IsCompleted} {dStartsAtOnce}");
        Assert.IsTrue(c.IsCompleted);
        Assert.IsTrue(dStartsAtOnce);
    }

    [TestMethod]
    public async Task Leave_TransferWithNoHostEnded_FreesTheWaiterForTheFirstToEnd()
    {
        Diagnostics.Arrange("queue", "/a runs at h, /c waits at h; a file:// transfer ends");
        ParallelHostQueue queue = new(0, parallelImmediate: false);
        await queue.WaitForHostAsync("http://h/a", CancellationToken.None);
        Task c = queue.WaitForHostAsync("http://h/c", CancellationToken.None);

        queue.Leave("file:///dir/x");
        queue.StartReleased();
        Diagnostics.Act("c started", c.IsCompleted);

        Diagnostics.Assert("c started", true, c.IsCompleted);
        Assert.IsTrue(c.IsCompleted);
    }

    [TestMethod]
    public async Task Leave_TransferAtAnotherHostEnded_KeepsTheParallelMaxHostLimit()
    {
        Diagnostics.Arrange("queue", "--parallel-max-host 1; /a runs at h, /q runs at g, /c waits at h; /q ends");
        ParallelHostQueue queue = new(1, parallelImmediate: false);
        await queue.WaitForHostAsync("http://h/a", CancellationToken.None);
        await queue.WaitForHostAsync("http://g/q", CancellationToken.None);
        Task c = queue.WaitForHostAsync("http://h/c", CancellationToken.None);

        queue.Leave("http://g/q");
        queue.StartReleased();
        Diagnostics.Act("c started", c.IsCompleted);

        Diagnostics.Assert("c started", false, c.IsCompleted);
        Assert.IsFalse(c.IsCompleted);
    }
}
