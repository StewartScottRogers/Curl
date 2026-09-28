using System.Globalization;

using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Holds one transfer attempt to <c>-m</c>/<c>--max-time</c> for every scheme, as libcurl
/// 8.21.0's multi loop does (ADR-0117, Decisions 2 and 3): it takes the attempt's start as
/// <see cref="OperationStarted" /> on the injected <see cref="TimeProvider" />, and once
/// <c>-m</c> has passed on that clock it sets <see cref="HasTimedOut" /> and cancels
/// <see cref="Token" />. A timer that fires before the limit has passed on the clock is set
/// again for the time left, so a handler that checks the clock after the cancellation always
/// finds its own limit passed. The progress sink it wraps tells it whether the attempt got past
/// its connect and how many body bytes it received, which <see cref="Failure" /> reports.
/// </summary>
public sealed class MaxTimeWatchdog : IDisposable
{
    private readonly Lock gate = new();

    private readonly TimeSpan maxTime;

    private readonly TimeProvider timeProvider;

    private readonly CancellationTokenSource timedOut = new();

    private readonly ITimer timer;

    private bool transferStarted;

    private long bytesReceived;

    private long? expectedTotal;

    private TimeSpan elapsedWhenTimedOut;

    private bool stopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaxTimeWatchdog" /> class, taking now as
    /// <see cref="OperationStarted" /> and starting the timer for <paramref name="maxTime" />.
    /// </summary>
    /// <param name="maxTime">The <c>-m</c> limit; more than zero.</param>
    /// <param name="timeProvider">The clock the limit is measured and timed on.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTime" /> is not positive.</exception>
    public MaxTimeWatchdog(TimeSpan maxTime, TimeProvider timeProvider)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTime, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.maxTime = maxTime;
        this.timeProvider = timeProvider;
        OperationStarted = timeProvider.GetTimestamp();
        timer = timeProvider.CreateTimer(_ => CheckMaxTime(), null, maxTime, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Gets the <see cref="TimeProvider" /> timestamp the attempt started at, which becomes the
    /// context's <see cref="ITransferContext.OperationStarted" />, so every limit a handler keeps
    /// counts from the same instant.
    /// </summary>
    public long OperationStarted { get; }

    /// <summary>Gets the token cancelled once <c>-m</c> has passed.</summary>
    public CancellationToken Token => timedOut.Token;

    /// <summary>Gets a value indicating whether <c>-m</c> passed before the watchdog was stopped.</summary>
    public bool HasTimedOut { get; private set; }

    /// <summary>
    /// Gets the failure curl 8.21.0 ends a timed-out attempt with, exit 28 carrying the body bytes
    /// received: <c>Operation timed out after N milliseconds with M bytes received</c> (or
    /// <c>with M out of T bytes received</c> when the last report carried an expected size) once
    /// the handler reported the transfer started, and <c>Connection timed out after N
    /// milliseconds</c> before that, N counted from <see cref="OperationStarted" /> to the moment
    /// the limit was found passed (measured 2026-09-28, BL-511 Notes).
    /// </summary>
    public TransferResult Failure
    {
        get
        {
            lock (gate)
            {
                long milliseconds = (long)elapsedWhenTimedOut.TotalMilliseconds;
                return TransferResult.Failure(
                    CurlExitCode.OperationTimedOut,
                    transferStarted ? OperationTimedOutMessage(milliseconds) : ConnectionTimedOutMessage(milliseconds),
                    bytesReceived);
            }
        }
    }

    /// <summary>
    /// Starts a watchdog for curl's <c>-m</c> as given on the command line: none, like
    /// <c>-m 0</c>, watches nothing.
    /// </summary>
    /// <param name="maxTime">The <c>-m</c> value, or <see langword="null" /> when not given.</param>
    /// <param name="timeProvider">The clock the limit is measured and timed on.</param>
    /// <returns>The started watchdog, or <see langword="null" /> when there is no limit.</returns>
    public static MaxTimeWatchdog? StartFromCommandLine(TimeSpan? maxTime, TimeProvider timeProvider) =>
        maxTime is { } limit && limit > TimeSpan.Zero ? new MaxTimeWatchdog(limit, timeProvider) : null;

    /// <summary>
    /// Wraps a transfer's progress sink so the watchdog learns whether the transfer got past its
    /// connect and how many body bytes it received; every report still reaches
    /// <paramref name="progress" />.
    /// </summary>
    /// <param name="progress">The attempt's progress sink.</param>
    /// <returns>The watching sink.</returns>
    public ITransferProgress WatchProgress(ITransferProgress progress) => new ReceivedWatchingProgress(progress, this);

    /// <summary>
    /// Stops the timer. <see cref="Token" />'s source is not disposed, for the reason
    /// <see cref="LowSpeedWatchdog.Dispose" /> gives.
    /// </summary>
    public void Dispose()
    {
        timer.Dispose();
        lock (gate)
        {
            stopped = true;
        }
    }

    private void CheckMaxTime()
    {
        if (HasJustTimedOut())
        {
            timedOut.Cancel();
        }
    }

    private bool HasJustTimedOut()
    {
        lock (gate)
        {
            if (stopped || HasTimedOut)
            {
                return false;
            }

            TimeSpan elapsed = timeProvider.GetElapsedTime(OperationStarted);
            if (elapsed < maxTime)
            {
                timer.Change(maxTime - elapsed, Timeout.InfiniteTimeSpan);
                return false;
            }

            elapsedWhenTimedOut = elapsed;
            HasTimedOut = true;
            return true;
        }
    }

    private string OperationTimedOutMessage(long milliseconds) =>
        expectedTotal is { } total
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Operation timed out after {milliseconds} milliseconds with {bytesReceived} out of {total} bytes received")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Operation timed out after {milliseconds} milliseconds with {bytesReceived} bytes received");

    private static string ConnectionTimedOutMessage(long milliseconds) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection timed out after {milliseconds} milliseconds");

    /// <summary>Passes every report through and keeps whether the transfer started and what it received.</summary>
    private sealed class ReceivedWatchingProgress(ITransferProgress inner, MaxTimeWatchdog watchdog) : ITransferProgress
    {
        public void ReportTransferStarted()
        {
            lock (watchdog.gate)
            {
                watchdog.transferStarted = true;
            }

            inner.ReportTransferStarted();
        }

        public void ReportDownloaded(long bytesSoFar, long? expectedTotal)
        {
            lock (watchdog.gate)
            {
                watchdog.bytesReceived = bytesSoFar;
                watchdog.expectedTotal = expectedTotal;
            }

            inner.ReportDownloaded(bytesSoFar, expectedTotal);
        }

        public void ReportUploaded(long bytesSoFar, long? expectedTotal) => inner.ReportUploaded(bytesSoFar, expectedTotal);

        public void ReportTransferDone() => inner.ReportTransferDone();
    }
}
