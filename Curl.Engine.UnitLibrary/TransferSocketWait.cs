using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;

namespace Curl.Engine;

/// <summary>
/// What a transfer in a multi handle driven by <c>curl_multi_socket_action</c> waits for on its socket and by when:
/// its reads and writes wait here until the multi handle signals the socket ready (<see cref="Signal" />), and it
/// publishes the direction it waits in (<see cref="Waiting" />) and its earliest deadline
/// (<see cref="EarliestDeadline" />), so the multi handle can give the application libcurl's socket and timer
/// callbacks (ADR-0914, BL-3847).
/// </summary>
/// <remarks>
/// A signal that comes while nothing waits in its direction is kept, and the next wait in that direction takes it
/// at once: the application's poll is level-triggered, so the socket is still ready. Each wait takes one signal,
/// so after a read the next read waits for the application to see the socket readable again.
/// </remarks>
/// <param name="timeProvider">The clock the deadlines are reckoned on.</param>
internal sealed class TransferSocketWait(TimeProvider timeProvider)
{
    /// <summary>The last socket number <see cref="TakeSocket" /> gave (<see cref="FirstEngineSocket" /> before any), shared by every transfer in the process.</summary>
    private static long nextEngineSocket = FirstEngineSocket;

    private readonly Lock sync = new();
    private TaskCompletionSource? readableWait;
    private TaskCompletionSource? writableWait;
    private CurlSocketSelect keptSignals;
    private bool connecting;
    private bool resolving;
    private bool socketIsResolveWakeup;
    private IDisposable? resolveWakeup;
    private bool connectSignalled;
    private TaskCompletionSource? failedConnectHold;
    private bool gatingStopped;
    private TimeSpan lowSpeedTime;
    private DateTimeOffset? connectDeadline;
    private DateTimeOffset? transferDeadline;
    private DateTimeOffset? lowSpeedDeadline;
    private DateTimeOffset? expectContinueDeadline;

    /// <summary>
    /// Raised after <see cref="Waiting" /> or <see cref="EarliestDeadline" /> may have changed, outside any lock.
    /// </summary>
    internal event Action? Changed;

    /// <summary>
    /// Gets the socket number the engine's own numbering for connections with no operating system socket (an
    /// in-memory one) starts from; the first such transfer is given <c>FirstEngineSocket + 1</c>, the next
    /// <c>FirstEngineSocket + 2</c> and so on. The range lies far above any descriptor or handle the operating
    /// system gives a process and apart from <see cref="ConnectOnlyConnection.FirstSocket" />'s numbers and
    /// above <see cref="ManagedMultiTransfer.ResolverWakeupSocket" /> (ADR-0955).
    /// </summary>
    internal const long FirstEngineSocket = 0x5000_0000;

    /// <summary>
    /// Gets what the transfer waits for on its socket: <see cref="CurlPoll.Out" /> while it connects or a write
    /// waits, <see cref="CurlPoll.In" /> while a read waits or a look-up runs (<see cref="BeginResolve" />), both when both do, and <see cref="CurlPoll.None" />
    /// while it waits on neither.
    /// </summary>
    internal CurlPoll Waiting
    {
        get
        {
            lock (sync)
            {
                CurlPoll output = (connecting && !connectSignalled) || writableWait is not null ? CurlPoll.Out : CurlPoll.None;
                return readableWait is null && !resolving ? output : output | CurlPoll.In;
            }
        }
    }

    /// <summary>
    /// Gets the earliest moment the transfer must be woken whether or not its socket is ready, or
    /// <see langword="null" /> when it has none: <c>CURLOPT_CONNECTTIMEOUT_MS</c> until <see cref="EndConnect" />,
    /// <c>CURLOPT_TIMEOUT_MS</c>, the low-speed time from the start of a wait, and the expect-100 timeout while
    /// <see cref="BeginExpectContinueWait" /> holds.
    /// </summary>
    internal DateTimeOffset? EarliestDeadline
    {
        get
        {
            lock (sync)
            {
                return Earlier(Earlier(connectDeadline, transferDeadline), Earlier(lowSpeedDeadline, expectContinueDeadline));
            }
        }
    }

    /// <summary>
    /// Gets the handle of the transfer's socket, or <see langword="null" /> until one is known: set as the dialer
    /// creates it (<see cref="ConnectingOn" />), so the application is told it while the connect is in progress.
    /// </summary>
    internal nint? Socket { get; private set; }

