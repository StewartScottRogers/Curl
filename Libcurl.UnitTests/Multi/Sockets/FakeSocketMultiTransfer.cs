using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Libcurl.UnitTests.Multi.Sockets;

/// <summary>
/// An <see cref="IMultiTransfer"/> for the socket-action API: <see cref="IMultiTransfer.SocketActionAsync"/>
/// answers <see cref="SocketActionResult"/> and records its arguments, <see cref="IMultiTransfer.Assign"/>
/// answers <see cref="AssignResult"/> and records its arguments, and <see cref="IMultiTransfer.Add"/>
/// accepts every transfer. Its other members are not called by these tests.
/// </summary>
internal sealed class FakeSocketMultiTransfer : IMultiTransfer
{
    public (CurlMultiCode Code, int RunningTransfers) SocketActionResult { get; set; }

    public (CurlSocket Socket, CurlSocketSelect Events, CancellationToken Token)? SocketAction { get; private set; }

    public CurlMultiCode AssignResult { get; set; } = CurlMultiCode.Ok;

    public (CurlSocket Socket, object? State)? Assigned { get; private set; }

    public MultiOptions Options { get; set; } = new();

    public CurlMultiCode Add(IEasyTransfer transfer) => CurlMultiCode.Ok;

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken)
    {
        SocketAction = (socket, events, cancellationToken);
        return ValueTask.FromResult(SocketActionResult);
    }

    public CurlMultiCode Assign(CurlSocket socket, object? socketState)
    {
        Assigned = (socket, socketState);
        return AssignResult;
    }

    public CurlMultiCode Remove(IEasyTransfer transfer) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) => throw new NotSupportedException();

    public CurlMultiCode Wakeup() => throw new NotSupportedException();

    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout() => throw new NotSupportedException();

    public IReadOnlyList<IEasyTransfer> GetHandles() => throw new NotSupportedException();

    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount) => throw new NotSupportedException();

    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info) => throw new NotSupportedException();

    public CurlMultiCode EnableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public CurlMultiCode DisableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
