using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

/// <summary>
/// The managed <see cref="IMultiTransfer" /> (ADR-0506, ADR-0594): the easy transfers added to it run at
/// once, each its own <see cref="IEasyTransfer.PerformAsync" /> over the engine's shared connections.
/// <see cref="PerformAsync" /> starts every transfer added since the last call and queues a
/// <see cref="CurlMultiMessageType.Done" /> message, with the transfer's <see cref="CurlCode" />, for each
/// one that has finished; <see cref="TryReadInfo" /> takes them in the order they were queued.
/// <see cref="Remove" /> stops a running transfer and drops its unread message, and
/// <see cref="DisposeAsync" /> removes every transfer. <see cref="GetTimeout" />, <see cref="WaitAsync" />,
/// <see cref="PollAsync" /> and <see cref="Wakeup" /> run on the engine's <see cref="TimeProvider" />
/// (ADR-0597). <see cref="SocketActionAsync" /> moves the transfers on as <see cref="PerformAsync" /> does, and
/// both tell <see cref="MultiOptions.Socket" /> and <see cref="MultiOptions.Timer" /> what to wait for: each
/// running transfer is one engine-numbered socket, and while any runs the timer asks to be called back
/// every <see cref="RunningTransferCheckInterval" /> (ADR-0599). get_handles, waitfds, get_offt and
/// notifications (BL-2427) throw <see cref="NotSupportedException" /> until their task lands.
/// </summary>
/// <param name="owners">
/// The multi handle each of the engine's transfers is in, shared by all its multi handles, so a
/// transfer is in one at a time (<see cref="CurlMultiCode.AddedAlready" />).
/// </param>
/// <param name="timeProvider">The clock the timeout and the waits are measured on.</param>
/// <param name="socketPoller">Checks the extra sockets a wait or poll is given.</param>
internal sealed class ManagedMultiTransfer(
    ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer> owners,
    TimeProvider timeProvider,
    IWaitSocketPoller socketPoller) : IMultiTransfer
{
    private readonly List<MultiEntry> entries = [];
    private readonly List<CurlMultiMessage> messages = [];
    private readonly Lock wakeupLock = new();
    private TaskCompletionSource wakeupSignal = NewWakeupSignal();
    private nint nextTransferSocket = FirstTransferSocket;
    private long reportedTimerMilliseconds = -1;
    private bool abortedByCallback;
    private bool disposed;

    /// <summary>
    /// Gets the first socket number the handle gives a running transfer. The managed engine's
    /// connections are not operating system descriptors the application could wait on, so each running
    /// transfer is one number of the handle's own, above any descriptor a process is likely to hold.
    /// </summary>
    internal static nint FirstTransferSocket { get; } = 0x4000_0000;

    /// <summary>
    /// Gets how often a wait with extra sockets checks them again: the managed engine owns no
    /// operating system poll set to add them to, so it asks <see cref="IWaitSocketPoller" /> each interval.
    /// </summary>
    internal static TimeSpan SocketPollInterval { get; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Gets the longest time the timer callback is given while a transfer runs: a transfer's socket never
    /// becomes readable to the application's poll, so the timer is what brings it back to
    /// <see cref="SocketActionAsync" /> to see the transfer finish.
    /// </summary>
    internal static TimeSpan RunningTransferCheckInterval { get; } = TimeSpan.FromMilliseconds(10);

    /// <inheritdoc />
    public MultiOptions Options { get; set; } = new();

    /// <summary>
    /// <c>curl_multi_add_handle</c>: adds <paramref name="transfer" />, which starts on the next
    /// <see cref="PerformAsync" />.
    /// </summary>
    /// <param name="transfer">The transfer to add.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />; <see cref="CurlMultiCode.AddedAlready" /> when it is in a multi handle
    /// of the engine already, <see cref="CurlMultiCode.BadEasyHandle" /> for <see langword="null" /> and
    /// <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed. As libcurl does, it gives the
    /// timer callback 0 (act at once), and answers <see cref="CurlMultiCode.AbortedByCallback" /> when that
    /// returns -1.
    /// </returns>
    public CurlMultiCode Add(IEasyTransfer transfer)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        if (transfer is null)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        if (!owners.TryAdd(transfer, this))
        {
            return CurlMultiCode.AddedAlready;
        }

        entries.Add(new MultiEntry(transfer));
        return UpdateTimer(timerFired: false);
    }

    /// <summary>
    /// <c>curl_multi_remove_handle</c>: removes <paramref name="transfer" />, cancelling its perform if it
    /// still runs and dropping its unread message, as libcurl does; a socket the socket callback was told
    /// of is given <see cref="CurlPoll.Remove" /> first, and that call's answer is not checked.
    /// </summary>
    /// <param name="transfer">The transfer to remove.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />, also for a transfer in no multi handle;
    /// <see cref="CurlMultiCode.BadEasyHandle" /> for <see langword="null" /> or one in another multi handle,
    /// and <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.
    /// </returns>
    public CurlMultiCode Remove(IEasyTransfer transfer)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        if (transfer is null)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        if (!owners.TryGetValue(transfer, out ManagedMultiTransfer? owner))
        {
            return CurlMultiCode.Ok;
        }

        if (owner != this)
        {
            return CurlMultiCode.BadEasyHandle;
        }

        RemoveEntry(entries.Find(candidate => candidate.Transfer == transfer)!);
        return CurlMultiCode.Ok;
    }

    /// <summary>
    /// <c>curl_multi_perform</c>: starts every transfer added since the last call and queues a
    /// <see cref="CurlMultiMessageType.Done" /> message for each one that has finished since, without
    /// waiting for any. An exception a transfer's perform threw comes out of this call.
    /// </summary>
    /// <param name="cancellationToken">Stops the call before it starts anything.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the transfers not yet finished, or
    /// <see cref="CurlMultiCode.BadHandle" /> and 0 once this handle is disposed. It tells the socket and
    /// timer callbacks what changed as <see cref="SocketActionAsync" /> does.
    /// </returns>
    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken) =>
        MoveTransfersOnAsync(timerFired: false, cancellationToken);

    /// <summary>
    /// <c>curl_multi_socket_action</c>: moves every transfer on as <see cref="PerformAsync" /> does - the
    /// managed engine's transfers run on their own, so a socket's events add nothing to what is checked -
    /// then tells <see cref="MultiOptions.Socket" /> of each transfer that started (its socket and
    /// <see cref="CurlPoll.In" />) or finished (<see cref="CurlPoll.Remove" />, with the value
    /// <see cref="Assign" /> gave), and <see cref="MultiOptions.Timer" /> of the next timeout when it changed,
    /// or always after <see cref="CurlMultiSockets.Timeout" />, whose timer is spent. Once either callback
    /// returns -1, every unfinished transfer ends with <see cref="CurlCode.AbortedByCallback" /> on the
    /// next call, as libcurl's dead multi handle does.
    /// </summary>
    /// <param name="socket">The socket with the event, or <see cref="CurlMultiSockets.Timeout" /> for the timer.</param>
    /// <param name="events">The events on the socket; not read.</param>
    /// <param name="cancellationToken">Stops the call before it starts anything.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the transfers not yet finished;
    /// <see cref="CurlMultiCode.AbortedByCallback" /> when a callback returned -1, and
    /// <see cref="CurlMultiCode.BadHandle" /> and 0 once this handle is disposed.
    /// </returns>
    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) =>
        MoveTransfersOnAsync(socket == CurlMultiSockets.Timeout, cancellationToken);

    /// <summary>
    /// <c>curl_multi_assign</c>: the value the socket callback receives with <paramref name="socket" />
    /// from now on.
    /// </summary>
    /// <param name="socket">A socket the socket callback was told of and not yet told to remove.</param>
    /// <param name="socketState">The value, or <see langword="null" /> to clear it.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />; <see cref="CurlMultiCode.BadSocket" /> for any other socket and
    /// <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.
    /// </returns>
    public CurlMultiCode Assign(CurlSocket socket, object? socketState)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        MultiEntry? entry = entries.Find(candidate => candidate.Socket == socket);
        if (entry is null)
        {
            return CurlMultiCode.BadSocket;
        }

        entry.SocketState = socketState;
        return CurlMultiCode.Ok;
    }

    /// <summary><c>curl_multi_info_read</c>: takes the oldest queued message.</summary>
    /// <param name="message">The message, or <see langword="null" /> when none waits.</param>
    /// <param name="remainingMessages">The messages still queued after this one.</param>
    /// <returns><see langword="true" /> when a message was taken.</returns>
    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages)
    {
        if (messages.Count == 0)
        {
            message = null;
            remainingMessages = 0;
            return false;
        }

        message = messages[0];
        messages.RemoveAt(0);
        remainingMessages = messages.Count;
        return true;
    }

    /// <summary>
    /// <c>curl_multi_cleanup</c>: removes every transfer, cancelling the running ones, and waits for
    /// their performs to end; later calls answer <see cref="CurlMultiCode.BadHandle" />.
    /// </summary>
    /// <returns>A task that completes once every perform has ended.</returns>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (MultiEntry entry in entries)
        {
            entry.Cancellation.Cancel();
            owners.TryRemove(entry.Transfer, out _);
        }

        Task[] performs = [.. entries.Select(entry => entry.Perform).OfType<Task>()];
        entries.Clear();
        messages.Clear();
        await Task.WhenAll(performs).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <summary>
    /// <c>curl_multi_wait</c>: waits until a running transfer finishes, an extra socket has one of its
    /// events, the timeout passes, or the handle's own timeout (<see cref="GetTimeout" />) comes sooner;
    /// with no running transfer and no extra socket it returns at once, as libcurl does.
    /// </summary>
    /// <param name="extraSockets">More sockets to wait on; the wait sets each one's <see cref="CurlWaitSocket.ReturnedEvents" />.</param>
    /// <param name="timeout">The longest wait.</param>
    /// <param name="cancellationToken">Stops the wait with an <see cref="OperationCanceledException" />.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the finished transfers not yet reported by a perform plus the extra
    /// sockets with an event; <see cref="CurlMultiCode.BadFunctionArgument" /> for a negative timeout and
    /// <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.
    /// </returns>
    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitOrPollAsync(extraSockets, timeout, poll: false, cancellationToken);

    /// <summary>
    /// <c>curl_multi_poll</c>: as <see cref="WaitAsync" />, but with nothing to wait on it still waits the
    /// whole timeout, and <see cref="Wakeup" /> ends it.
    /// </summary>
    /// <param name="extraSockets">More sockets to wait on; the wait sets each one's <see cref="CurlWaitSocket.ReturnedEvents" />.</param>
    /// <param name="timeout">The longest wait.</param>
    /// <param name="cancellationToken">Stops the wait with an <see cref="OperationCanceledException" />.</param>
    /// <returns>As <see cref="WaitAsync" />; a wakeup returns <see cref="CurlMultiCode.Ok" /> and 0.</returns>
    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) =>
        WaitOrPollAsync(extraSockets, timeout, poll: true, cancellationToken);

    /// <summary>
    /// <c>curl_multi_wakeup</c>: ends a running <see cref="PollAsync" /> now, from any thread; with none
    /// running, the next <see cref="PollAsync" /> returns at once, as libcurl's wakeup socket does.
    /// </summary>
    /// <returns><see cref="CurlMultiCode.Ok" />, or <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.</returns>
    public CurlMultiCode Wakeup()
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        lock (wakeupLock)
        {
            wakeupSignal.TrySetResult();
        }

        return CurlMultiCode.Ok;
    }

    /// <summary>
    /// <c>curl_multi_timeout</c>: 0 while a perform has work (a transfer added and not started, or one
    /// finished and not yet reported); otherwise the milliseconds, rounded up, until the first running
    /// transfer's <c>CURLOPT_TIMEOUT</c> (<see cref="Api.Options.Behaviour.EasyTransferOptions.Timeout" />)
    /// expires, measured on the engine's <see cref="TimeProvider" /> from the perform that started it;
    /// -1 when no running transfer has one.
    /// </summary>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the milliseconds, or <see cref="CurlMultiCode.BadHandle" /> and -1
    /// once this handle is disposed.
    /// </returns>
    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout()
    {
        if (disposed)
        {
            return (CurlMultiCode.BadHandle, -1);
        }

        if (entries.Exists(HasWorkForPerform))
        {
            return (CurlMultiCode.Ok, 0);
        }

        return (CurlMultiCode.Ok, entries.Select(RemainingMilliseconds).Where(milliseconds => milliseconds >= 0).DefaultIfEmpty(-1).Min());
    }

    /// <summary>Not supported yet: <c>curl_multi_get_handles</c> is BL-2427's.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public IReadOnlyList<IEasyTransfer> GetHandles() => throw NotYet("curl_multi_get_handles", "BL-2427");

    /// <summary>Not supported yet: <c>curl_multi_waitfds</c> is BL-2427's.</summary>
    /// <param name="sockets">Unused.</param>
    /// <param name="socketCount">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount) => throw NotYet("curl_multi_waitfds", "BL-2427");

    /// <summary>Not supported yet: <c>curl_multi_get_offt</c> is BL-2427's.</summary>
    /// <param name="info">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info) => throw NotYet("curl_multi_get_offt", "BL-2427");

    /// <summary>Not supported yet: <c>curl_multi_notify_enable</c> is BL-2427's.</summary>
    /// <param name="notification">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public CurlMultiCode EnableNotification(CurlMultiNotification notification) => throw NotYet("curl_multi_notify_enable", "BL-2427");

    /// <summary>Not supported yet: <c>curl_multi_notify_disable</c> is BL-2427's.</summary>
    /// <param name="notification">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public CurlMultiCode DisableNotification(CurlMultiNotification notification) => throw NotYet("curl_multi_notify_disable", "BL-2427");

    private static NotSupportedException NotYet(string function, string task) => new($"{function} is not built yet ({task}).");

    private static TaskCompletionSource NewWakeupSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static bool HasWorkForPerform(MultiEntry entry) => (!entry.Done && entry.Perform is null) || IsFinishedUnreported(entry);

    private static bool IsFinishedUnreported(MultiEntry entry) => !entry.Done && entry.Perform is { IsCompleted: true };

    private void RemoveEntry(MultiEntry entry)
    {
        if (entry.Socket is { } socket)
        {
            Options.Socket?.Invoke(entry.Transfer, socket, CurlPoll.Remove, entry.SocketState);
        }

        entry.Cancellation.Cancel();
        entries.Remove(entry);
        messages.RemoveAll(message => message.Transfer == entry.Transfer);
        owners.TryRemove(entry.Transfer, out _);
    }

    private ValueTask<(CurlMultiCode Code, int RunningTransfers)> MoveTransfersOnAsync(bool timerFired, CancellationToken cancellationToken)
    {
        if (disposed)
        {
            return ValueTask.FromResult((CurlMultiCode.BadHandle, 0));
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (MultiEntry entry in entries)
        {
            MoveOn(entry);
        }

        CurlMultiCode code = UpdateSockets() ? UpdateTimer(timerFired) : CurlMultiCode.AbortedByCallback;
        return ValueTask.FromResult((code, entries.Count(entry => !entry.Done)));
    }

    private void MoveOn(MultiEntry entry)
    {
        if (entry.Done)
        {
            return;
        }

        if (abortedByCallback)
        {
            entry.Cancellation.Cancel();
            entry.Done = true;
            messages.Add(new CurlMultiMessage(CurlMultiMessageType.Done, entry.Transfer, CurlCode.AbortedByCallback));
            return;
        }

        entry.Perform ??= Start(entry);
        QueueDoneMessageWhenFinished(entry);
    }

    private bool UpdateSockets()
    {
        MultiSocketCallback? callback = Options.Socket;
        if (callback is null)
        {
            return true;
        }

        foreach (MultiEntry entry in entries)
        {
            if (TellSocketCallback(callback, entry) == -1)
            {
                abortedByCallback = true;
                return false;
            }
        }

        return true;
    }

    private int TellSocketCallback(MultiSocketCallback callback, MultiEntry entry)
    {
        if (!entry.Done && entry.Socket is null)
        {
            entry.Socket = new CurlSocket(nextTransferSocket++);
            return callback(entry.Transfer, entry.Socket.Value, CurlPoll.In, null);
        }

        if (entry.Done && entry.Socket is { } socket)
        {
            entry.Socket = null;
            return callback(entry.Transfer, socket, CurlPoll.Remove, entry.SocketState);
        }

        return 0;
    }

    private CurlMultiCode UpdateTimer(bool timerFired)
    {
        MultiTimerCallback? callback = Options.Timer;
        if (callback is null)
        {
            return CurlMultiCode.Ok;
        }

        long timer = TimerMilliseconds();
        if (timer == reportedTimerMilliseconds && !(timerFired && timer >= 0))
        {
            return CurlMultiCode.Ok;
        }

        reportedTimerMilliseconds = timer;
        if (callback(timer) != -1)
        {
            return CurlMultiCode.Ok;
        }

        abortedByCallback = true;
        return CurlMultiCode.AbortedByCallback;
    }

    private long TimerMilliseconds()
    {
        long handleTimeout = GetTimeout().TimeoutMilliseconds;
        if (!entries.Exists(entry => !entry.Done))
        {
            return handleTimeout;
        }

        long check = (long)RunningTransferCheckInterval.TotalMilliseconds;
        return handleTimeout < 0 ? check : Math.Min(handleTimeout, check);
    }

    private Task<CurlCode> Start(MultiEntry entry)
    {
        entry.StartTimestamp = timeProvider.GetTimestamp();
        return entry.Transfer.PerformAsync(entry.Cancellation.Token).AsTask();
    }

    private long RemainingMilliseconds(MultiEntry entry)
    {
        TimeSpan limit = entry.Transfer.Options.Transfer.Timeout;
        if (entry.Done || limit <= TimeSpan.Zero)
        {
            return -1;
        }

        TimeSpan left = limit - timeProvider.GetElapsedTime(entry.StartTimestamp);
        return Math.Max(0, (long)Math.Ceiling(left.TotalMilliseconds));
    }

    private async ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitOrPollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, bool poll, CancellationToken cancellationToken)
    {
        if (disposed)
        {
            return (CurlMultiCode.BadHandle, 0);
        }

        if (timeout < TimeSpan.Zero)
        {
            return (CurlMultiCode.BadFunctionArgument, 0);
        }

        long start = timeProvider.GetTimestamp();
        TimeSpan limit = WaitLimit(timeout);
        while (true)
        {
            int ready = MarkReadySockets(extraSockets.Span) + entries.Count(IsFinishedUnreported);
            TimeSpan left = limit - timeProvider.GetElapsedTime(start);
            if (ready > 0 || WaitEnds(left, extraSockets.Length, poll))
            {
                return (CurlMultiCode.Ok, ready);
            }

            await WaitForEventAsync(left, extraSockets.Length > 0, poll, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan WaitLimit(TimeSpan timeout)
    {
        long handleTimeout = GetTimeout().TimeoutMilliseconds;
        return handleTimeout >= 0 && handleTimeout < timeout.TotalMilliseconds ? TimeSpan.FromMilliseconds(handleTimeout) : timeout;
    }

    private int MarkReadySockets(Span<CurlWaitSocket> sockets)
    {
        int ready = 0;
        for (int index = 0; index < sockets.Length; index++)
        {
            CurlWaitEvents returned = socketPoller.Poll(sockets[index].Socket, sockets[index].Events);
            sockets[index] = sockets[index] with { ReturnedEvents = returned };
            ready += returned == CurlWaitEvents.None ? 0 : 1;
        }

        return ready;
    }

    private bool WaitEnds(TimeSpan left, int extraSocketCount, bool poll)
    {
        if (poll)
        {
            return TakeWakeup() || left <= TimeSpan.Zero;
        }

        // Every transfer has started by now (one not started makes the handle's timeout 0), so one
        // not done is one still running.
        return left <= TimeSpan.Zero || (extraSocketCount == 0 && entries.TrueForAll(entry => entry.Done));
    }

    private bool TakeWakeup()
    {
        lock (wakeupLock)
        {
            if (!wakeupSignal.Task.IsCompleted)
            {
                return false;
            }

            wakeupSignal = NewWakeupSignal();
            return true;
        }
    }

    private async Task WaitForEventAsync(TimeSpan left, bool checkSockets, bool poll, CancellationToken cancellationToken)
    {
        using CancellationTokenSource stopDelay = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TimeSpan delay = checkSockets && left > SocketPollInterval ? SocketPollInterval : left;
        List<Task> events = [.. entries.Select(entry => entry.Perform).OfType<Task>().Where(perform => !perform.IsCompleted)];
        events.Add(Task.Delay(TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, int.MaxValue)), timeProvider, stopDelay.Token));
        if (poll)
        {
            events.Add(wakeupSignal.Task);
        }

        await Task.WhenAny(events).ConfigureAwait(false);
        await stopDelay.CancelAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void QueueDoneMessageWhenFinished(MultiEntry entry)
    {
        if (!entry.Perform!.IsCompleted)
        {
            return;
        }

        entry.Done = true;
        messages.Add(new CurlMultiMessage(CurlMultiMessageType.Done, entry.Transfer, entry.Perform.GetAwaiter().GetResult()));
    }

    /// <summary>
    /// One added transfer: its perform once started and when, whether its message was queued, and the
    /// socket the socket callback was told of with the value <see cref="Assign" /> gave it.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    private sealed class MultiEntry(IEasyTransfer transfer)
    {
        public IEasyTransfer Transfer { get; } = transfer;

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<CurlCode>? Perform { get; set; }

        public long StartTimestamp { get; set; }

        public bool Done { get; set; }

        public CurlSocket? Socket { get; set; }

        public object? SocketState { get; set; }
    }
}
