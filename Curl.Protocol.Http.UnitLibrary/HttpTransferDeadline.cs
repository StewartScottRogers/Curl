using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Holds one HTTP transfer to curl's two time limits on the transfer's
/// <see cref="ITransferContext.TimeProvider" />: <c>-m</c>/<c>--max-time</c> for the whole
/// transfer, and <c>--connect-timeout</c> (300 seconds when not given, or given as 0) for each
/// connect within it. <c>-m 0</c>, like no <c>-m</c>, sets no limit. <c>-m</c> is measured
/// from <see cref="ITransferContext.OperationStarted" />, so a redirect chain shares one
/// limit, or from the moment the deadline is created when that is not set; the connect
/// timeout always from the moment the deadline is created. A limit that passes cancels
/// <see cref="Token" /> (or the connect's token), which the handler turns into exit 28 with
/// the message curl 8.21.0 prints (measured, BL-174 Notes).
/// </summary>
internal sealed class HttpTransferDeadline : IDisposable
{
    /// <summary>
    /// The connect timeout when <c>--connect-timeout</c> is not given: curl's
    /// <c>DEFAULT_CONNECT_TIMEOUT</c> of 300 seconds.
    /// </summary>
    internal static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(300);

    private readonly TimeProvider timeProvider;

    private readonly long startedAt;

    private readonly long operationStartedAt;

    private readonly CancellationToken transferCancellation;

    private readonly TimeSpan connectTimeout;

    private readonly TimeSpan? maxTime;

    private readonly CancellationTokenSource? maxTimeElapsed;

    private readonly CancellationTokenSource transfer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpTransferDeadline" /> class, starting
    /// the clock now.
    /// </summary>
    /// <param name="context">The transfer whose limits, clock and cancellation are used.</param>
    internal HttpTransferDeadline(ITransferContext context)
    {
        timeProvider = context.TimeProvider;
        startedAt = timeProvider.GetTimestamp();
        operationStartedAt = context.OperationStarted ?? startedAt;
        transferCancellation = context.CancellationToken;
        connectTimeout = PositiveOrNull(context.ConnectTimeout) ?? DefaultConnectTimeout;
        maxTime = PositiveOrNull(context.MaxTime);
        maxTimeElapsed = MaxTimeElapsed(maxTime);
        transfer = maxTimeElapsed is null
            ? CancellationTokenSource.CreateLinkedTokenSource(transferCancellation)
            : CancellationTokenSource.CreateLinkedTokenSource(transferCancellation, maxTimeElapsed.Token);
    }

    /// <summary>
    /// Gives a limit that is set and above zero, or <see langword="null" /> for one that is
    /// not set or is zero or less, which curl takes as no limit.
    /// </summary>
    private static TimeSpan? PositiveOrNull(TimeSpan? limit) =>
        limit > TimeSpan.Zero ? limit : null;

    /// <summary>
    /// Gets the token that cancels the transfer: when the transfer is cancelled, or when
    /// <c>-m</c> passes.
    /// </summary>
    internal CancellationToken Token => transfer.Token;

    /// <summary>
    /// Gets a value indicating whether a cancelled read, write or connect was ended by a time
    /// limit: true unless the transfer itself was cancelled, the only other thing that cancels
    /// <see cref="Token" />, and true even then once <c>-m</c> has passed on the clock, so the
    /// runner's <c>-m</c> watchdog, which cancels the transfer at the same instant, cannot take
    /// the handler's measured message from it (ADR-0117, Decision 4).
    /// </summary>
    internal bool EndedByLimit => !transferCancellation.IsCancellationRequested || MaxTimePassed;

    /// <summary>Gets a value indicating whether <c>-m</c> is set and has passed on the clock.</summary>
    private bool MaxTimePassed => maxTime is { } limit && timeProvider.GetElapsedTime(operationStartedAt) >= limit;

    /// <summary>
    /// Gets how many whole milliseconds have passed since the deadline was created, as the
    /// connect timeout message prints it: curl counts it from the start of this request.
    /// </summary>
    internal long ElapsedMilliseconds => (long)timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;

    /// <summary>
    /// Gets how many whole milliseconds have passed since the operation started, as the
    /// operation timeout message prints it: curl counts it from the first request of a
    /// redirect chain (measured, BL-299 Notes).
    /// </summary>
    internal long OperationElapsedMilliseconds => (long)timeProvider.GetElapsedTime(operationStartedAt).TotalMilliseconds;

    /// <summary>
    /// Connects through <paramref name="connector" />, ending the connect with exit 28 and
    /// curl's <c>Connection timed out after N milliseconds</c> when the connect timeout or
    /// <c>-m</c> passes first.
    /// </summary>
    /// <param name="connector">The connector to open the connection with.</param>
    /// <param name="target">What to connect to.</param>
    /// <returns>The connector's result, or the exit 28 failure.</returns>
    /// <exception cref="OperationCanceledException">The transfer was cancelled.</exception>
    internal async ValueTask<ConnectResult> ConnectAsync(IConnector connector, ConnectTarget target)
    {
        using CancellationTokenSource connectElapsed = new(connectTimeout, timeProvider);
        using CancellationTokenSource connect = CancellationTokenSource.CreateLinkedTokenSource(Token, connectElapsed.Token);
        try
        {
            return await connector.ConnectAsync(target, connect.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (EndedByLimit)
        {
            return ConnectResult.Failed(CurlExitCode.OperationTimedOut, HttpTransferMessages.ConnectionTimedOut(ElapsedMilliseconds));
        }
    }

    private CancellationTokenSource? MaxTimeElapsed(TimeSpan? positiveMaxTime)
    {
        if (positiveMaxTime is not { } limit)
        {
            return null;
        }

        TimeSpan left = limit - timeProvider.GetElapsedTime(operationStartedAt, startedAt);
        return new CancellationTokenSource(left > TimeSpan.Zero ? left : TimeSpan.Zero, timeProvider);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        transfer.Dispose();
        maxTimeElapsed?.Dispose();
    }
}
