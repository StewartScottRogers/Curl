using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Holds an FTP transfer's connect phase - the greeting, <c>AUTH</c>, the login, <c>PBSZ</c>,
/// <c>PROT</c> and <c>PWD</c> - to <c>--connect-timeout</c>, counted from the start of the
/// request, before its TCP connect, as libcurl 8.21.0 holds every state before <c>DO</c> to
/// it. Once the limit has passed on the context's <see cref="TimeProvider" /> it cancels
/// <see cref="Token" />, and the transfer ends with <see cref="Failure" />: curl's
/// <c>Operation timed out</c> message, not its <c>Connection timed out</c>, because the TCP
/// connect is over (measured 2026-09-28, BL-512 Notes).
/// </summary>
/// <remarks>
/// A timer that fires before the limit has passed on the clock is set again for the time left,
/// as <c>Curl.Core</c>'s <c>MaxTimeWatchdog</c> does, so a cancellation always means the limit
/// has passed.
/// </remarks>
internal sealed class FtpConnectPhaseLimit : IDisposable
{
    /// <summary>
    /// curl's <c>DEFAULT_CONNECT_TIMEOUT</c>, the limit when <c>--connect-timeout</c> is not
    /// given or is 0.
    /// </summary>
    internal static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(300);

    private readonly TimeProvider timeProvider;

    private readonly long started;

    private readonly TimeSpan limit;

    private readonly CancellationTokenSource source;

    private readonly ITimer timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="FtpConnectPhaseLimit" /> class and starts
    /// its timer.
    /// </summary>
    /// <param name="context">The transfer, whose <c>--connect-timeout</c>, clock and token it takes.</param>
    /// <param name="started">The <see cref="TimeProvider.GetTimestamp" /> the request started at, before its TCP connect.</param>
    internal FtpConnectPhaseLimit(ITransferContext context, long started)
    {
        timeProvider = context.TimeProvider;
        this.started = started;
        limit = context.ConnectTimeout is { } given && given > TimeSpan.Zero ? given : DefaultConnectTimeout;
        source = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        timer = timeProvider.CreateTimer(_ => CancelOncePassed(), null, TimeLeft, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Gets the token cancelled with the transfer's, or once the limit has passed.</summary>
    internal CancellationToken Token => source.Token;

    /// <summary>Gets a value indicating whether the limit has passed on the clock.</summary>
    internal bool HasPassed => Elapsed >= limit;

    private TimeSpan Elapsed => timeProvider.GetElapsedTime(started);

    private TimeSpan TimeLeft => limit > Elapsed ? limit - Elapsed : TimeSpan.Zero;

    /// <summary>
    /// Gets the failure the transfer ends with once the limit has passed: exit 28 and
    /// <c>Operation timed out after N milliseconds with 0 bytes received</c>, N counted from
    /// the start of the request to now.
    /// </summary>
    /// <returns>The failure.</returns>
    internal TransferResult Failure() =>
        TransferResult.Failure(CurlExitCode.OperationTimedOut, FtpTransferMessages.OperationTimedOut((long)Elapsed.TotalMilliseconds));

    /// <summary>Stops the timer and releases the token's source.</summary>
    public void Dispose()
    {
        timer.Dispose();
        source.Dispose();
    }

    /// <summary>
    /// Cancels <see cref="Token" /> when the limit has passed on the clock, and sets the timer
    /// again for the time left when it fired early. The timer is not yet assigned when a clock
    /// fires it from inside <see cref="TimeProvider.CreateTimer" />.
    /// </summary>
    private void CancelOncePassed()
    {
        if (HasPassed)
        {
            source.Cancel();
            return;
        }

        timer?.Change(TimeLeft, Timeout.InfiniteTimeSpan);
    }
}
