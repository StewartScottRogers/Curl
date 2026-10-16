using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Libcurl.UnitTests.Multi.Settings;

/// <summary>
/// An <see cref="IMultiTransfer"/> for the settings area: it holds the transfers added to it and answers
/// <see cref="GetHandles"/> from them plus <see cref="Extra"/>, fills <see cref="GetWaitSockets"/> from
/// <see cref="WaitSockets"/>, answers <see cref="GetInfo"/> from <see cref="Counts"/>, and records the
/// notifications switched on and off. The wait, poll and socket-action members are not called by these tests.
/// </summary>
internal sealed class SettingsFakeMultiTransfer : IMultiTransfer
{
    private readonly List<IEasyTransfer> added = [];

    public MultiOptions Options { get; set; } = new();

    public int OptionsWrites { get; private set; }

    public List<IEasyTransfer> Extra { get; } = [];

    public CurlWaitSocket[] WaitSockets { get; set; } = [];

    public Dictionary<CurlMultiInfo, long> Counts { get; } = [];

    public List<CurlMultiInfo> InfoAsked { get; } = [];

    public List<(CurlMultiNotification Notification, bool Enabled)> Notifications { get; } = [];

    public CurlMultiCode NotifyResult { get; set; } = CurlMultiCode.Ok;

    public CurlMultiCode Add(IEasyTransfer transfer)
    {
        added.Add(transfer);
        return CurlMultiCode.Ok;
    }

    public CurlMultiCode Remove(IEasyTransfer transfer)
    {
        added.Remove(transfer);
        return CurlMultiCode.Ok;
    }

    public IReadOnlyList<IEasyTransfer> GetHandles() => [.. Extra, .. added];

    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount)
    {
        socketCount = WaitSockets.Length;
        if (sockets.Length < WaitSockets.Length)
        {
            return CurlMultiCode.OutOfMemory;
        }

        WaitSockets.CopyTo(sockets);
        return CurlMultiCode.Ok;
    }

    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info)
    {
        InfoAsked.Add(info);
        return (CurlMultiCode.Ok, Counts[info]);
    }

    public CurlMultiCode EnableNotification(CurlMultiNotification notification)
    {
        Notifications.Add((notification, true));
        return NotifyResult;
    }

    public CurlMultiCode DisableNotification(CurlMultiNotification notification)
    {
        Notifications.Add((notification, false));
        return NotifyResult;
    }

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => throw new NotSupportedException();

    public CurlMultiCode Wakeup() => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) => throw new NotSupportedException();

    public CurlMultiCode Assign(CurlSocket socket, object? socketState) => throw new NotSupportedException();

    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout() => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
