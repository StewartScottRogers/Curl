using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task WaitForFreeSlotAsync_NoSlotFreeWithAMeter_DrawsTheMeterBeforeWaiting()
    {
        Diagnostics.Arrange("run", "--parallel-max 1 with a progress meter, one transfer running");
        System.Text.StringBuilder written = new();
        ParallelRun run = new(1, 0, true)
        {
            ProgressMeter = new ParallelProgressMeter(new ManualTimerTimeProvider(), new WriteGate(), text => written.Append(text)),
        };
        TaskCompletionSource running = new();
        run.Queue.Add(running.Task);

        Task wait = run.WaitForFreeSlotAsync();
        string drawnBeforeTheSlotFreed = written.ToString();
        running.SetResult();
        await wait;
        Diagnostics.Act("drawn before the slot freed", drawnBeforeTheSlotFreed.Replace(Environment.NewLine, "\\n").Replace("\r", "\\r"));

        Diagnostics.Assert("drawn before the slot freed starts with", "DL% UL%  Dled  Uled  Xfers  Live", drawnBeforeTheSlotFreed[..Math.Min(32, drawnBeforeTheSlotFreed.Length)]);
        StringAssert.StartsWith(drawnBeforeTheSlotFreed, "DL% UL%  Dled  Uled  Xfers  Live");
        run.ProgressMeter.Dispose();
    }

    [TestMethod]
    public void ExitCode_NothingFailed_IsOk()
    {
        Diagnostics.Arrange("results", "one success, not run-ending, no --fail-early");
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(TransferResult.Success(1), endsTheRun: false, failEarly: false);
        Diagnostics.Act("exit code", run.ExitCode);
        Diagnostics.Act("first failure", run.FirstFailure?.ErrorMessage ?? "none");

        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.ExitCode);
        Diagnostics.Assert("has ended / is aborted", "False/False", $"{run.HasEnded}/{run.IsAborted}");
        Assert.AreEqual(CurlExitCode.Ok, run.ExitCode);
        Assert.IsNull(run.FirstFailure);
        Assert.IsFalse(run.HasEnded);
        Assert.IsFalse(run.IsAborted);
    }

    [TestMethod]
    public void ExitCode_TwoFailures_IsTheFirstToEnd()
    {
        Diagnostics.Arrange("results", "missing file (37) then refused connection (7)");
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(Missing, endsTheRun: false, failEarly: false);
        run.RecordEnd(Refused, endsTheRun: false, failEarly: false);
        Diagnostics.Act("exit code", run.ExitCode);
        Diagnostics.Act("first failure", run.FirstFailure?.ErrorMessage ?? "none");

        Diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, run.ExitCode);
        Diagnostics.Assert("first failure", Missing.ErrorMessage, run.FirstFailure?.ErrorMessage);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, run.ExitCode);
        Assert.AreSame(Missing, run.FirstFailure);
    }

    [TestMethod]
    public void RecordEnd_RunEndingWithoutFailEarly_StopsFurtherStartsWithoutAborting()
    {
        Diagnostics.Arrange("results", "refused connection, run-ending, no --fail-early");
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(Refused, endsTheRun: true, failEarly: false);
        Diagnostics.Act("has ended / is aborted", $"{run.HasEnded}/{run.IsAborted}");

        Diagnostics.Assert("has ended / is aborted", "True/False", $"{run.HasEnded}/{run.IsAborted}");
        Assert.IsTrue(run.HasEnded);
        Assert.IsFalse(run.IsAborted);
    }

    [TestMethod]
    public void RecordEnd_RunEndingUnderFailEarly_AbortsTheRun()
    {
        Diagnostics.Arrange("results", "refused connection, run-ending, --fail-early");
        ParallelRun run = new(2, 0, true);

        run.RecordEnd(Refused, endsTheRun: true, failEarly: true);
        Diagnostics.Act("is aborted / abort requested / has ended", $"{run.IsAborted}/{run.AbortToken.IsCancellationRequested}/{run.HasEnded}");

        Diagnostics.Assert("is aborted / abort requested / has ended", "True/True/False", $"{run.IsAborted}/{run.AbortToken.IsCancellationRequested}/{run.HasEnded}");
        Assert.IsTrue(run.IsAborted);
        Assert.IsTrue(run.AbortToken.IsCancellationRequested);
        Assert.IsFalse(run.HasEnded);
    }

    [TestMethod]
    public async Task EndAsync_WritesDeferredReportsInTransferOrderThenClosesTheDispatches()
    {
        Diagnostics.Arrange("deferred reports", "transfers 2, 0, 1 in that order, and one dispatch to close");
        ParallelRun run = new(2, 0, true);
        List<string> written = [];
        run.DeferReport(2, () => Record(written, "2"));
        run.DeferReport(0, () => Record(written, "0"));
        run.DeferReport(1, () => Record(written, "1"));
        CountingConnectionPool pool = new();
        run.CloseAtEnd(new TransferDispatch(new ProtocolDispatcher([]), [], null, null, pool));

        await run.EndAsync();
        Diagnostics.Act("reports written", string.Join(",", written));
        Diagnostics.Act("pool disposals", pool.Disposals);

        Diagnostics.Assert("reports written", "0,1,2", string.Join(",", written));
        Diagnostics.Assert("pool disposals", 1, pool.Disposals);
        CollectionAssert.AreEqual(new[] { "0", "1", "2" }, written);
        Assert.AreEqual(1, pool.Disposals);
    }

    [TestMethod]
    public async Task EndAsync_NothingStarted_Ends()
    {
        Diagnostics.Arrange("run", "--parallel-max 1, nothing started");
        ParallelRun run = new(1, 0, true);

        await run.EndAsync();
        Diagnostics.Act("exit code", run.ExitCode);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, run.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, run.ExitCode);
    }

    [TestMethod]
    public async Task EndAsync_ATransferFaulted_Rethrows()
    {
        Diagnostics.Arrange("queue", "one transfer faulted with IOException \"broken\"");
        ParallelRun run = new(1, 0, true);
        run.Queue.Add(Task.FromException(new IOException("broken")));

        Diagnostics.Act("EndAsync", "awaited by Assert.ThrowsExactlyAsync");
        Diagnostics.Assert("exception", nameof(IOException), "checked by Assert.ThrowsExactlyAsync");
        await Assert.ThrowsExactlyAsync<IOException>(run.EndAsync);
    }

    [TestMethod]
    public void AbortedResult_IsCurlsAbortedTransfer()
    {
        Diagnostics.Arrange("result", nameof(ParallelRun.AbortedResult));

        TransferResult result = ParallelRun.AbortedResult;
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);

        Diagnostics.Assert("exit code", CurlExitCode.AbortedByCallback, result.ExitCode);
        Diagnostics.Assert("error message", "Transfer aborted due to critical error in another transfer", result.ErrorMessage);
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
