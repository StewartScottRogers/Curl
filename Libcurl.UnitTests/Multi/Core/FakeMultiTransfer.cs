using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Libcurl.UnitTests.Multi.Core;

/// <summary>
/// An <see cref="IMultiTransfer"/> that answers <see cref="AddResult"/>, <see cref="RemoveResult"/> and
/// <see cref="PerformResult"/>, records the transfers it was given, hands out <see cref="Messages"/> in
/// order and counts its disposals. Its other members are not called by these tests.
/// </summary>
internal sealed class FakeMultiTransfer : IMultiTransfer
{
    public CurlMultiCode AddResult { get; set; } = CurlMultiCode.Ok;

    public CurlMultiCode RemoveResult { get; set; } = CurlMultiCode.Ok;

    public (CurlMultiCode Code, int RunningTransfers) PerformResult { get; set; }

    public CancellationToken PerformedToken { get; private set; }

    public List<IEasyTransfer> Added { get; } = [];

    public List<IEasyTransfer> Removed { get; } = [];

    public Queue<CurlMultiMessage> Messages { get; } = new();

    public int DisposeCount { get; private set; }

    public MultiOptions Options { get; set; } = new();

    public CurlMultiCode Add(IEasyTransfer transfer)
    {
        Added.Add(transfer);
        return AddResult;
    }

    public CurlMultiCode Remove(IEasyTransfer transfer)
    {
        Removed.Add(transfer);
        return RemoveResult;
    }

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken)
    {
        PerformedToken = cancellationToken;
        return ValueTask.FromResult(PerformResult);
    }

    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages)
    {
        var taken = Messages.TryDequeue(out message);
        remainingMessages = Messages.Count;
        return taken;
    }

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => throw new NotSupportedException();

    public CurlMultiCode Wakeup() => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) => throw new NotSupportedException();

    public CurlMultiCode Assign(CurlSocket socket, object? socketState) => throw new NotSupportedException();

    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout() => throw new NotSupportedException();

    public IReadOnlyList<IEasyTransfer> GetHandles() => throw new NotSupportedException();

    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount) => throw new NotSupportedException();

    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info) => throw new NotSupportedException();

    public CurlMultiCode EnableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public CurlMultiCode DisableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
