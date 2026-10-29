using Curl.Api.Options.Behaviour;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Performs one attempt of a transfer, following its redirects under the <c>-Y</c>/<c>-y</c> and
/// <c>-m</c> watchdogs - what libcurl does inside one <c>curl_easy_perform</c> - and numbers the
/// run's transfers and connections, as libcurl gives each its <c>%{xfer_id}</c> and
/// <c>%{conn_id}</c>. The <c>--retry</c> loop around it stays in <c>CurlCommandRunner</c>,
/// as retrying is the curl tool's work (<c>tool_operate.c</c>), not libcurl's (ADR-0506, BL-2282).
/// </summary>
/// <param name="timeProvider">The clock the watchdogs and the serial attempt start are read on.</param>
/// <param name="easyErrorText">
/// Curl's easy error text for each exit code, which the known-hosts and cookie-line failures carry:
/// <c>CurlEasyErrorText.Of</c> in production, which lives in <c>Libcurl/Misc/ErrorText</c> and
/// which <c>Curl.Console</c> passes in (BL-2291, BL-2450).
/// </param>
internal sealed partial class TransferPerformer(TimeProvider timeProvider, Func<CurlExitCode, string> easyErrorText)
{
    /// <summary>The <c>%{conn_id}</c> the next transfer that connects takes, counted per run from zero.</summary>
    private long nextConnectionId;

    /// <summary>
    /// The <c>%{xfer_id}</c> the next transfer takes, counted per run from zero, one for every URL
    /// a glob expands to.
    /// </summary>
    private long nextTransferId;

    /// <summary>Gets the <c>%{xfer_id}</c> the next transfer will take, without taking it.</summary>
    internal long NextTransferId => nextTransferId;

    /// <summary>
    /// Gets when the run's last serial transfer started - its last <c>--retry</c> attempt's start, set by
    /// <see cref="RecordSerialAttemptStart" /> - as a <see cref="TimeProvider.GetTimestamp" />, which
    /// <c>--rate</c> measures from; <see langword="null" /> before the first.
    /// </summary>
    internal long? SerialTransferStart { get; private set; }

    /// <summary>
    /// Gives the performer a runner was made with, or, when it was given none, a new one on the
    /// runner's clock (the system's when it was given none either).
    /// </summary>
    /// <param name="performer">The performer the runner was given, or <see langword="null" />.</param>
    /// <param name="timeProvider">The clock the runner was given, or <see langword="null" />.</param>
    /// <param name="easyErrorText">Curl's easy error text for each exit code, for a new performer.</param>
    /// <returns>The performer the runner uses.</returns>
    internal static TransferPerformer GivenOrOnClock(TransferPerformer? performer, TimeProvider? timeProvider, Func<CurlExitCode, string> easyErrorText) =>
        performer ?? new TransferPerformer(timeProvider ?? TimeProvider.System, easyErrorText);

    /// <summary>Gives a performer on the same clock and error text whose <c>%{xfer_id}</c> and <c>%{conn_id}</c> count from 0, as a new libcurl easy or multi handle's do (CGR-247).</summary>
    /// <returns>The new performer.</returns>
    internal TransferPerformer StartingAtZero() => new(timeProvider, easyErrorText);

    /// <summary>Takes the next <c>%{xfer_id}</c>.</summary>
    /// <returns>The transfer's number.</returns>
    internal long TakeTransferId() => nextTransferId++;

    /// <summary>Takes the next <c>%{conn_id}</c>.</summary>
    /// <returns>The connection's number.</returns>
    internal long TakeConnectionId() => nextConnectionId++;

