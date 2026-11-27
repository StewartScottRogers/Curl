using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

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
    /// Connects through <paramref name="connector" />, ending the connect with exit 28 when the
    /// connect timeout or <c>-m</c> passes first. A connector that returns its own exit 28 result
    /// has reported it, so that result is returned; otherwise the failure is curl's
    /// <c>Connection timed out after N milliseconds</c>, N counted from the start of this request,
    /// also reported once on the target's events as a <c>-v</c> line (ADR-1459).
    /// </summary>
    /// <param name="connector">The connector to open the connection with.</param>
    /// <param name="target">What to connect to.</param>
    /// <param name="abandoned">
    /// Cancels this connect alone, when a racing connect has won (ADR-0144 section 4); the
    /// connect then ends as the exit 28 failure, which the race discards, and no line is reported.
    /// </param>
    /// <returns>The connector's result, or the exit 28 failure.</returns>
    /// <exception cref="OperationCanceledException">The transfer was cancelled.</exception>
    internal ValueTask<ConnectResult> ConnectAsync(IConnector connector, ConnectTarget target, CancellationToken abandoned = default) =>
        WithinConnectTimeoutAsync(token => connector.ConnectAsync(target, token), target.Events, abandoned);

    /// <summary>
    /// Connects over QUIC through <paramref name="connector" />
    /// as the session <c>openSession</c> builds over it, which the connector may share with other
    /// transfers (<see cref="IConnector.ConnectMultiplexedSessionAsync" />, BL-735), under the same limits as
    /// <see cref="ConnectAsync" /> and with the same exit 28 failure (ADR-0144 section 7).
    /// </summary>
    /// <param name="connector">The connector to open the connection with.</param>
    /// <param name="target">What to connect to.</param>
    /// <param name="abandoned">
    /// Cancels this connect alone, when a racing connect has won (ADR-0144 section 4); the
    /// connect then ends as the exit 28 failure, which the race discards.
    /// </param>
    /// <param name="openSession">Builds the session over a new QUIC connection.</param>
    /// <returns>The connector's result, or the exit 28 failure.</returns>
    /// <exception cref="OperationCanceledException">The transfer was cancelled.</exception>
    internal ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(
        IConnector connector,
        ConnectTarget target,
        Func<IMultiplexedConnection, IConnection> openSession,
        CancellationToken abandoned = default) =>
        WithinConnectTimeoutAsync(
            token => connector.ConnectMultiplexedSessionAsync(target, openSession, token),
            target.Events,
            abandoned);

    private async ValueTask<ConnectResult> WithinConnectTimeoutAsync(
        Func<CancellationToken, ValueTask<ConnectResult>> connectAsync,
        ITransferEvents events,
        CancellationToken abandoned)
    {
        using CancellationTokenSource connectElapsed = new(connectTimeout, timeProvider);
        using CancellationTokenSource connect = CancellationTokenSource.CreateLinkedTokenSource(Token, connectElapsed.Token, abandoned);
        Task<ConnectResult> connecting = connectAsync(connect.Token).AsTask();
        try
        {
            // WaitAsync ends the wait at the limit even when the connector does not honour its token (BL-5660).
            // An abandoned connect (its own token) is awaited here until it ends, so the race disposes what it opens.
            // At the limit the connect is left running: DisposeLateConnectionAsync disposes the connection it may
            // still open and observes its fault.
            using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(Token, connectElapsed.Token);
            return await connecting.WaitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (EndedByLimit)
        {
            _ = DisposeLateConnectionAsync(connecting);
            (ConnectResult? timedOutByConnector, ConnectTimings? timings) = await WhatTheEndedConnectLeftAsync(connecting).ConfigureAwait(false);
            return timedOutByConnector ?? TimedOut(timings, events, abandoned.IsCancellationRequested);
        }
    }

    // The exit 28 failure for a connect the connector did not report. An abandoned connect lost a race, so it gets no -v line.
    private ConnectResult TimedOut(ConnectTimings? timings, ITransferEvents events, bool isAbandoned)
    {
        string message = HttpTransferMessages.ConnectionTimedOut(ElapsedMilliseconds);
        if (!isAbandoned)
        {
            events.ReportInfo(message);
        }

        return ConnectResult.Failed(CurlExitCode.OperationTimedOut, message, timings);
    }

    /// <summary>
    /// What the connect the limit ended left behind: the connector's own exit 28 result, whose message it has already
    /// reported, so the <c>-v</c> line and <c>curl: (28)</c> carry one N (ADR-1459); else the timings it took before
    /// the limit, so the failure still reports the name lookup it did (STD-145).
    /// A connector that honours its token finishes within <see cref="ConnectorUnwindGrace" />; one that does not leaves nothing.
    /// </summary>
    private static async ValueTask<(ConnectResult? TimedOutByConnector, ConnectTimings? Timings)> WhatTheEndedConnectLeftAsync(Task<ConnectResult> connecting)
    {
        // Real time on purpose: the limit has passed on the transfer's clock already, and this only waits for the connector's cancellation to unwind.
        await Task.WhenAny(connecting, Task.Delay(ConnectorUnwindGrace, TimeProvider.System)).ConfigureAwait(false);
        if (!connecting.IsCompleted)
        {
            return default;
        }

        try
        {
            ConnectResult ended = await connecting.ConfigureAwait(false);
            return (ended.ExitCode == CurlExitCode.OperationTimedOut ? ended : null, ended.Timings);
        }
        catch (OperationCanceledException canceled)
        {
            return (null, canceled.Data[ConnectTimings.CarriedOnCancellationKey] as ConnectTimings);
        }
        catch (Exception)
        {
            return default;
        }
    }

    private static readonly TimeSpan ConnectorUnwindGrace = TimeSpan.FromMilliseconds(100);

    internal static async Task DisposeLateConnectionAsync(Task<ConnectResult> connecting)
    {
        try
        {
            ConnectResult late = await connecting.ConfigureAwait(false);
            if (late.Connection is not null)
            {
                await late.Connection.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // The transfer already ended as exit 28; a late fault has nobody to report to.
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
