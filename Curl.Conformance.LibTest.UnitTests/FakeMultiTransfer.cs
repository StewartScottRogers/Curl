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

    public MultiOptions Options { get; set; } = new();

    public CurlMultiCode Add(IEasyTransfer transfer) => AddResult;

    public CurlMultiCode Remove(IEasyTransfer transfer) => RemoveResult;

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken) => ValueTask.FromResult(PerformResult);

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => ValueTask.FromResult(WaitResult);

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => ValueTask.FromResult(PollResult);

    public CurlMultiCode Wakeup() => WakeupResult;

    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages)
    {
        message = null;
        remainingMessages = 0;
        return false;
    }

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) => throw new NotSupportedException();

    public CurlMultiCode Assign(CurlSocket socket, object? socketState) => throw new NotSupportedException();

    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout() => TimeoutResult;

    public IReadOnlyList<IEasyTransfer> GetHandles() => [];

    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount)
    {
        socketCount = WaitSockets.Length;
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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