    /// <summary>
    /// Moves <see cref="SerialTransferStart" /> to now as a serial transfer's attempt starts, after any
    /// <c>--retry</c> wait, so <c>--rate</c> measures from the last attempt's start, as curl 8.21.0's
    /// <c>serial_transfers</c> resets its start on every retry: 503 then 200 with <c>--retry-delay 1</c> and
    /// 300 ms answers printed <c>Note: Transfer took 315 ms</c> (measured 2026-10-02, BL-971 Notes). Under
    /// <c>-Z</c>, where <c>--rate</c> waits for nothing, it is left alone.
    /// </summary>
    /// <param name="runsInParallel">Whether the run is a <c>-Z</c> one.</param>
    internal void RecordSerialAttemptStart(bool runsInParallel)
    {
        if (!runsInParallel)
        {
            SerialTransferStart = timeProvider.GetTimestamp();
        }
    }

    /// <summary>
    /// Numbers the attempt <c>--retry</c> is about to run as a transfer of its own, as curl 8.21.0
    /// does: it takes the next <c>%{xfer_id}</c>, and the retried attempt keeps the <c>%{conn_id}</c>
    /// it connected with, so the next attempt takes a new one unless it reuses that connection
    /// (<see cref="KeepRetriedConnectionIdWhenReused" />). Against 503, 503, 200 on connections the
    /// server closed, curl printed <c>xfer_id</c> 2 and <c>conn_id</c> 2 (measured 2026-09-29, BL-799 Notes).
    /// </summary>
    /// <param name="state">The running transfer.</param>
    internal void TakeNextAttemptIds(EngineTransferState state)
    {
        state.RetriedConnectionId = state.ConnectionId ?? nextConnectionId++;
        state.ConnectionId = null;
        state.RetryTransferId = nextTransferId++;
    }

    /// <summary>
    /// Gives a <c>--retry</c> attempt that reused the retried attempt's connection that attempt's
    /// <c>%{conn_id}</c>, as curl 8.21.0 printed <c>conn_id</c> 0 after three attempts over one
    /// kept-alive connection (measured 2026-09-29, BL-799 Notes). Only an <c>http</c> or
    /// <c>https</c> attempt says whether it opened a connection (<see cref="TransferReport.ConnectionCount" />);
    /// any other is taken to have opened one.
    /// </summary>
    /// <param name="state">The running transfer.</param>
    /// <param name="context">The attempt's context.</param>
    /// <param name="attempt">The attempt's result.</param>
    internal static void KeepRetriedConnectionIdWhenReused(EngineTransferState state, TransferContext context, TransferResult attempt)
    {
        if (state.RetriedConnectionId is { } retriedConnectionId && ReportsNoConnectionOpened(context, attempt))
        {
            state.ConnectionId = retriedConnectionId;
        }
    }

    /// <summary>
    /// Creates the context of an attempt <c>--retry</c> runs again, on fresh progress started now,
    /// once the retry wait is over, so its meter counts from the attempt's start as curl 8.21.0's
    /// does: after a one-second wait curl drew no <c>00:01</c> status line for a 6-byte body,
    /// which progress started before the wait did (measured 2026-10-09, BL-1849 Notes).
    /// </summary>
    /// <param name="startProgress">Starts the attempt's fresh progress.</param>
    /// <param name="createAttemptContext">Creates an attempt's context on the transfer's current progress.</param>
    /// <param name="resumeFrom">The <c>-C</c> offset the attempt resumes from, or <see langword="null" />.</param>
    /// <returns>The attempt's context.</returns>
    internal static TransferContext CreateRetryAttemptContext(
        Action startProgress,
        Func<long?, TransferContext> createAttemptContext,
        long? resumeFrom)
    {
        startProgress();

        return createAttemptContext(resumeFrom);
    }

