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
    private ValueTask<(CurlMultiCode Code, int RunningTransfers)> MoveTransfersOnAsync(bool socketAction, bool timerFired, CancellationToken cancellationToken)
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
        return ValueTask.FromResult(socketAction && abortedByCallback ? (CurlMultiCode.Ok, UnfinishedCount()) : MoveOnAndTellCallbacks(socketAction, timerFired));
    }

    private (CurlMultiCode Code, int RunningTransfers) MoveOnAndTellCallbacks(bool socketAction, bool timerFired)
    {
        int previousThread = callbackQueue.EnterPerform();
        try
        {
            RunQueuedCallbacks();
            List<MultiEntry> startedBefore = entries.FindAll(entry => entry.Perform is not null);
            MoveEveryTransferOn();
            if (SettleSocketWaitingTransfers())
            {
                startedBefore.ForEach(MoveOn);
            }
        }
        finally
        {
            callbackQueue.LeavePerform(previousThread);
        }

        SendNotifications();
        CurlMultiCode code = TellCallbacks(socketAction, timerFired);
        return (code, UnfinishedCount());
    }

    private int UnfinishedCount() => entries.Count(entry => !entry.Done);

    /// <summary>
    /// Tells the socket callback (on a socket action only) and the timer callback what changed; a handle a
    /// callback killed in an earlier call tells neither again, as curl 8.22.0's dead multi handle (CGR-148).
    /// </summary>
    private CurlMultiCode TellCallbacks(bool socketAction, bool timerFired)
    {
        if (abortedByCallback)
        {
            return CurlMultiCode.Ok;
        }

        return !socketAction || UpdateSockets() ? UpdateTimer(timerFired) : CurlMultiCode.AbortedByCallback;
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
        if (entry.Done && entry.Socket is { } socket)
        {
            entry.Socket = null;
            return callback(entry.Transfer, socket, CurlPoll.Remove, entry.SocketState);
        }

        if (entry.SocketWait is { } wait)
        {
            return TellWaitedSocket(callback, entry, wait);
        }

        return 0;
    }

    /// <summary>
    /// Tells the socket callback of the transfer's real socket and what it waits for there, when either changed:
    /// <see cref="CurlPoll.Out" /> while a write waits, <see cref="CurlPoll.In" /> while a read does, as libcurl
    /// does; a transfer that waits on neither keeps what it was told last (ADR-0973).
    /// </summary>
    private static int TellWaitedSocket(MultiSocketCallback callback, MultiEntry entry, TransferSocketWait wait)
    {
        CurlPoll waiting = wait.Waiting;
        return WaitedSocketOf(entry, wait, waiting) is { } socket ? TellChangedSocket(callback, entry, socket, waiting) : 0;
    }

    private static int TellChangedSocket(MultiSocketCallback callback, MultiEntry entry, CurlSocket socket, CurlPoll waiting)
    {
        bool sameSocket = entry.Socket == socket;
        if (sameSocket && entry.ReportedPoll == waiting)
        {
            return 0;
        }

        entry.SocketState = sameSocket ? entry.SocketState : null;
        entry.Socket = socket;
        entry.ReportedPoll = waiting;
        return callback(entry.Transfer, socket, waiting, entry.SocketState);
    }

    private static CurlSocket? WaitedSocketOf(MultiEntry entry, TransferSocketWait wait, CurlPoll waiting) =>
        entry.Done || wait.Socket is not { } handle || waiting == CurlPoll.None ? null : new CurlSocket(handle);

    /// <summary>
    /// Whether an action on <paramref name="socket" /> wakes <paramref name="wait" />: an action on its own socket,
    /// or a timeout action while it has no operating system socket (an in-memory connection), which the timer's 0
    /// brings the application back for (ADR-0998).
    /// </summary>
    private static bool IsActionFor(TransferSocketWait wait, CurlSocket socket) =>
        wait.Socket is { } handle ? new CurlSocket(handle) == socket : socket == CurlMultiSockets.Timeout;

    private static CurlSocketSelect SelectOf(CurlPoll waiting) => (CurlSocketSelect)(int)(waiting & CurlPoll.InOut);

    /// <summary>
    /// How long a transfer still connecting is let go on before its connect's <see cref="CurlPoll.Out" /> wait counts as
    /// settled: a connect that completes at once (loopback) moves on to its read wait in that time, as libcurl's
    /// action runs the connect through to it (CGR-034 C_75, C_21).
    /// </summary>
    private const long ConnectGraceMilliseconds = 50;

    /// <summary>
    /// Lets each running transfer whose socket the application polls go on until it waits on that socket again or
    /// ends, running the callbacks it asks for meanwhile, for at most <see cref="SocketWaitSettleLimit" />: libcurl's
    /// action does a transfer's work on the calling thread up to its next wait, so the socket and timer callbacks
    /// that follow say what it waits for next (ADR-0973).
    /// </summary>
    /// <returns><see langword="true" /> when any transfer was let go on.</returns>
    private bool SettleSocketWaitingTransfers()
    {
        List<MultiEntry> waiting = entries.FindAll(entry => entry.SocketWait is not null && entry.Perform is not null && !entry.Done);
        waiting.ForEach(Settle);
        return waiting.Count > 0;
    }

    private void Settle(MultiEntry entry)
    {
        long began = Environment.TickCount64;
        long until = began + (long)SocketWaitSettleLimit.TotalMilliseconds;
        while (true)
        {
            Task changed = entry.NextChange;
            long left = until - Environment.TickCount64;
            if (IsSettled(entry, Environment.TickCount64 - began >= ConnectGraceMilliseconds) || left <= 0)
            {
                return;
            }

            if (callbackQueue.HasQueued)
            {
                RunQueuedCallbacks();
                continue;
            }

            Task.WaitAny([changed, entry.Perform!, callbackQueue.Arrived], TimeSpan.FromMilliseconds(Math.Min(left, ConnectGraceMilliseconds)));
        }
    }

    /// <summary>
    /// Whether the transfer has ended, or waits on its socket, or on I/O over a connection with no operating system
    /// socket, with no deadline passed: one whose deadline has passed is let go on until its own timer ends it, as
    /// libcurl's timeout action fails it with <see cref="CurlCode.OperationTimedOut" /> in that call (CGR-034 C_75).
    /// </summary>
    private bool IsSettled(MultiEntry entry, bool connectGraceOver) =>
        entry.Perform!.IsCompleted
        || ((IsWaitingOnItsSettledSocket(entry, connectGraceOver) || IsWaitingWithoutSocket(entry)) && !(entry.SocketWait!.EarliestDeadline <= timeProvider.GetUtcNow()));

    private static bool IsWaitingOnItsSettledSocket(MultiEntry entry, bool connectGraceOver) =>
        IsWaitingOnItsSocket(entry) && (connectGraceOver || !entry.SocketWait!.IsConnecting);

    private static bool IsWaitingWithoutSocket(MultiEntry entry) => entry.SocketWait!.IsWaitingWithoutSocket;

    private static bool IsWaitingOnItsSocket(MultiEntry entry) => entry.SocketWait is { IsWaitingOnItsSocket: true };

}
