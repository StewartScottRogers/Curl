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
            if (ReturnsNow(ready, left, extraSockets.Length, poll))
            {
                return (CurlMultiCode.Ok, ready);
            }

            await WaitForEventAsync(left, extraSockets.Length > 0, poll, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Whether a wait or poll returns now. A poll takes a pending wakeup whenever it looks, transfer activity
    /// or not, as libcurl's <c>multi_wait</c> drains its wakeup socket after every poll, so a wakeup made
    /// before a poll that returned for a ready socket never ends a later poll (CGR-153).
    /// </summary>
    private bool ReturnsNow(int ready, TimeSpan left, int extraSocketCount, bool poll)
    {
        bool wokenUp = poll && TakeWakeup();
        return ready > 0 || wokenUp || WaitEnds(left, extraSocketCount, poll);
    }

    /// <summary>
    /// Whether libcurl's socket for the transfer reports an event now: the perform that started it only began
    /// a non-blocking connect, so until the next perform the socket is writable and a wait or poll returns
    /// at once with it counted (BL-3308).
    /// </summary>
    private static bool HasSocketReadyUntilNextPerform(MultiEntry entry) => entry.SocketReadyUntilNextPerform && !entry.Done;

    private int CountReady(Span<CurlWaitSocket> extraSockets) =>
        MarkReadySockets(extraSockets) + entries.Count(IsFinishedUnreported) + entries.Count(HasSocketReadyUntilNextPerform)
        + entries.Count(IsWaitingOnAnEngineSocket) + (callbackQueue.HasQueued ? 1 : 0);

    /// <summary>
    /// Whether a running transfer waits on the engine socket number its connection with no operating system socket
    /// was given (<see cref="TransferSocketWait.TakeSocket" />, ADR-0955): no poll can tell when an in-memory
    /// connection is ready, so a wait counts it ready at once, as a <c>select()</c> on a level-triggered socket that
    /// has its data returns, and the application's action on it lets the transfer go on (ADR-0973).
    /// </summary>
    private static bool IsWaitingOnAnEngineSocket(MultiEntry entry) =>
        entry.Perform is { IsCompleted: false } && entry.SocketWait is { IsWaitingOnAnEngineSocket: true };

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
            return left <= TimeSpan.Zero;
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

}
