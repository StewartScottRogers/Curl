using Curl.Engine;
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
/// <param name="timeProvider">The clock the <c>--fail-early</c> abort delay runs on; <see cref="TimeProvider.System" /> when not given.</param>
internal sealed class ParallelRun(int maxRunning, int maxPerHost, bool parallelImmediate, TimeProvider? timeProvider = null)
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

    /// <summary>The abort delay's timer on the run's clock, started by the first failure that ends a <c>--fail-early</c> run.</summary>
    private ITimer? abortTimer;

    /// <summary>Cancelled at once by the first failure under <c>--fail-early</c>, so no waiting transfer starts.</summary>
    private readonly CancellationTokenSource stopStarts = new();

    /// <summary>The reports left for the end of the run, with the transfer number they are ordered by.</summary>
    private readonly List<(long TransferId, Func<Task> Report)> deferredReports = [];

    /// <summary>The option groups' dispatches, closed once every transfer has ended.</summary>
    private readonly List<TransferDispatch> dispatches = [];

    /// <summary>The queue for a free slot, built on first use so that it can start <see cref="Hosts" />' freed transfers.</summary>
    private ParallelTransferQueue? queue;

    /// <summary>
    /// The transfers of the run's first batch set up so far, held unstarted until the batch is complete:
    /// each one's start, and its report should a later setup of the batch fail (CGR-170, BL-3699).
    /// </summary>
    private readonly List<(Func<Task<bool>> Start, Func<TransferResult, Task> Abandon)> heldTransfers = [];

    /// <summary>
    /// How many more transfers the run's first batch sets up before any starts: curl 8.22.0 sets up
    /// <c>--parallel-max</c> plus one before its first <c>curl_multi_perform</c> (BL-3699 Notes).
    /// </summary>
    private int setupsLeftInFirstBatch = maxRunning + 1;

    /// <summary>
    /// Gets the queue the run's transfers wait in for a free slot; before each wait it starts the transfers
    /// <see cref="Hosts" /> freed.
    /// </summary>
    internal ParallelTransferQueue Queue => queue ??= new(maxRunning) { BeforeEachWait = () => Hosts.StartReleased() };

    /// <summary>Gets the queue the run's transfers wait in, holding their slot, for a busy host.</summary>
    internal ParallelHostQueue Hosts { get; } = new(maxPerHost, parallelImmediate);

    /// <summary>
    /// Gets or sets the run's combined progress meter (BL-521), or <see langword="null" /> when the run
    /// shows none.
    /// </summary>
    internal ParallelProgressMeter? ProgressMeter { get; set; }

    /// <summary>
    /// Gets the multi handle the run's transfers perform through (ADR-0628), cleaned up by <see cref="EndAsync" />;
    /// <see langword="null" /> for a run built without one, as the tests of the run alone build it.
    /// </summary>
    internal ParallelTransferMulti? Transfers { get; init; }

    /// <summary>Gets the token every transfer of the run is aborted through.</summary>
    internal CancellationToken AbortToken => abort.Token;

    /// <summary>
    /// How long after the first failure under <c>--fail-early</c> the running transfers are aborted: curl
    /// 8.22.0 checks for the abort about once a second, so a transfer whose response arrives first completes
    /// (CGR-294).
    /// </summary>
    internal static readonly TimeSpan AbortDelay = TimeSpan.FromSeconds(1);

    /// <summary>Gets a value indicating whether <c>--fail-early</c> has aborted the running transfers.</summary>
    internal bool IsAborted => abort.IsCancellationRequested;

    /// <summary>Gets a value indicating whether <c>--fail-early</c> has seen its first failure, so no transfer starts.</summary>
    internal bool HasFailedEarly => stopStarts.IsCancellationRequested;

    /// <summary>Gets the token that ends a wait to start, cancelled at the first failure under <c>--fail-early</c>.</summary>
    internal CancellationToken StopStartsToken => stopStarts.Token;

    /// <summary>
    /// Gets a value indicating whether a result that ends the run without <c>--fail-early</c>, such as
    /// a <c>-D</c> file that cannot be opened or a bad glob, has stopped any further transfer starting.
    /// </summary>
    internal bool HasEnded { get; private set; }

    /// <summary>Gets the first failure, in completion order; <see langword="null" /> while none has failed.</summary>
    internal TransferResult? FirstFailure { get; private set; }

    /// <summary>
    /// Gets the last failure to set a transfer up (a bad glob, a <c>-T</c> file that cannot be opened),
    /// whose code is the run's whatever failed before it, as in curl 8.22.0 (BL-3699 Notes);
    /// <see langword="null" /> while none has.
    /// </summary>
    internal TransferResult? SetupFailure { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the run is still setting up its first batch, whose transfers are
    /// held unstarted (<see cref="HoldInFirstBatch" />).
    /// </summary>
    internal bool HoldsFirstBatch => setupsLeftInFirstBatch > 0;

    /// <summary>
    /// Gets the run's exit code: the last setup failure's, else the first failure's, in completion order,
    /// or <see cref="CurlExitCode.Ok" /> when none failed (ADR-0127, decision 3; BL-3699).
    /// </summary>
    internal CurlExitCode ExitCode => (SetupFailure ?? FirstFailure)?.ExitCode ?? CurlExitCode.Ok;

    /// <summary>
    /// Holds a transfer of the first batch unstarted until <see cref="StartHeldAsync" /> or
    /// <see cref="AbandonFirstBatchAsync" />.
    /// </summary>
    /// <param name="start">Starts the transfer; <see langword="true" /> when the run has ended.</param>
    /// <param name="abandon">Reports the transfer with the setup failure that abandoned the batch.</param>
    /// <returns><see langword="true" /> when the batch is now complete.</returns>
    internal bool HoldInFirstBatch(Func<Task<bool>> start, Func<TransferResult, Task> abandon)
    {
        heldTransfers.Add((start, abandon));
        return --setupsLeftInFirstBatch == 0;
    }

    /// <summary>
    /// Ends the first batch and starts its held transfers in order; once one finds the run ended, every
    /// later one finds it ended too and starts nothing, as <see cref="HasEnded" /> never clears.
    /// </summary>
    /// <returns><see langword="true" /> when the run has ended.</returns>
    internal async Task<bool> StartHeldAsync()
    {
        setupsLeftInFirstBatch = 0;
        (Func<Task<bool>> Start, Func<TransferResult, Task> Abandon)[] held = [.. heldTransfers];
        heldTransfers.Clear();
        bool hasEnded = false;
        foreach ((Func<Task<bool>> start, _) in held)
        {
            hasEnded = await start().ConfigureAwait(false);
        }

        return hasEnded;
    }

    /// <summary>
    /// Ends the run at a setup failure in its first batch, as curl 8.22.0 abandons the batch: no held
    /// transfer starts, and each is reported, in command-line order, with the failure's code
    /// (CGR-170, BL-3699).
    /// </summary>
    /// <param name="failure">The setup failure.</param>
    /// <returns>A task that completes when the held transfers are reported.</returns>
    internal async Task AbandonFirstBatchAsync(TransferResult failure)
    {
        setupsLeftInFirstBatch = 0;
        RecordSetupFailure(failure, endsTheRun: true);
        foreach ((_, Func<TransferResult, Task> abandon) in heldTransfers)
        {
            await abandon(failure).ConfigureAwait(false);
        }

        heldTransfers.Clear();
    }

    /// <summary>
    /// Records a failure to set a transfer up, whose code becomes the run's (<see cref="SetupFailure" />).
    /// </summary>
    /// <param name="failure">The setup failure.</param>
    /// <param name="endsTheRun">Whether it stops any further transfer starting.</param>
    internal void RecordSetupFailure(TransferResult failure, bool endsTheRun)
    {
        SetupFailure = failure;
        HasEnded |= endsTheRun;
    }

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
            FailEarly();
        }
        else
        {
            HasEnded = true;
        }
    }

    /// <summary>
    /// Stops further starts at once and starts the <see cref="AbortDelay" /> on the runs clock, on the first
    /// failure that ends a <c>--fail-early</c> run only, so a later failure does not move the abort.
    /// </summary>
    private void FailEarly()
    {
        if (HasFailedEarly)
        {
            return;
        }

        stopStarts.Cancel();
        abortTimer = (timeProvider ?? TimeProvider.System).CreateTimer(_ => abort.Cancel(), null, AbortDelay, Timeout.InfiniteTimeSpan);
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
    /// the meter's final line, then cleans up the multi handle and closes the option groups' dispatches. Every transfer has been
    /// started when it is called, so the meter first draws when due. A run that started no transfer,
    /// one a bad glob or a refused <c>-T</c> upload ended first, draws no meter at all, as curl 8.21.0
    /// draws none (measured 2026-10-07, BL-1683).
    /// </summary>
    /// <returns>A task that completes when the run has ended.</returns>
    /// <remarks>
    /// A transfer that faulted, which no handler failure does, ends the run with its exception at
    /// once, as it does without <c>-Z</c>; the meter stops without a final line, and the dispatches
    /// are left to the process's exit.
    /// </remarks>
    internal async Task EndAsync()
    {
        using ParallelProgressMeter? ownedMeter = ProgressMeter;
        ParallelProgressMeter? meter = Queue.HasStartedAny ? ownedMeter : null;
        meter?.DrawIfDue();
        await Queue.WhenAllEndedAsync().ConfigureAwait(false);
        await WriteDeferredReportsAsync().ConfigureAwait(false);
        meter?.DrawFinal();
        if (Transfers is { } transfers)
        {
            await transfers.DisposeAsync().ConfigureAwait(false);
        }

        await CloseDispatchesAsync().ConfigureAwait(false);
        abortTimer?.Dispose();
        abort.Dispose();
    }

    /// <summary>Writes the deferred reports in command-line order.</summary>
    /// <returns>A task that completes when every report is written.</returns>
    private async Task WriteDeferredReportsAsync()
    {
        foreach ((_, Func<Task> report) in deferredReports.OrderBy(deferred => deferred.TransferId))
        {
            await report().ConfigureAwait(false);
        }
    }

    /// <summary>Closes the option groups' dispatches.</summary>
    /// <returns>A task that completes when every dispatch is closed.</returns>
    private async Task CloseDispatchesAsync()
    {
        foreach (TransferDispatch dispatch in dispatches)
        {
            await dispatch.DisposeAsync().ConfigureAwait(false);
        }
    }
}