    /// <summary>Gets whether a dial is in progress (<see cref="BeginConnect" /> until <see cref="EndConnect" />).</summary>
    internal bool IsConnecting
    {
        get
        {
            lock (sync)
            {
                return connecting;
            }
        }
    }

    /// <summary>Gets whether the transfer waits in some direction on a socket it knows.</summary>
    internal bool IsWaitingOnItsSocket => Socket is not null && Waiting != CurlPoll.None;

    /// <summary>
    /// Gets whether the transfer waits in some direction with no socket known and no dial in progress: I/O over a
    /// connection with no operating system socket before <see cref="TakeSocket" /> numbers it.
    /// </summary>
    internal bool IsWaitingWithoutSocket => Socket is null && !IsConnecting && Waiting != CurlPoll.None;

    /// <summary>
    /// Gets whether the transfer waits in some direction on an engine socket number <see cref="TakeSocket" /> gave
    /// a connection with no operating system socket (above <see cref="FirstEngineSocket" />, ADR-0955).
    /// </summary>
    internal bool IsWaitingOnAnEngineSocket => Socket > FirstEngineSocket && Waiting != CurlPoll.None;

    /// <summary>
    /// Starts the transfer's clock: the connect deadline when <c>CURLOPT_CONNECTTIMEOUT_MS</c> is set, the
    /// whole-transfer deadline when <c>CURLOPT_TIMEOUT_MS</c> is set, as curl 8.22.0's <c>Curl_pretransfer</c> arms
    /// only those it is given (the default 300 seconds is the connector's to hold, never told to the timer callback,
    /// CGR-034 C_65), and the low-speed time each later wait counts when <c>CURLOPT_LOW_SPEED_LIMIT</c> and
    /// <c>CURLOPT_LOW_SPEED_TIME</c> are both set.
    /// </summary>
    /// <param name="options">The transfer's options.</param>
    internal void Start(EasyOptions options)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool limitsLowSpeed = options.Transfer.LowSpeedLimit > 0 && options.Transfer.LowSpeedTime > TimeSpan.Zero;
        lock (sync)
        {
            connectDeadline = options.Network.ConnectTimeout > TimeSpan.Zero ? now + options.Network.ConnectTimeout : null;
            transferDeadline = options.Transfer.Timeout > TimeSpan.Zero ? now + options.Transfer.Timeout : null;
            lowSpeedTime = limitsLowSpeed ? options.Transfer.LowSpeedTime : TimeSpan.Zero;
        }

