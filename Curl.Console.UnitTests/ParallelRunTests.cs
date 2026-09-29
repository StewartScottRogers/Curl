using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-Z</c> run's bookkeeping (ADR-0127): the first failure in completion order is the exit
/// code, a run-ending result aborts the run under <c>--fail-early</c> and stops further starts otherwise,
/// and the deferred reports are written in command-line order before the dispatches close.
/// </summary>
[TestClass]
public sealed class ParallelRunTests
{
    private static readonly TransferResult Refused = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");

    private static readonly TransferResult Missing = TransferResult.Failure(CurlExitCode.FileCouldntReadFile, "missing");

    [TestMethod]
    public void ExitCode_NothingFailed_IsOk()
    {
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(TransferResult.Success(1), endsTheRun: false, failEarly: false);

        Assert.AreEqual(CurlExitCode.Ok, run.ExitCode);
        Assert.IsNull(run.FirstFailure);
        Assert.IsFalse(run.HasEnded);
        Assert.IsFalse(run.IsAborted);
    }

    [TestMethod]
    public void ExitCode_TwoFailures_IsTheFirstToEnd()
    {
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(Missing, endsTheRun: false, failEarly: false);
        run.RecordEnd(Refused, endsTheRun: false, failEarly: false);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, run.ExitCode);
        Assert.AreSame(Missing, run.FirstFailure);
    }

    [TestMethod]
    public void RecordEnd_RunEndingWithoutFailEarly_StopsFurtherStartsWithoutAborting()
    {
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(Refused, endsTheRun: true, failEarly: false);

        Assert.IsTrue(run.HasEnded);
        Assert.IsFalse(run.IsAborted);
    }

    [TestMethod]
    public void RecordEnd_RunEndingUnderFailEarly_AbortsTheRun()
    {
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(Refused, endsTheRun: true, failEarly: true);

        Assert.IsTrue(run.IsAborted);
        Assert.IsTrue(run.AbortToken.IsCancellationRequested);
        Assert.IsFalse(run.HasEnded);
    }

    [TestMethod]
    public async Task EndAsync_WritesDeferredReportsInTransferOrderThenClosesTheDispatches()
    {
        ParallelRun run = new(2, 0, true);
        List<string> written = [];
        run.DeferReport(2, () => Record(written, "2"));
        run.DeferReport(0, () => Record(written, "0"));
        run.DeferReport(1, () => Record(written, "1"));
        CountingConnectionPool pool = new();
        run.CloseAtEnd(new TransferDispatch(new ProtocolDispatcher([]), [], null, null, pool));

        await run.EndAsync();

        CollectionAssert.AreEqual(new[] { "0", "1", "2" }, written);
        Assert.AreEqual(1, pool.Disposals);
    }

    [TestMethod]
    public async Task EndAsync_NothingStarted_Ends()
    {
        ParallelRun run = new(1, 0, true);

        await run.EndAsync();

        Assert.AreEqual(CurlExitCode.Ok, run.ExitCode);
    }

    [TestMethod]
    public async Task EndAsync_ATransferFaulted_Rethrows()
    {
        ParallelRun run = new(1, 0, true);
        run.Queue.Add(Task.FromException(new IOException("broken")));

        await Assert.ThrowsExactlyAsync<IOException>(run.EndAsync);
    }

    [TestMethod]
    public void AbortedResult_IsCurlsAbortedTransfer()
    {
        Assert.AreEqual(CurlExitCode.AbortedByCallback, ParallelRun.AbortedResult.ExitCode);
        Assert.AreEqual("Transfer aborted due to critical error in another transfer", ParallelRun.AbortedResult.ErrorMessage);
    }

    private static Task Record(List<string> written, string text)
    {
        written.Add(text);
        return Task.CompletedTask;
    }

    /// <summary>A connection pool that counts how often it is closed.</summary>
    private sealed class CountingConnectionPool : IAsyncDisposable
    {
        public int Disposals { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
