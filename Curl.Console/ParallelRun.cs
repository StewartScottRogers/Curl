using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The state of one <c>-Z</c> run (ADR-0127): the queue that keeps up to <c>--parallel-max</c>
/// transfers running, the first failure in completion order, which is the run's exit code, and what
/// <c>--fail-early</c> leaves to report once every running transfer has ended. Its members are called
/// holding the run's <see cref="WriteGate" />, or from the one flow that starts the transfers.
/// </summary>
/// <param name="maxRunning">The <c>--parallel-max</c> limit.</param>
/// <param name="maxPerHost">The <c>--parallel-max-host</c> limit; zero for none.</param>
/// <param name="parallelImmediate">Whether <c>--parallel-immediate</c> was given.</param>
internal sealed class ParallelRun(int maxRunning, int maxPerHost, bool parallelImmediate)
{
    /// <summary>
    /// The message of a transfer <c>--fail-early</c> aborted because another failed, with exit
    /// <see cref="CurlExitCode.AbortedByCallback" />, as curl 8.21.0 prints it (ADR-0127, rows 11 and 12).
    /// </summary>
    internal const string AbortedMessage = "Transfer aborted due to critical error in another transfer";

    /// <summary>The result every transfer aborted by <c>--fail-early</c> ends with.</summary>
    internal static readonly TransferResult AbortedResult = TransferResult.Failure(CurlExitCode.AbortedByCallback, AbortedMessage);

    /// <summary>Cancelled by the first failure under <c>--fail-early</c>, aborting every running transfer.</summary>
    private readonly CancellationTokenSource abort = new();

    /// <summary>The reports left for the end of the run, with the transfer number they are ordered by.</summary>
    private readonly List<(long TransferId, Func<Task> Report)> deferredReports = [];

    /// <summary>The option groups' dispatches, closed once every transfer has ended.</summary>
    private readonly List<TransferDispatch> dispatches = [];

    /// <summary>Gets the queue the run's transfers wait in for a free slot.</summary>
    internal ParallelTransferQueue Queue { get; } = new(maxRunning);

    /// <summary>Gets the queue the run's transfers wait in, holding their slot, for a busy host.</summary>
    internal ParallelHostQueue Hosts { get; } = new(maxPerHost, parallelImmediate);

    /// <summary>
    /// Gets or sets the run's combined progress meter (BL-521), or <see langword="null" /> when the run
    /// shows none.
    /// </summary>
    internal ParallelProgressMeter? ProgressMeter { get; set; }

    /// <summary>Gets the token every transfer of the run is aborted through.</summary>
    internal CancellationToken AbortToken => abort.Token;

    /// <summary>Gets a value indicating whether <c>--fail-early</c> has aborted the run.</summary>
    internal bool IsAborted => abort.IsCancellationRequested;

    /// <summary>
    /// Gets a value indicating whether a result that ends the run without <c>--fail-early</c>, such as
    /// a <c>-D</c> file that cannot be opened or a bad glob, has stopped any further transfer starting.
    /// </summary>
    internal bool HasEnded { get; private set; }

    /// <summary>Gets the first failure, in completion order; <see langword="null" /> while none has failed.</summary>
    internal TransferResult? FirstFailure { get; private set; }

    /// <summary>
    /// Gets the run's exit code: the first failure's, in completion order, or
    /// <see cref="CurlExitCode.Ok" /> when none failed (ADR-0127, decision 3).
    /// </summary>
    internal CurlExitCode ExitCode => FirstFailure?.ExitCode ?? CurlExitCode.Ok;

    /// <summary>
    /// Records how a transfer ended: a failure becomes the first failure unless one came before it, and
    /// a result that ends the run aborts it under <c>--fail-early</c> and otherwise stops further starts.
    /// </summary>
    /// <param name="result">The transfer's result.</param>
    /// <param name="endsTheRun">Whether the result ends the run.</param>
    /// <param name="failEarly">Whether <c>--fail-early</c> was given.</param>
    internal void RecordEnd(TransferResult result, bool endsTheRun, bool failEarly)
    {
        if (!result.IsSuccess)
        {
            FirstFailure ??= result;
        }

        if (!endsTheRun)
        {
            return;
        }

        if (failEarly)
        {
            abort.Cancel();
        }
        else
        {
            HasEnded = true;
        }
    }

    /// <summary>
    /// Leaves a transfer's report for <see cref="EndAsync" />, which writes the deferred reports in
    /// command-line order.
    /// </summary>
    /// <param name="transferId">The transfer's <c>%{xfer_id}</c>, which orders the reports.</param>
    /// <param name="report">Writes the transfer's failure lines and <c>-w</c> output.</param>
    internal void DeferReport(long transferId, Func<Task> report)
    {
        lock (deferredReports)
        {
            deferredReports.Add((transferId, report));
        }
    }

    /// <summary>
    /// Waits until fewer than <c>--parallel-max</c> transfers are running; when none is free, the runner
    /// has started every transfer it can for now, and the meter draws when due, as curl's loop does
    /// once it has added what it can.
    /// </summary>
    /// <returns>A task that completes when a transfer may start.</returns>
    internal Task WaitForFreeSlotAsync()
    {
        if (!Queue.HasFreeSlot)
        {
            ProgressMeter?.DrawIfDue();
        }

        return Queue.WaitForFreeSlotAsync();
    }

    /// <summary>Keeps an option group's dispatch open until <see cref="EndAsync" />.</summary>
    /// <param name="dispatch">The dispatch.</param>
    internal void CloseAtEnd(TransferDispatch dispatch) => dispatches.Add(dispatch);

    /// <summary>
    /// Waits for every started transfer to end, writes the deferred reports in command-line order and
    /// the meter's final line, then closes the option groups' dispatches. Every transfer has been
    /// started when it is called, so the meter first draws when due.
    /// </summary>
    /// <returns>A task that completes when the run has ended.</returns>
    /// <remarks>
    /// A transfer that faulted, which no handler failure does, ends the run with its exception at
    /// once, as it does without <c>-Z</c>; the meter stops without a final line, and the dispatches
    /// are left to the process's exit.
    /// </remarks>
    internal async Task EndAsync()
    {
        using ParallelProgressMeter? meter = ProgressMeter;
        meter?.DrawIfDue();
        await Queue.WhenAllEndedAsync().ConfigureAwait(false);
        foreach ((_, Func<Task> report) in deferredReports.OrderBy(deferred => deferred.TransferId))
        {
            await report().ConfigureAwait(false);
        }

        meter?.DrawFinal();

        foreach (TransferDispatch dispatch in dispatches)
        {
            await dispatch.DisposeAsync().ConfigureAwait(false);
        }

        abort.Dispose();
    }
}