        Changed?.Invoke();
    }

    /// <summary>Marks the transfer as connecting: it waits for its socket to be writable.</summary>
    internal void BeginConnect() => Update(() =>
    {
        connecting = true;
        connectSignalled = false;
    });

    /// <summary>
    /// Records <paramref name="socket" /> as the transfer's socket while it connects: the dialer created it and
    /// is about to connect it, so the transfer waits writable on it until a signal says the connect has an answer.
    /// </summary>
    /// <param name="socket">The handle of the socket being connected.</param>
    internal void ConnectingOn(nint socket) => Update(() =>
    {
        Socket = socket;
        socketIsResolveWakeup = false;
        connectSignalled = false;
    });

    /// <summary>
    /// Records the socket of the connection the transfer dialed, unless <see cref="Socket" /> is already known:
    /// <paramref name="socketHandle" /> when the connection has an operating system socket, otherwise the engine's
    /// next number from <see cref="FirstEngineSocket" />, so the application is told of a socket and an action
    /// naming it wakes the transfer (<see cref="SignalOn" />, ADR-0955).
    /// </summary>
    /// <param name="socketHandle">The connection's operating system socket (<c>IConnection.SocketHandle</c>), or <see langword="null" /> when it has none.</param>
    internal void TakeSocket(nint? socketHandle) => Update(() =>
    {
        Socket = socketIsResolveWakeup ? null : Socket;
        Socket ??= socketHandle ?? (nint)Interlocked.Increment(ref nextEngineSocket);
        socketIsResolveWakeup = false;
    });

    /// <summary>
    /// Marks the transfer as resolving its host: it waits readable on <paramref name="wakeupSocket" />, the socket the
    /// look-up makes readable as it ends, as curl 8.22.0 tells the socket callback its threaded resolver's wakeup
    /// socket with <c>CURL_POLL_IN</c>, so the application is brought back by a socket rather than a timer (BL-4036,
    /// ADR-0984). The wakeup stays the transfer's <see cref="Socket" /> until the dial gives it another; an action
    /// on it keeps no signal for the transfer's reads and writes. <paramref name="wakeup" /> is disposed when a
    /// later look-up replaces it or <see cref="StopGating" /> runs.
    /// </summary>
    /// <param name="wakeupSocket">The handle of the wakeup socket.</param>
    /// <param name="wakeup">What owns the wakeup socket.</param>
    internal void BeginResolve(nint wakeupSocket, IDisposable wakeup) => Update(() =>
    {
        if (gatingStopped)
        {
            wakeup.Dispose();
            return;
        }

        resolveWakeup?.Dispose();
        resolveWakeup = wakeup;
        Socket = wakeupSocket;
        socketIsResolveWakeup = true;
        resolving = true;
    });

    /// <summary>Marks the look-up as over, so the transfer no longer waits readable on its wakeup socket.</summary>
    internal void EndResolve() => Update(() => resolving = false);

    /// <summary>Marks the connect as over, so it no longer waits writable for it and the connect deadline ends.</summary>
    internal void EndConnect() => Update(() =>
    {
        connecting = false;
        connectDeadline = null;
    });

    /// <summary>Starts the expect-100 deadline: the request waits <paramref name="timeout" /> for <c>100 Continue</c>.</summary>
    /// <param name="timeout">How long it waits (<c>CURLOPT_EXPECT_100_TIMEOUT_MS</c>).</param>
    internal void BeginExpectContinueWait(TimeSpan timeout)
    {
        DateTimeOffset deadline = timeProvider.GetUtcNow() + timeout;
        Update(() => expectContinueDeadline = deadline);
    }

    /// <summary>Ends the expect-100 deadline: the answer came, or the body is sent without it.</summary>
    internal void EndExpectContinueWait() => Update(() => expectContinueDeadline = null);

    /// <summary>
    /// Signals the socket ready as the multi handle learned from <c>curl_multi_socket_action</c> or a perform:
    /// <see cref="CurlSocketSelect.In" /> lets a read go on, <see cref="CurlSocketSelect.Out" /> a write, and
    /// <see cref="CurlSocketSelect.Error" /> both, so the I/O meets the error. Either answers a connect in progress:
    /// it waits on nothing until the connect ends or the dialer starts another, so the multi handle lets it run on to
    /// its outcome, as libcurl's action finishes or fails the connect the poll woke it for (CGR-034 C_65).
    /// </summary>
    /// <param name="events">What the socket is ready for.</param>
    internal void Signal(CurlSocketSelect events)
    {
        bool readyIn = (events & (CurlSocketSelect.In | CurlSocketSelect.Error)) != 0;
        bool readyOut = (events & (CurlSocketSelect.Out | CurlSocketSelect.Error)) != 0;
        TaskCompletionSource? readable;
        TaskCompletionSource? writable;
        TaskCompletionSource? failedConnect;
        lock (sync)
        {
            readable = Release(ref readableWait, CurlSocketSelect.In, readyIn && !socketIsResolveWakeup);
            writable = Release(ref writableWait, CurlSocketSelect.Out, readyOut && !socketIsResolveWakeup);
            connectSignalled |= connecting && readyOut;
            failedConnect = TakeFailedConnectHold(readyOut);
        }

        readable?.TrySetResult();
        writable?.TrySetResult();
        failedConnect?.TrySetResult();
    }

    // Called under the lock: the failed connect's hold an Out or Error signal releases, taken so it is released once.
    private TaskCompletionSource? TakeFailedConnectHold(bool readyOut)
    {
        TaskCompletionSource? held = readyOut ? failedConnectHold : null;
        failedConnectHold = readyOut ? null : failedConnectHold;
        return held;
    }

    /// <summary>
    /// Holds a connect that failed, its socket still open, until the multi handle signals the socket writable or in
    /// error (<see cref="Signal" />), gating stops (<see cref="StopGating" />) or <paramref name="cancellationToken" />
    /// is cancelled: the dialer closes the socket once this ends, so the socket the socket callback was told stays a
    /// real one the application polls until the action that reads the failure removes it, as libcurl's does
    /// (CGR-034 C_65). A connect the multi handle signalled already, or gating stopped, is not held.
    /// </summary>
    /// <param name="cancellationToken">Ends the hold, as the transfer's end or its connect deadline does.</param>
    /// <returns>A task that completes when the dialer may close the socket.</returns>
    internal async ValueTask HoldFailedConnectAsync(CancellationToken cancellationToken)
    {
        Task noticed;
        lock (sync)
        {
            if (gatingStopped || connectSignalled)
            {
                return;
            }

            failedConnectHold ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            noticed = failedConnectHold.Task;
        }

        await noticed.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <summary>
    /// Stops holding the connection's reads and writes for a socket action, once the transfer that owned this wait
    /// has finished or left its multi handle: the waits now in progress return and every later one returns at once,
    /// so a kept connection closed by cleanup never waits for an action the application will not send (ADR-0973).
    /// </summary>
    internal void StopGating()
    {
        Update(() =>
        {
            gatingStopped = true;
            socketIsResolveWakeup = false;
            resolveWakeup?.Dispose();
            resolveWakeup = null;
        });
        Signal(CurlSocketSelect.In | CurlSocketSelect.Out);
    }

    /// <summary>
    /// Signals the socket ready (<see cref="Signal" />) when <paramref name="socket" /> is the transfer's
    /// <see cref="Socket" />, as the multi handle does for <c>curl_multi_socket_action</c> naming a socket.
    /// </summary>
    /// <param name="socket">The socket the action names.</param>
    /// <param name="events">What the socket is ready for.</param>
    /// <returns><see langword="true" /> when the socket is the transfer's and was signalled.</returns>
    internal bool SignalOn(nint socket, CurlSocketSelect events)
    {
        if (Socket != socket)
        {
            return false;
        }

        Signal(events);
        return true;
    }

    /// <summary>Waits until the multi handle signals the socket readable, or takes a signal kept for it.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the transfer may read.</returns>
    internal ValueTask WaitUntilReadableAsync(CancellationToken cancellationToken) =>
        WaitAsync(CurlSocketSelect.In, cancellationToken);

    /// <summary>Waits until the multi handle signals the socket writable, or takes a signal kept for it.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that completes when the transfer may write.</returns>
    internal ValueTask WaitUntilWritableAsync(CancellationToken cancellationToken) =>
        WaitAsync(CurlSocketSelect.Out, cancellationToken);

    private static DateTimeOffset? Earlier(DateTimeOffset? first, DateTimeOffset? second) =>
        (first, second) switch
        {
            (null, _) => second,
            (DateTimeOffset one, DateTimeOffset other) when other < one => other,
            _ => first,
        };

    private TaskCompletionSource? Release(ref TaskCompletionSource? wait, CurlSocketSelect direction, bool isReady)
    {
        if (!isReady)
        {
            return null;
        }

        TaskCompletionSource? released = wait;
        keptSignals = released is null ? keptSignals | direction : keptSignals;
        wait = null;
        return released;
    }

    private async ValueTask WaitAsync(CurlSocketSelect direction, CancellationToken cancellationToken)
    {
        TaskCompletionSource? wait = TakeKeptSignalOrStartWait(direction);
        if (wait is null)
        {
            return;
        }

        Changed?.Invoke();
        try
        {
            await wait.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Update(() => EndWait(wait));
        }
    }

    private TaskCompletionSource? TakeKeptSignalOrStartWait(CurlSocketSelect direction)
    {
        lock (sync)
        {
            if (gatingStopped || (keptSignals & direction) != 0)
            {
                keptSignals &= ~direction;
                return null;
            }

            TaskCompletionSource wait = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (direction == CurlSocketSelect.In)
            {
                readableWait = wait;
            }
            else
            {
                writableWait = wait;
            }

            lowSpeedDeadline = lowSpeedTime > TimeSpan.Zero ? timeProvider.GetUtcNow() + lowSpeedTime : null;
            return wait;
        }
    }

    private void EndWait(TaskCompletionSource wait)
    {
        readableWait = ReferenceEquals(readableWait, wait) ? null : readableWait;
        writableWait = ReferenceEquals(writableWait, wait) ? null : writableWait;
        lowSpeedDeadline = readableWait is null && writableWait is null ? null : lowSpeedDeadline;
    }

    private void Update(Action change)
    {
        lock (sync)
        {
            change();
        }

        Changed?.Invoke();
    }
}
