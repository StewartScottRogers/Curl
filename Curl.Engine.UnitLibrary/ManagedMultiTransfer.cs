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
/// (ADR-0597). The socket-action API (BL-2426), and get_handles, waitfds, get_offt and notifications
/// (BL-2427) throw <see cref="NotSupportedException" /> until their tasks land.
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
    private bool disposed;

    /// <summary>
    /// Gets how often a wait with extra sockets checks them again: the managed engine owns no
    /// operating system poll set to add them to, so it asks <see cref="IWaitSocketPoller" /> each interval.
    /// </summary>
    internal static TimeSpan SocketPollInterval { get; } = TimeSpan.FromMilliseconds(10);

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
    /// <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.
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
        return CurlMultiCode.Ok;
    }

    /// <summary>
    /// <c>curl_multi_remove_handle</c>: removes <paramref name="transfer" />, cancelling its perform if it
    /// still runs and dropping its unread message, as libcurl does.
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

        MultiEntry entry = entries.Find(candidate => candidate.Transfer == transfer)!;
        entry.Cancellation.Cancel();
        entries.Remove(entry);
        messages.RemoveAll(message => message.Transfer == transfer);
        owners.TryRemove(transfer, out _);
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
    /// <see cref="CurlMultiCode.BadHandle" /> and 0 once this handle is disposed.
    /// </returns>
    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken)
    {
        if (disposed)
        {
            return ValueTask.FromResult((CurlMultiCode.BadHandle, 0));
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach (MultiEntry entry in entries)
        {
            entry.Perform ??= Start(entry);
            QueueDoneMessageWhenFinished(entry);
        }

        return ValueTask.FromResult((CurlMultiCode.Ok, entries.Count(entry => !entry.Done)));
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

    /// <summary>Not supported yet: <c>curl_multi_socket_action</c> is BL-2426's.</summary>
    /// <param name="socket">Unused.</param>
    /// <param name="events">Unused.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) => throw NotYet("curl_multi_socket_action", "BL-2426");

    /// <summary>Not supported yet: <c>curl_multi_assign</c> is BL-2426's.</summary>
    /// <param name="socket">Unused.</param>
    /// <param name="socketState">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public CurlMultiCode Assign(CurlSocket socket, object? socketState) => throw NotYet("curl_multi_assign", "BL-2426");

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

    private static bool HasWorkForPerform(MultiEntry entry) => entry.Perform is null || IsFinishedUnreported(entry);

    private static bool IsFinishedUnreported(MultiEntry entry) => !entry.Done && entry.Perform is { IsCompleted: true };

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
        if (entry.Done || !entry.Perform!.IsCompleted)
        {
            return;
        }

        entry.Done = true;
        messages.Add(new CurlMultiMessage(CurlMultiMessageType.Done, entry.Transfer, entry.Perform.GetAwaiter().GetResult()));
    }

    /// <summary>One added transfer: its perform once started and when, and whether its message was queued.</summary>
    /// <param name="transfer">The transfer.</param>
    private sealed class MultiEntry(IEasyTransfer transfer)
    {
        public IEasyTransfer Transfer { get; } = transfer;

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<CurlCode>? Perform { get; set; }

        public long StartTimestamp { get; set; }

        public bool Done { get; set; }
    }
}
