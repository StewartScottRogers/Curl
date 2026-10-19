using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Conformance.LibTest.UnitTests;

/// <summary>An <see cref="IMultiTransfer"/> whose calls answer the codes and counts its properties hold; it runs no transfer.</summary>
internal sealed class FakeMultiTransfer : IMultiTransfer
{
    public CurlMultiCode AddResult { get; set; } = CurlMultiCode.Ok;

    public CurlMultiCode RemoveResult { get; set; } = CurlMultiCode.Ok;

    public (CurlMultiCode Code, int RunningTransfers) PerformResult { get; set; } = (CurlMultiCode.Ok, 0);

    public (CurlMultiCode Code, int ReadySockets) WaitResult { get; set; } = (CurlMultiCode.Ok, 0);

    public (CurlMultiCode Code, int ReadySockets) PollResult { get; set; } = (CurlMultiCode.Ok, 0);

    public CurlMultiCode WakeupResult { get; set; } = CurlMultiCode.Ok;

    public (CurlMultiCode Code, long TimeoutMilliseconds) TimeoutResult { get; set; } = (CurlMultiCode.Ok, -1);

    public CurlMultiCode WaitSocketsResult { get; set; } = CurlMultiCode.Ok;

    public CurlWaitSocket[] WaitSockets { get; set; } = [];

    /// <summary>The events each wait reports for every extra socket it is given.</summary>
    public CurlWaitEvents ExtraSocketEvents { get; set; }

    public MultiOptions Options { get; set; } = new();

    /// <summary>Perform answers taken in turn before <see cref="PerformResult"/>.</summary>
    public Queue<(CurlMultiCode Code, int RunningTransfers)> PerformResults { get; } = new();

    /// <summary>Runs in each wait, before it answers.</summary>
    public Action? Waiting { get; set; }

    /// <summary>When set, one done message with this code is read for the transfer added last.</summary>
    public CurlCode? DoneResult { get; set; }

    /// <summary>The messages left that the read reports.</summary>
    public int MessagesLeftAfterRead { get; set; }

    /// <summary>Answers each socket action in place of a transfer; the action throws when it is not set.</summary>
    public Func<CurlSocket, CurlSocketSelect, (CurlMultiCode Code, int RunningTransfers)>? SocketActing { get; set; }

    /// <summary>The type of the message <see cref="DoneResult"/> reads.</summary>
    public CurlMultiMessageType DoneMessageType { get; set; } = CurlMultiMessageType.Done;

    /// <summary>The transfer added last.</summary>
    public IEasyTransfer? Added => added;

    private IEasyTransfer? added;

    private readonly Queue<IEasyTransfer> doneTransfers = new();

    /// <summary>Add answers taken in turn before <see cref="AddResult"/>.</summary>
    public Queue<CurlMultiCode> AddResults { get; } = new();

    /// <summary>Runs in each add, before it answers.</summary>
    public Action? Adding { get; set; }

    /// <summary>Runs in each wait-sockets call before it counts; a code it gives is the call's answer.</summary>
    public Func<CurlMultiCode?>? WaitSocketsAnswering { get; set; }

    public CurlMultiCode Add(IEasyTransfer transfer)
    {
        Adding?.Invoke();
        if (AddResults.TryDequeue(out CurlMultiCode next))
        {
            AddResult = next;
        }

        added = AddResult == CurlMultiCode.Ok ? transfer : added;
        if (AddResult == CurlMultiCode.Ok && EachAddedDoneResult is not null)
        {
            lock (doneTransfers)
            {
                doneTransfers.Enqueue(transfer);
            }
        }

        return AddResult;
    }

    /// <summary>How many transfers have been removed.</summary>
    public int Removals { get; private set; }

    public CurlMultiCode Remove(IEasyTransfer transfer)
    {
        Removals++;
        return RemoveResult;
    }

    /// <summary>Runs in each perform, before it answers.</summary>
    public Action? Performing { get; set; }

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken)
    {
        Performing?.Invoke();
        return ValueTask.FromResult(PerformResults.Count > 0 ? PerformResults.Dequeue() : PerformResult);
    }

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Waiting?.Invoke();
        for (int i = 0; i < extraSockets.Length; i++)
        {
            extraSockets.Span[i] = extraSockets.Span[i] with { ReturnedEvents = ExtraSocketEvents };
        }

        return ValueTask.FromResult(WaitResult);
    }

    /// <summary>Runs in each poll, before it answers.</summary>
    public Action? Polling { get; set; }

    /// <summary>When set, every transfer added from then on reads one done message with this code, in the order added.</summary>
    public CurlCode? EachAddedDoneResult { get; set; }

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Polling?.Invoke();
        return ValueTask.FromResult(PollResult);
    }

    /// <summary>How many wakeups have been sent.</summary>
    public int Wakeups { get; private set; }

    public CurlMultiCode Wakeup()
    {
        Wakeups++;
        return WakeupResult;
    }

    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages)
    {
        lock (doneTransfers)
        {
            if (EachAddedDoneResult is { } each && doneTransfers.TryDequeue(out IEasyTransfer? done))
            {
                message = new CurlMultiMessage(CurlMultiMessageType.Done, done, each);
                remainingMessages = doneTransfers.Count;
                return true;
            }
        }

        message = DoneResult is { } result && added is not null ? new CurlMultiMessage(DoneMessageType, added, result) : null;
        remainingMessages = MessagesLeftAfterRead;
        DoneResult = null;
        return message is not null;
    }

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) =>
        SocketActing is { } acting ? ValueTask.FromResult(acting(socket, events)) : throw new NotSupportedException();

    public CurlMultiCode Assign(CurlSocket socket, object? socketState) => throw new NotSupportedException();

    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout() => TimeoutResult;

    public IReadOnlyList<IEasyTransfer> GetHandles() => [];

    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount)
    {
        CurlMultiCode? answer = WaitSocketsAnswering?.Invoke();
        socketCount = WaitSockets.Length;
        if (answer is { } forced)
        {
            return forced;
        }

        if (WaitSocketsResult != CurlMultiCode.Ok)
        {
            return WaitSocketsResult;
        }

        if (sockets.Length < WaitSockets.Length)
        {
            return CurlMultiCode.OutOfMemory;
        }

        WaitSockets.CopyTo(sockets);
        return CurlMultiCode.Ok;
    }

    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info) => throw new NotSupportedException();

    public CurlMultiCode EnableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public CurlMultiCode DisableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    /// <summary>How many times the multi transfer has been disposed.</summary>
    public int Disposals { get; private set; }

    public ValueTask DisposeAsync()
    {
        Disposals++;
        return ValueTask.CompletedTask;
    }
}
