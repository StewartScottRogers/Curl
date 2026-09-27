using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Tftp;

/// <summary>
/// The time limits of one TFTP transfer, counted from its start on
/// <see cref="ITransferContext.TimeProvider" />: the retry schedule each phase runs on,
/// the wait for a datagram until the next re-send is due, and the failure
/// <see cref="ITransferContext.MaxTime" /> passing ends the transfer with.
/// </summary>
/// <param name="context">The transfer being performed.</param>
/// <param name="startTimestamp">
/// The <see cref="ITransferContext.TimeProvider" /> timestamp the transfer started at,
/// which <see cref="ITransferContext.MaxTime" /> and the connect timeout count from.
/// </param>
/// <remarks>
/// A <see cref="ITransferContext.ConnectTimeout" /> or <see cref="ITransferContext.MaxTime" />
/// of zero or less is no limit, as curl treats <c>--connect-timeout 0</c> and <c>-m 0</c>.
/// </remarks>
internal sealed class TftpTimeLimits(ITransferContext context, long startTimestamp)
{
    /// <summary>The connect timeout curl applies when none is given.</summary>
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(300);

    private readonly TimeSpan? maxTime = Positive(context.MaxTime);

    /// <summary>
    /// Derives the schedule the request is sent on from the time left: the connect
    /// timeout, 300 seconds by default, or <see cref="ITransferContext.MaxTime" /> when
    /// that is sooner.
    /// </summary>
    /// <returns>The request phase's schedule.</returns>
    internal TftpRetrySchedule RequestSchedule() =>
        TftpRetrySchedule.ForTimeLeft(
            Min(Positive(context.ConnectTimeout) ?? DefaultConnectTimeout, maxTime) - Elapsed());

    /// <summary>
    /// Derives the schedule the transfer runs on once the server has answered, from the
    /// time <see cref="ITransferContext.MaxTime" /> leaves, as curl re-derives it then.
    /// </summary>
    /// <returns>The data phase's schedule.</returns>
    internal TftpRetrySchedule AnsweredSchedule() => TftpRetrySchedule.ForTimeLeft(maxTime - Elapsed());

    /// <summary>
    /// Gets how long the transfer has run.
    /// </summary>
    /// <returns>The time since the start timestamp.</returns>
    internal TimeSpan Elapsed() => context.TimeProvider.GetElapsedTime(startTimestamp);

    /// <summary>
    /// Waits for one datagram until <paramref name="resendAt" /> or the maximum time,
    /// whichever is sooner.
    /// </summary>
    /// <param name="channel">The channel to receive from.</param>
    /// <param name="buffer">Where the datagram is written.</param>
    /// <param name="resendAt">The elapsed time the next re-send is due at.</param>
    /// <returns>
    /// The datagram's length and source, or <see langword="null" /> when the deadline came
    /// first.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    internal async ValueTask<DatagramReceived?> ReceiveBeforeAsync(
        IDatagramChannel channel,
        Memory<byte> buffer,
        TimeSpan resendAt)
    {
        var wait = Min(resendAt, maxTime) - Elapsed();
        using var deadline = new CancellationTokenSource(
            TimeSpan.FromTicks(Math.Max(0, wait.Ticks)),
            context.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        try
        {
            return await channel.ReceiveAsync(buffer, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the failure the transfer ends with when <see cref="ITransferContext.MaxTime" />
    /// has passed.
    /// </summary>
    /// <param name="bytesReceived">The bytes received, which curl's message reports.</param>
    /// <param name="bytesTransferred">The bytes the result reports as transferred.</param>
    /// <returns>
    /// Exit 28 with <c>Operation timed out after N milliseconds with M bytes received</c>,
    /// or <see langword="null" /> when the maximum time has not passed or none is set.
    /// </returns>
    internal TransferResult? FailureIfMaxTimePassed(long bytesReceived, long bytesTransferred)
    {
        var elapsed = Elapsed();
        return elapsed >= maxTime
            ? TransferResult.Failure(
                CurlExitCode.OperationTimedOut,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Operation timed out after {(long)elapsed.TotalMilliseconds} milliseconds with {bytesReceived} bytes received"),
                bytesTransferred)
            : null;
    }

    private static TimeSpan? Positive(TimeSpan? limit) => limit > TimeSpan.Zero ? limit : null;

    private static TimeSpan Min(TimeSpan limit, TimeSpan? other) =>
        other < limit ? other.Value : limit;
}
