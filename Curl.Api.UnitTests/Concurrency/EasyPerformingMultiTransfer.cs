using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Api.Concurrency;

/// <summary>
/// An <see cref="IMultiTransfer"/> that runs each added transfer by its own
/// <see cref="IEasyTransfer.PerformAsync"/>, as the managed engine's multi handle does, honouring
/// <see cref="MultiOptions.MaxTotalConnections"/> and <see cref="MultiOptions.MaxHostConnections"/> (one
/// connection per running transfer, the host taken from its URL). It records the most transfers it ran at
/// once, in all and per host, and lets a test fail a call (<see cref="AddCode"/>, <see cref="PerformCode"/>,
/// <see cref="PollCode"/>) or slip in a message about a transfer it never had (<see cref="StrayMessages"/>).
/// </summary>
internal sealed class EasyPerformingMultiTransfer : IMultiTransfer
{
    private readonly Lock _gate = new();
    private readonly List<Entry> _entries = [];
    private readonly Queue<CurlMultiMessage> _messages = new();
    private TaskCompletionSource _wakeup = NewSignal();

    public MultiOptions Options { get; set; } = new();

    public CurlMultiCode AddCode { get; set; } = CurlMultiCode.Ok;

    public CurlMultiCode PerformCode { get; set; } = CurlMultiCode.Ok;

    public CurlMultiCode PollCode { get; set; } = CurlMultiCode.Ok;

    public List<CurlMultiMessage> StrayMessages { get; } = [];

    public List<IEasyTransfer> Removed { get; } = [];

    public int MostRunningAtOnce { get; private set; }

    public int MostRunningOnOneHostAtOnce { get; private set; }

    public int WakeupCount { get; private set; }

    public bool Disposed { get; private set; }

    public CurlMultiCode Add(IEasyTransfer transfer)
    {
        if (AddCode == CurlMultiCode.Ok)
        {
            _entries.Add(new Entry(transfer));
        }

        return AddCode;
    }

    public CurlMultiCode Remove(IEasyTransfer transfer)
    {
        Entry entry = _entries.Single(candidate => candidate.Transfer == transfer);
        _entries.Remove(entry);
        entry.Cancellation.Cancel();
        Removed.Add(transfer);
        return CurlMultiCode.Ok;
    }

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken)
    {
        StartWhatTheLimitsAllow();
        foreach (Entry entry in _entries.Where(entry => entry.Perform is { IsCompleted: true } && !entry.Reported))
        {
            entry.Reported = true;
            _messages.Enqueue(new CurlMultiMessage(CurlMultiMessageType.Done, entry.Transfer, entry.Perform!.GetAwaiter().GetResult()));
        }

        StrayMessages.ForEach(_messages.Enqueue);
        StrayMessages.Clear();
        return ValueTask.FromResult((PerformCode, _entries.Count(entry => !entry.Reported)));
    }

    public async ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken)
    {
        List<Task> events = [.. _entries.Select(entry => entry.Perform).OfType<Task>().Where(perform => !perform.IsCompleted)];
        lock (_gate)
        {
            events.Add(_wakeup.Task);
        }

        events.Add(Task.Delay(timeout, cancellationToken));
        await Task.WhenAny(events);
        lock (_gate)
        {
            if (_wakeup.Task.IsCompleted)
            {
                _wakeup = NewSignal();
            }
        }

        return (PollCode, 0);
    }

    public CurlMultiCode Wakeup()
    {
        lock (_gate)
        {
            WakeupCount++;
            _wakeup.TrySetResult();
        }

        return CurlMultiCode.Ok;
    }

    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages)
    {
        bool read = _messages.TryDequeue(out message);
        remainingMessages = _messages.Count;
        return read;
    }

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public CurlMultiCode Assign(CurlSocket socket, object? socketState) => throw new NotSupportedException();

    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout() => throw new NotSupportedException();

    public IReadOnlyList<IEasyTransfer> GetHandles() => [.. _entries.Select(entry => entry.Transfer)];

    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount) => throw new NotSupportedException();

    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info) => throw new NotSupportedException();

    public CurlMultiCode EnableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public CurlMultiCode DisableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private static string HostOf(IEasyTransfer transfer) => new Uri(transfer.Options.Network.Url!).Host;

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void StartWhatTheLimitsAllow()
    {
        foreach (Entry entry in _entries.Where(entry => entry.Perform is null).ToArray())
        {
            List<Entry> running = [.. _entries.Where(other => other.Perform is { IsCompleted: false })];
            int onHost = running.Count(other => HostOf(other.Transfer) == HostOf(entry.Transfer));
            if ((Options.MaxTotalConnections > 0 && running.Count >= Options.MaxTotalConnections)
                || (Options.MaxHostConnections > 0 && onHost >= Options.MaxHostConnections))
            {
                continue;
            }

            entry.Perform = entry.Transfer.PerformAsync(entry.Cancellation.Token).AsTask();
            MostRunningAtOnce = Math.Max(MostRunningAtOnce, running.Count + 1);
            MostRunningOnOneHostAtOnce = Math.Max(MostRunningOnOneHostAtOnce, onHost + 1);
        }
    }

    private sealed class Entry(IEasyTransfer transfer)
    {
        public IEasyTransfer Transfer { get; } = transfer;

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<CurlCode>? Perform { get; set; }

        public bool Reported { get; set; }
    }
}
