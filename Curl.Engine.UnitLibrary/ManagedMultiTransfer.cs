using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

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
/// every <see cref="RunningTransferCheckInterval" /> (ADR-0599). <see cref="GetHandles" />,
/// <see cref="GetWaitSockets" />, <see cref="GetInfo" />, the notifications and
/// <c>CURLMOPT_NETWORK_CHANGED</c> follow curl 8.22.0 (ADR-0604). A transfer that would open a connection
/// past <see cref="MultiOptions.MaxHostConnections" /> or <see cref="MultiOptions.MaxTotalConnections" />
/// waits, pending, until a running one finishes; under <see cref="CurlPipelining.Multiplex" /> transfers to
/// one origin that can multiplex share a connection, <see cref="MultiOptions.MaxConcurrentStreams" /> at a
/// time, and <see cref="OfferPush" /> answers a server push through <see cref="MultiOptions.Push" /> (ADR-0605).
/// </summary>
/// <param name="owners">
/// The multi handle each of the engine's transfers is in, shared by all its multi handles, so a
/// transfer is in one at a time (<see cref="CurlMultiCode.AddedAlready" />).
/// </param>
/// <param name="timeProvider">The clock the timeout and the waits are measured on.</param>
/// <param name="socketPoller">Checks the extra sockets a wait or poll is given.</param>
/// <param name="closeIdleConnections">Closes the engine's idle connections, for <see cref="CurlNetworkChange.ClearConnections" /> (<see cref="ConnectionCache.CloseIdleAsync" />).</param>
/// <param name="closeIdleConnectionsBeyond">
/// Closes the engine's oldest idle connections beyond a count, after each transfer ends, for
/// <see cref="MultiOptions.MaxConnects" /> (<see cref="ConnectionCache.CloseIdleBeyondAsync" />).
/// </param>
/// <param name="clearDnsCache">Empties the engine's DNS cache, for <see cref="CurlNetworkChange.ClearDns" /> (<see cref="DnsCache.Clear" />).</param>
internal sealed class ManagedMultiTransfer(
    ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer> owners,
    TimeProvider timeProvider,
    IWaitSocketPoller socketPoller,
    Func<ValueTask> closeIdleConnections,
    Func<long, ValueTask> closeIdleConnectionsBeyond,
    Action clearDnsCache) : IMultiTransfer
{
    private readonly List<MultiEntry> entries = [];
    private readonly List<CurlMultiMessage> messages = [];
    private readonly List<(CurlMultiNotification Notification, IEasyTransfer Transfer)> pendingNotifications = [];
    private readonly Lock wakeupLock = new();
    private readonly MultiCallbackQueue callbackQueue = new();
    private Task idleConnectionsClosing = Task.CompletedTask;
    private MultiOptions options = new();
    private uint enabledNotifications;
    private long transfersAdded;
    private TaskCompletionSource wakeupSignal = NewWakeupSignal();
    private nint nextTransferSocket = FirstTransferSocket;
    private long reportedTimerMilliseconds = -1;
    private bool abortedByCallback;
    private bool disposed;

    // The multi handle whose transfer's perform the current code runs in, or null outside every perform.
    private static readonly AsyncLocal<ManagedMultiTransfer?> PerformingHandle = new();

    /// <inheritdoc />
    public bool IsInTransferCallback => PerformingHandle.Value == this;

    /// <summary>
    /// Gets the first socket number the handle gives a running transfer. The managed engine's
    /// connections are not operating system descriptors the application could wait on, so each running
    /// transfer is one number of the handle's own, above any descriptor a process is likely to hold.
    /// </summary>
    internal static nint FirstTransferSocket { get; } = 0x4000_0000;

    /// <summary>
    /// Gets the socket number <see cref="GetWaitSockets" /> gives after the running transfers' sockets while any
    /// transfer runs: the wakeup descriptor of libcurl's threaded resolver, which <c>curl_version_info</c>'s
    /// <c>AsynchDNS</c> without c-ares says this build has (<see cref="ManagedCurlVersion" />), just below
    /// <see cref="FirstTransferSocket" />.
    /// </summary>
    internal static nint ResolverWakeupSocket { get; } = 0x3FFF_FFFF;

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

    /// <summary>
    /// Gets the streams per multiplexed connection when <see cref="MultiOptions.MaxConcurrentStreams" /> is
    /// below 1 or past <see cref="int.MaxValue" />, as libcurl's <c>CURLMOPT_MAX_CONCURRENT_STREAMS</c> falls back.
    /// </summary>
    internal static long DefaultConcurrentStreams { get; } = 100;

    /// <summary>
    /// Gets the connections the engine keeps for each transfer in the handle when <see cref="MultiOptions.MaxConnects" />
    /// is 0 or less, as libcurl's <c>CURLMOPT_MAXCONNECTS</c> default does.
    /// </summary>
    internal static long DefaultConnectsPerTransfer { get; } = 4;

    /// <summary>
    /// Gets or sets every option of the handle (<c>curl_multi_setopt</c>). <c>CURLMOPT_NETWORK_CHANGED</c>
    /// is an action, not a setting, as in libcurl: a value whose <see cref="MultiOptions.NetworkChanged" />
    /// is not <see cref="CurlNetworkChange.None" /> closes the engine's idle connections
    /// (<see cref="CurlNetworkChange.ClearConnections" />) and empties its DNS cache
    /// (<see cref="CurlNetworkChange.ClearDns" />) at once, and is kept with it set back to
    /// <see cref="CurlNetworkChange.None" />, so a later <see langword="with" /> on it clears nothing again.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null" />.</exception>
    public MultiOptions Options
    {
        get => options;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.NetworkChanged == CurlNetworkChange.None)
            {
                options = value;
                return;
            }

            ClearForNetworkChange(value.NetworkChanged);
            options = value with { NetworkChanged = CurlNetworkChange.None };
        }
    }

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
    /// returns -1. Called from inside one of its own transfers' callbacks (<see cref="IsInTransferCallback" />)
    /// it adds nothing and answers <see cref="CurlMultiCode.RecursiveApiCall" />, as libcurl does (CGR-146).
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

        // Before the added-already check: curl 8.22.0 refuses a handle already in the multi handle with 8 here too.
        if (IsInTransferCallback)
        {
            return CurlMultiCode.RecursiveApiCall;
        }

        return owners.TryAdd(transfer, this) ? AddEntry(transfer) : CurlMultiCode.AddedAlready;
    }

    // Adds a transfer this handle now owns; also the engine's own add of an HTTP/2 pushed transfer, which
    // runs inside a transfer and so skips the recursive-call check.
    private CurlMultiCode AddEntry(IEasyTransfer transfer)
    {
        entries.Add(new MultiEntry(transfer));
        transfersAdded++;
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
    /// and <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed; called from inside one of its
    /// own transfers' callbacks it removes nothing and answers <see cref="CurlMultiCode.RecursiveApiCall" /> (CGR-146).
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

        if (IsInTransferCallback)
        {
            return CurlMultiCode.RecursiveApiCall;
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
    /// <returns>
    /// <see langword="true" /> when a message was taken; <see langword="false" /> from inside one of the handle's
    /// own transfers' callbacks, where libcurl's call answers no message (CGR-146).
    /// </returns>
    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages)
    {
        if (messages.Count == 0 || IsInTransferCallback)
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
    /// their performs to end; later calls answer <see cref="CurlMultiCode.BadHandle" />. Called from inside
    /// one of its own transfers (<see cref="IsInTransferCallback" />) it does nothing, as libcurl refuses
    /// that call, since waiting for the perform it runs in would never end.
    /// </summary>
    /// <returns>A task that completes once every perform has ended.</returns>
    public async ValueTask DisposeAsync()
    {
        if (disposed || IsInTransferCallback)
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
        await idleConnectionsClosing.ConfigureAwait(false);
    }

    /// <summary>
    /// <c>curl_multi_wait</c>: waits until a running transfer finishes, an extra socket has one of its
    /// events, the timeout passes, or the handle's own timeout (<see cref="GetTimeout" />) comes sooner -
    /// counting only a transfer timeout still ahead, since one already passed ends its transfer, which ends
    /// the wait (<see cref="WaitTimerMilliseconds" />); with no running transfer and no extra socket it
    /// returns at once, as libcurl does.
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

        return IsInTransferCallback ? (CurlMultiCode.RecursiveApiCall, -1) : (CurlMultiCode.Ok, TimeoutMilliseconds());
    }

    // GetTimeout's milliseconds for a live handle.
    private long TimeoutMilliseconds()
    {
        if (entries.Exists(HasWorkForPerform) || callbackQueue.HasQueued)
        {
            return 0;
        }

        return entries.Select(RemainingMilliseconds).Where(milliseconds => milliseconds >= 0).DefaultIfEmpty(-1).Min();
    }

    /// <summary>
    /// <c>curl_multi_get_handles</c>: every transfer added and not yet removed, in the order added.
    /// </summary>
    /// <returns>The transfers; none once this handle is disposed, as libcurl answers a bad handle with no list.</returns>
    public IReadOnlyList<IEasyTransfer> GetHandles() => [.. entries.Select(entry => entry.Transfer)];

    /// <summary>
    /// <c>curl_multi_waitfds</c>: one <see cref="CurlWaitEvents.PollIn" /> socket for each transfer whose
    /// perform has started and whose message is not yet queued, in the order added (but none for one that
    /// waits for an earlier transfer's connection, <see cref="WaitsForEarlierConnection" />) - the engine-numbered
    /// socket the socket callback is told of for the same transfer (<see cref="FirstTransferSocket" />) -
    /// then, while there is any, the threaded resolver's <see cref="ResolverWakeupSocket" />.
    /// </summary>
    /// <param name="sockets">Filled with up to its length of sockets, each with no returned events.</param>
    /// <param name="socketCount">How many sockets the handle waits on, even past <paramref name="sockets" />' length.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />; <see cref="CurlMultiCode.OutOfMemory" /> when <paramref name="sockets" />
    /// is too short, as libcurl answers, and <see cref="CurlMultiCode.BadHandle" /> with a count of 0 once
    /// this handle is disposed.
    /// </returns>
    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount)
    {
        socketCount = 0;
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        List<MultiEntry> running = entries.FindAll(entry => !entry.Done && entry.Perform is not null);
        List<CurlSocket> waiting = [.. running.Where((entry, index) => !WaitsForEarlierConnection(entry, running.Take(index))).Select(SocketNumberOf)];
        if (waiting.Count > 0)
        {
            waiting.Add(new CurlSocket(ResolverWakeupSocket));
        }

        socketCount = waiting.Count;
        for (int index = 0; index < Math.Min(waiting.Count, sockets.Length); index++)
        {
            sockets[index] = new CurlWaitSocket(waiting[index], CurlWaitEvents.PollIn, CurlWaitEvents.None);
        }

        return waiting.Count > sockets.Length ? CurlMultiCode.OutOfMemory : CurlMultiCode.Ok;
    }

    /// <summary>
    /// <c>curl_multi_get_offt</c>: one of the handle's transfer counts, as curl 8.22.0 keeps them. Current is
    /// every transfer added and not removed; pending every one of those not started that a connection limit
    /// holds back; running every other one whose message is not yet queued, started or not; done every one
    /// whose message is queued; added every transfer ever added.
    /// </summary>
    /// <param name="info">The count to report.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" /> and the count; <see cref="CurlMultiCode.UnknownOption" /> and 0 for
    /// <see cref="CurlMultiInfo.None" /> or any other value, and <see cref="CurlMultiCode.BadHandle" /> and 0
    /// once this handle is disposed.
    /// </returns>
    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info)
    {
        if (disposed)
        {
            return (CurlMultiCode.BadHandle, 0);
        }

        return info switch
        {
            CurlMultiInfo.TransfersCurrent => (CurlMultiCode.Ok, entries.Count),
            CurlMultiInfo.TransfersRunning => (CurlMultiCode.Ok, entries.Count(entry => !entry.Done && !IsPending(entry))),
            CurlMultiInfo.TransfersPending => (CurlMultiCode.Ok, entries.Count(IsPending)),
            CurlMultiInfo.TransfersDone => (CurlMultiCode.Ok, entries.Count(entry => entry.Done)),
            CurlMultiInfo.TransfersAdded => (CurlMultiCode.Ok, transfersAdded),
            _ => (CurlMultiCode.UnknownOption, 0),
        };
    }

    /// <summary>
    /// <c>curl_multi_notify_enable</c>: from the next perform or socket action on, sends
    /// <paramref name="notification" /> to <see cref="MultiOptions.Notify" />: <see cref="CurlMultiNotification.InfoRead" />
    /// when a message is queued with none queued before it, then <see cref="CurlMultiNotification.EasyDone" />
    /// for each transfer whose message is queued, each with that transfer, once the call has moved every
    /// transfer on and before it tells the socket and timer callbacks.
    /// </summary>
    /// <param name="notification">The notification to switch on; any number from 0 to 31 is kept.</param>
    /// <returns>
    /// <see cref="CurlMultiCode.Ok" />; <see cref="CurlMultiCode.UnknownOption" /> for a number outside 0 to 31
    /// and <see cref="CurlMultiCode.BadHandle" /> once this handle is disposed.
    /// </returns>
    public CurlMultiCode EnableNotification(CurlMultiNotification notification) =>
        ChangeNotification(notification, enable: true);

    /// <summary><c>curl_multi_notify_disable</c>: stops sending <paramref name="notification" />.</summary>
    /// <param name="notification">The notification to switch off.</param>
    /// <returns>As <see cref="EnableNotification" />.</returns>
    public CurlMultiCode DisableNotification(CurlMultiNotification notification) =>
        ChangeNotification(notification, enable: false);

    /// <summary>
    /// A server push on <paramref name="parent" />, as libcurl's HTTP/2 push promise handling answers it:
    /// refused when the handle is disposed, <paramref name="parent" /> is not in it, no
    /// <see cref="MultiOptions.Push" /> is set or <paramref name="pushed" /> is in a multi handle already;
    /// otherwise the callback decides. <see cref="CurlPushResult.Ok" /> adds <paramref name="pushed" />,
    /// which starts on the next perform as an added transfer does; <see cref="CurlPushResult.ErrorOut" />
    /// refuses it and ends <paramref name="parent" /> with <see cref="CurlCode.Http2" /> on the next perform;
    /// any other answer refuses it, as libcurl treats every answer but <c>CURL_PUSH_OK</c>.
    /// </summary>
    /// <param name="parent">The transfer the server pushed on.</param>
    /// <param name="pushed">The new transfer for the pushed stream.</param>
    /// <param name="headers">The push promise's headers as <c>name: value</c> lines, in order.</param>
    /// <returns><see cref="CurlPushResult.Ok" /> when the push joined the handle, else what refused it.</returns>
    internal CurlPushResult OfferPush(IEasyTransfer parent, IEasyTransfer pushed, IReadOnlyList<string> headers)
    {
        MultiEntry? parentEntry = PushParentOf(parent, pushed);
        if (parentEntry is null || Options.Push is not { } callback)
        {
            return CurlPushResult.Deny;
        }

        CurlPushResult answer = callback(parent, pushed, headers);
        if (answer == CurlPushResult.ErrorOut)
        {
            parentEntry.ForcedResult = CurlCode.Http2;
            return answer;
        }

        if (answer != CurlPushResult.Ok)
        {
            return CurlPushResult.Deny;
        }

        // The timer's answer to the add is the handle's to report on its next call, not the push's.
        owners[pushed] = this;
        AddEntry(pushed);
        return CurlPushResult.Ok;
    }

    /// <summary>The parent's entry, or <see langword="null" /> when the handle is disposed, the parent is not in it, or the pushed transfer is null or in a multi handle already.</summary>
    private MultiEntry? PushParentOf(IEasyTransfer parent, IEasyTransfer pushed)
    {
        if (disposed || pushed is null || owners.ContainsKey(pushed))
        {
            return null;
        }

        return entries.Find(candidate => candidate.Transfer == parent);
    }

    private static bool IsNotificationNumber(CurlMultiNotification notification) => (uint)notification < 32;

    private CurlMultiCode ChangeNotification(CurlMultiNotification notification, bool enable)
    {
        if (disposed)
        {
            return CurlMultiCode.BadHandle;
        }

        if (!IsNotificationNumber(notification))
        {
            return CurlMultiCode.UnknownOption;
        }

        uint bit = 1u << (int)notification;
        enabledNotifications = enable ? enabledNotifications | bit : enabledNotifications & ~bit;
        return CurlMultiCode.Ok;
    }

    private bool IsEnabled(CurlMultiNotification notification) => (enabledNotifications & (1u << (int)notification)) != 0;

    private void QueueDoneMessage(MultiEntry entry, CurlCode result)
    {
        entry.Done = true;
        if (messages.Count == 0 && IsEnabled(CurlMultiNotification.InfoRead))
        {
            pendingNotifications.Add((CurlMultiNotification.InfoRead, entry.Transfer));
        }

        messages.Add(new CurlMultiMessage(CurlMultiMessageType.Done, entry.Transfer, result));
        if (IsEnabled(CurlMultiNotification.EasyDone))
        {
            pendingNotifications.Add((CurlMultiNotification.EasyDone, entry.Transfer));
        }
    }

    private void SendNotifications()
    {
        (CurlMultiNotification Notification, IEasyTransfer Transfer)[] sending = [.. pendingNotifications];
        pendingNotifications.Clear();
        foreach ((CurlMultiNotification notification, IEasyTransfer transfer) in sending)
        {
            Options.Notify?.Invoke(notification, transfer);
        }
    }

    private void ClearForNetworkChange(CurlNetworkChange change)
    {
        if (change.HasFlag(CurlNetworkChange.ClearConnections))
        {
            idleConnectionsClosing = Task.WhenAll(idleConnectionsClosing, closeIdleConnections().AsTask());
        }

        if (change.HasFlag(CurlNetworkChange.ClearDns))
        {
            clearDnsCache();
        }
    }

    /// <summary>
    /// Whether <paramref name="entry" /> holds no socket of its own: under <see cref="CurlPipelining.Multiplex" />
    /// with <c>CURLOPT_PIPEWAIT</c> and not held to HTTP/1, it waits for a running transfer to the same
    /// origin added before it to open the connection it then shares, as libcurl's does (upstream test2407, BL-2750).
    /// </summary>
    private bool WaitsForEarlierConnection(MultiEntry entry, IEnumerable<MultiEntry> earlier)
    {
        HttpVersion version = entry.Transfer.Options.Http.HttpVersion;
        return Options.Pipelining.HasFlag(CurlPipelining.Multiplex)
            && entry.Transfer.Options.Http.PipeWait
            && version is not (HttpVersion.Http10 or HttpVersion.Http11)
            && earlier.Any(other => other.Origin == entry.Origin);
    }

    private CurlSocket SocketNumberOf(MultiEntry entry) => entry.Number ??= new CurlSocket(nextTransferSocket++);

    private static TaskCompletionSource NewWakeupSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool HasWorkForPerform(MultiEntry entry) => (!entry.Done && entry.Perform is null && CanStart(entry)) || IsFinishedUnreported(entry);

    private bool IsPending(MultiEntry entry) => !entry.Done && entry.Perform is null && !CanStart(entry);

    /// <summary>
    /// Whether <paramref name="candidate" /> may start now: it joins a multiplexed connection to its origin
    /// with a stream free, or the connection it opens stays within both connection limits.
    /// </summary>
    private bool CanStart(MultiEntry candidate)
    {
        List<MultiEntry> running = entries.FindAll(entry => entry.Perform is not null && !entry.Done);
        string origin = OriginOf(candidate.Transfer);
        if (IsMultiplexed(candidate.Transfer, origin) && running.Count(entry => entry.Origin == origin && entry.Multiplexed) % ConcurrentStreams() != 0)
        {
            return true;
        }

        return IsWithin(Options.MaxHostConnections, ConnectionsUsed(running.FindAll(entry => entry.Origin == origin)))
            && IsWithin(Options.MaxTotalConnections, ConnectionsUsed(running));
    }

    private static bool IsWithin(long limit, long used) => limit <= 0 || used < limit;

    private long ConnectionsUsed(List<MultiEntry> running)
    {
        long streams = ConcurrentStreams();
        return running.GroupBy(entry => (entry.Origin, entry.Multiplexed))
            .Sum(group => group.Key.Multiplexed ? (group.Count() + streams - 1) / streams : group.Count());
    }

    private long ConcurrentStreams() =>
        Options.MaxConcurrentStreams is < 1 or > int.MaxValue ? DefaultConcurrentStreams : Options.MaxConcurrentStreams;

    /// <summary>
    /// Whether a transfer to <paramref name="origin" /> shares its connection under
    /// <see cref="CurlPipelining.Multiplex" />: an <c>https</c> URL not held to HTTP/1 (ALPN offers h2 by
    /// default) or HTTP/2 with prior knowledge.
    /// </summary>
    private bool IsMultiplexed(IEasyTransfer transfer, string origin)
    {
        HttpVersion version = transfer.Options.Http.HttpVersion;
        return Options.Pipelining.HasFlag(CurlPipelining.Multiplex)
            && version is not (HttpVersion.Http10 or HttpVersion.Http11)
            && (origin.StartsWith("https://", StringComparison.Ordinal) || version == HttpVersion.Http2PriorKnowledge);
    }

    /// <summary>The scheme, host and port of the transfer's URL, or the URL text as it is when it does not parse.</summary>
    private static string OriginOf(IEasyTransfer transfer)
    {
        string? url = transfer.Options.Network.Url;
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? $"{uri.Scheme}://{uri.IdnHost}:{uri.Port}" : url ?? string.Empty;
    }

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

        if (IsInTransferCallback)
        {
            return ValueTask.FromResult((CurlMultiCode.RecursiveApiCall, 0));
        }

        cancellationToken.ThrowIfCancellationRequested();
        int previousThread = callbackQueue.EnterPerform();
        try
        {
            RunQueuedCallbacks();
            MoveEveryTransferOn();
        }
        finally
        {
            callbackQueue.LeavePerform(previousThread);
        }

        SendNotifications();
        CurlMultiCode code = UpdateSockets() ? UpdateTimer(timerFired) : CurlMultiCode.AbortedByCallback;
        return ValueTask.FromResult((code, entries.Count(entry => !entry.Done)));
    }

    /// <summary>
    /// Runs the callbacks the running transfers have asked for since the last call, on this thread and as
    /// code inside this handle's transfers (<see cref="IsInTransferCallback" />), as libcurl's perform calls them (CGR-033).
    /// </summary>
    private void RunQueuedCallbacks()
    {
        ManagedMultiTransfer? previous = PerformingHandle.Value;
        PerformingHandle.Value = this;
        try
        {
            callbackQueue.RunQueued();
        }
        finally
        {
            PerformingHandle.Value = previous;
        }
    }

    /// <summary>The started transfers first, so a slot one of them frees goes to a pending one in this call.</summary>
    private void MoveEveryTransferOn()
    {
        List<MultiEntry> notStarted = entries.FindAll(entry => entry.Perform is null);
        foreach (MultiEntry entry in entries.FindAll(entry => entry.Perform is not null))
        {
            MoveOn(entry);
        }

        foreach (MultiEntry entry in notStarted)
        {
            MoveOn(entry);
        }
    }

    /// <summary>
    /// Queues the message of a started transfer that has finished, or starts one not started. A transfer
    /// started in this call is never reported done in it, as libcurl's first perform of an HTTP transfer
    /// only begins its non-blocking connect (upstream test2405, BL-2743).
    /// </summary>
    private void MoveOn(MultiEntry entry)
    {
        if (entry.Done || EndedByForce(entry))
        {
            return;
        }

        if (entry.Perform is null)
        {
            StartWhenAllowed(entry);
            return;
        }

        entry.SocketReadyUntilNextPerform = false;
        QueueDoneMessageWhenFinished(entry);
    }

    /// <summary>Ends the transfer when a callback aborted the handle or a push answer failed it.</summary>
    private bool EndedByForce(MultiEntry entry)
    {
        CurlCode? forced = abortedByCallback ? CurlCode.AbortedByCallback : entry.ForcedResult;
        if (forced is null)
        {
            return false;
        }

        entry.Cancellation.Cancel();
        QueueDoneMessage(entry, forced.Value);
        return true;
    }

    /// <summary>Starts the transfer when it has not started and a connection limit does not hold it pending.</summary>
    private void StartWhenAllowed(MultiEntry entry)
    {
        if (CanStart(entry))
        {
            entry.Perform = Start(entry);
            entry.SocketReadyUntilNextPerform = true;
        }
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
        if (NeedsSocket(entry))
        {
            entry.Socket = SocketNumberOf(entry);
            return callback(entry.Transfer, entry.Socket.Value, CurlPoll.In, null);
        }

        if (entry.Done && entry.Socket is { } socket)
        {
            entry.Socket = null;
            return callback(entry.Transfer, socket, CurlPoll.Remove, entry.SocketState);
        }

        return 0;
    }

    private static bool NeedsSocket(MultiEntry entry) => !entry.Done && entry.Perform is not null && entry.Socket is null;

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
        long handleTimeout = TimeoutMilliseconds();
        if (!entries.Exists(entry => !entry.Done))
        {
            return handleTimeout;
        }

        long check = (long)RunningTransferCheckInterval.TotalMilliseconds;
        return handleTimeout < 0 ? check : Math.Min(handleTimeout, check);
    }

    // Marks the code a transfer's perform runs, its callbacks included, with the handle that started it
    // (IsInTransferCallback); the value flows into the perform and never back out to the caller.
    private async Task<CurlCode> Start(MultiEntry entry)
    {
        entry.StartTimestamp = timeProvider.GetTimestamp();
        entry.Origin = OriginOf(entry.Transfer);
        entry.Multiplexed = IsMultiplexed(entry.Transfer, entry.Origin);
        PerformingHandle.Value = this;
        TransferCallbackMarshal callbacks = new(callbackQueue, entry.Cancellation.Token);
        if (entry.Transfer is ManagedEasyTransfer easy)
        {
            easy.MultiCallbacks = callbacks;
        }

        try
        {
            return await entry.Transfer.PerformAsync(entry.Cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            callbacks.Stop();
        }
    }

    private long RemainingMilliseconds(MultiEntry entry)
    {
        TimeSpan limit = entry.Transfer.Options.Transfer.Timeout;
        if (entry.Done || entry.Perform is null || limit <= TimeSpan.Zero)
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

        if (IsInTransferCallback)
        {
            return (CurlMultiCode.RecursiveApiCall, 0);
        }

        if (timeout < TimeSpan.Zero)
        {
            return (CurlMultiCode.BadFunctionArgument, 0);
        }

        long start = timeProvider.GetTimestamp();
        TimeSpan limit = WaitLimit(timeout);
        while (true)
        {
            int ready = CountReady(extraSockets.Span);
            TimeSpan left = limit - timeProvider.GetElapsedTime(start);
            if (ready > 0 || WaitEnds(left, extraSockets.Length, poll))
            {
                return (CurlMultiCode.Ok, ready);
            }

            await WaitForEventAsync(left, extraSockets.Length > 0, poll, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether libcurl's socket for the transfer reports an event now: the perform that started it only began
    /// a non-blocking connect, so until the next perform the socket is writable and a wait or poll returns
    /// at once with it counted (BL-3308).
    /// </summary>
    private static bool HasSocketReadyUntilNextPerform(MultiEntry entry) => entry.SocketReadyUntilNextPerform && !entry.Done;

    private int CountReady(Span<CurlWaitSocket> extraSockets) =>
        MarkReadySockets(extraSockets) + entries.Count(IsFinishedUnreported) + entries.Count(HasSocketReadyUntilNextPerform) + (callbackQueue.HasQueued ? 1 : 0);

    private TimeSpan WaitLimit(TimeSpan timeout)
    {
        long handleTimeout = WaitTimerMilliseconds();
        return handleTimeout >= 0 && handleTimeout < timeout.TotalMilliseconds ? TimeSpan.FromMilliseconds(handleTimeout) : timeout;
    }

    /// <summary>
    /// The handle's timeout as a wait or poll counts it: 0 while a perform has work, else the milliseconds
    /// until the first running transfer's timeout still ahead, or -1 when none is. A timeout already passed
    /// is left out: the transfer's perform ends it with <see cref="CurlCode.OperationTimedOut" /> on its own
    /// clock, a moment later, and the wait ends when a perform finishes. Counted as 0 (as
    /// <see cref="GetTimeout" /> answers it), every wait until then returned at once, and a perform-and-poll
    /// loop spun at full CPU (BL-3206).
    /// </summary>
    internal long WaitTimerMilliseconds() =>
        entries.Exists(HasWorkForPerform) || callbackQueue.HasQueued ? 0 : entries.Select(RemainingMilliseconds).Where(milliseconds => milliseconds > 0).DefaultIfEmpty(-1).Min();

    private int MarkReadySockets(Span<CurlWaitSocket> sockets)
    {
        int ready = 0;
        for (int index = 0; index < sockets.Length; index++)
        {
            CurlWaitEvents returned = PollWaitSocket(sockets[index].Socket, sockets[index].Events);
            sockets[index] = sockets[index] with { ReturnedEvents = returned };
            ready += returned == CurlWaitEvents.None ? 0 : 1;
        }

        return ready;
    }

    /// <summary>
    /// The events of <paramref name="events" /> an extra socket has now: for the socket number
    /// <c>CURLINFO_ACTIVESOCKET</c> gave a connect-only transfer in the handle, its connection's readiness
    /// (<see cref="ManagedEasyTransfer.ConnectOnlyReadyEvents" />, upstream test677, BL-2633), else what
    /// <see cref="IWaitSocketPoller" /> answers for the operating system socket. Any other number of the
    /// engine's own, from <see cref="ResolverWakeupSocket" /> up (a multi handle's transfer sockets, which
    /// <c>curl_multi_waitfds</c> gives, upstream test2405), is no descriptor and never has an event.
    /// </summary>
    private CurlWaitEvents PollWaitSocket(CurlSocket socket, CurlWaitEvents events)
    {
        foreach (MultiEntry entry in entries)
        {
            if (entry.Transfer is ManagedEasyTransfer easy && easy.ConnectOnlyReadyEvents(socket, events) is { } ready)
            {
                return ready;
            }
        }

        return socket.Handle >= ResolverWakeupSocket ? CurlWaitEvents.None : socketPoller.Poll(socket, events);
    }

    private bool WaitEnds(TimeSpan left, int extraSocketCount, bool poll)
    {
        if (poll)
        {
            return TakeWakeup() || left <= TimeSpan.Zero;
        }

        // Every transfer that can start has (one that can makes the handle's timeout 0), and one held
        // pending by a connection limit means another runs, so one not done means one still running.
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
        events.Add(callbackQueue.Arrived);
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

        // Done before the result is read, so a perform that threw is not read again.
        entry.Done = true;
        QueueDoneMessage(entry, entry.Perform.GetAwaiter().GetResult());
        CloseIdleConnectionsBeyondMaxConnects();
    }

    /// <summary>
    /// Closes the engine's oldest idle connections beyond <see cref="MultiOptions.MaxConnects" />, or 4 for
    /// each transfer in the handle when it is 0 or less, as libcurl's pool does when a transfer hands its
    /// connection back.
    /// </summary>
    private void CloseIdleConnectionsBeyondMaxConnects()
    {
        long keep = Options.MaxConnects > 0 ? Options.MaxConnects : DefaultConnectsPerTransfer * entries.Count;
        idleConnectionsClosing = Task.WhenAll(idleConnectionsClosing, closeIdleConnectionsBeyond(keep).AsTask());
    }

    /// <summary>
    /// One added transfer: its perform once started and when, whether its message was queued, its
    /// engine-numbered socket once one was given, and the socket the socket callback was told of with
    /// the value <see cref="Assign" /> gave it.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    private sealed class MultiEntry(IEasyTransfer transfer)
    {
        public IEasyTransfer Transfer { get; } = transfer;

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<CurlCode>? Perform { get; set; }

        public long StartTimestamp { get; set; }

        public bool Done { get; set; }

        public bool SocketReadyUntilNextPerform { get; set; }

        public CurlSocket? Number { get; set; }

        public CurlSocket? Socket { get; set; }

        public object? SocketState { get; set; }

        public string Origin { get; set; } = string.Empty;

        public bool Multiplexed { get; set; }

        public CurlCode? ForcedResult { get; set; }
    }
}