    /// <summary>
    /// Starts the <c>-Y</c>/<c>-y</c> watchdog for the attempt whose context is being created, on
    /// the performer's clock, and keeps it on <paramref name="state" /> for
    /// <see cref="FollowWatchingSpeedAsync" />, watching for <see cref="EasyTransferOptions.LowSpeedTime" />
    /// below <see cref="EasyTransferOptions.LowSpeedLimit" /> (<c>-Y</c> alone gives 30 seconds and <c>-y</c>
    /// alone 1 byte per second, as curl 8.21.0 does, measured 2026-09-27, BL-400 Notes).
    /// </summary>
    /// <param name="state">The running transfer.</param>
    /// <param name="transfer">The transfer's options, whose low-speed limit and time are <c>-Y</c> and <c>-y</c>.</param>
    /// <param name="diagnosticLog">Where the watchdog writes.</param>
    /// <returns>The started watchdog, or <see langword="null" /> when the limit or the time is zero, so the speed is not watched.</returns>
    internal LowSpeedWatchdog? StartLowSpeedWatchdog(EngineTransferState state, EasyTransferOptions transfer, IDiagnosticLog diagnosticLog) =>
        state.AttemptLowSpeedWatchdog = LowSpeedWatchdog.StartFromCommandLine(
            transfer.LowSpeedLimit, (long)transfer.LowSpeedTime.TotalSeconds, timeProvider, diagnosticLog);

    /// <summary>
    /// Starts the <c>-m</c> watchdog for the attempt whose context is being created, on the
    /// performer's clock, and keeps it on <paramref name="state" /> for
    /// <see cref="FollowWatchingSpeedAsync" /> (ADR-0117): each <c>--retry</c> attempt gets a fresh
    /// <c>-m</c>, as in curl 8.21.0.
    /// </summary>
    /// <param name="state">The running transfer.</param>
    /// <param name="transfer">The transfer's options, whose <see cref="EasyTransferOptions.Timeout" /> is <c>-m</c>, zero when not given.</param>
    /// <param name="diagnosticLog">Where the watchdog writes.</param>
    /// <returns>The started watchdog, or <see langword="null" /> without a positive <c>-m</c>.</returns>
    internal MaxTimeWatchdog? StartMaxTimeWatchdog(EngineTransferState state, EasyTransferOptions transfer, IDiagnosticLog diagnosticLog) =>
        state.AttemptMaxTimeWatchdog = MaxTimeWatchdog.StartFromCommandLine(transfer.Timeout, timeProvider, diagnosticLog);

    /// <summary>
    /// Performs one attempt through <paramref name="follower" /> under the <c>-Y</c>/<c>-y</c> and
    /// <c>-m</c> watchdogs its context was created with, stopping both when the attempt ends. An
    /// attempt either watchdog cancelled ends with that watchdog's exit 28 failure, which
    /// <c>--retry</c> counts as transient.
    /// </summary>
    /// <param name="state">The running transfer, holding the attempt's watchdogs.</param>
    /// <param name="follower">Performs the attempt, following redirects under <c>-L</c>.</param>
    /// <param name="context">The attempt's context.</param>
    /// <param name="redirectPolicy">The <c>-L</c> policy.</param>
    /// <returns>The attempt's result, or a watchdog's failure.</returns>
    internal static async Task<TransferResult> FollowWatchingSpeedAsync(
        EngineTransferState state,
        RedirectFollower follower,
        TransferContext context,
        RedirectPolicy redirectPolicy)
    {
        using LowSpeedWatchdog? watchdog = state.AttemptLowSpeedWatchdog;
        using MaxTimeWatchdog? maxTimeWatchdog = state.AttemptMaxTimeWatchdog;
        state.AttemptLowSpeedWatchdog = null;
        state.AttemptMaxTimeWatchdog = null;
        try
        {
            return await follower.FollowAsync(context, redirectPolicy).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (watchdog is { IsTooSlow: true })
        {
            return watchdog.Failure;
        }
        catch (OperationCanceledException) when (maxTimeWatchdog is { HasTimedOut: true })
        {
            return maxTimeWatchdog.Failure;
        }
    }

    /// <summary>
    /// Tells whether an attempt said it opened no connection: an <c>http</c> or <c>https</c> one
    /// whose report counts none.
    /// </summary>
    /// <param name="context">The attempt's context.</param>
    /// <param name="attempt">The attempt's result.</param>
    /// <returns><see langword="true" /> when the attempt reused a connection.</returns>
    private static bool ReportsNoConnectionOpened(TransferContext context, TransferResult attempt) =>
        context.Url.Scheme is "http" or "https" && attempt.Report is { ConnectionCount: 0 };
}
