using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

internal sealed partial class ManagedMultiTransfer
{
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
    /// waits for an earlier transfer's connection, <see cref="WaitsForEarlierConnection" />) - the transfer's real
    /// socket once its dial has one (its operating system socket, or ADR-0955's engine number for a connection with
    /// none), with or without a socket callback (ADR-0982); a transfer with no socket the handle knows (before its dial
    /// has one, or one that is not a <see cref="ManagedEasyTransfer" />) gives none
    /// - then, while there is any socket, the threaded resolver's <see cref="ResolverWakeupSocket" />.
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

        List<CurlSocket> waiting = WaitedSockets();
        socketCount = waiting.Count;
        for (int index = 0; index < Math.Min(waiting.Count, sockets.Length); index++)
        {
            sockets[index] = new CurlWaitSocket(waiting[index], CurlWaitEvents.PollIn, CurlWaitEvents.None);
        }

        return waiting.Count > sockets.Length ? CurlMultiCode.OutOfMemory : CurlMultiCode.Ok;
    }

    /// <summary>
    /// The sockets <see cref="GetWaitSockets" /> answers, in order: each running transfer's known socket (none for
    /// one that waits for an earlier transfer's connection or has no socket the handle knows), then, while there
    /// is any, the threaded resolver's <see cref="ResolverWakeupSocket" />.
    /// </summary>
    private List<CurlSocket> WaitedSockets()
    {
        List<MultiEntry> running = entries.FindAll(entry => !entry.Done && entry.Perform is not null);
        List<CurlSocket> waiting = [.. running.Where((entry, index) => !WaitsForEarlierConnection(entry, running.Take(index))).Select(WaitSocketOf).OfType<CurlSocket>()];
        if (waiting.Count > 0)
        {
            waiting.Add(new CurlSocket(ResolverWakeupSocket));
        }

        return waiting;
    }
}
