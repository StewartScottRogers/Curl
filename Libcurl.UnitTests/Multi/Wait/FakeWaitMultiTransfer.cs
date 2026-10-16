using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;

namespace Libcurl.UnitTests.Multi.Wait;

/// <summary>
/// An <see cref="IMultiTransfer"/> for the wait area: <see cref="WaitAsync"/> and <see cref="PollAsync"/>
/// record what they were given, mark every extra socket as having its events, and answer
/// <see cref="WaitResult"/>; <see cref="Wakeup"/> counts its calls and answers <see cref="WakeupResult"/>;
/// <see cref="GetTimeout"/> answers <see cref="TimeoutResult"/>; and <see cref="GetWaitSockets"/> reports
/// <see cref="WaitSockets"/> as libcurl's <c>curl_multi_waitfds</c> does, or <see cref="WaitSocketsResult"/>
/// when that is set, from call <see cref="WaitSocketsResultFromCall"/> on. Its other members are not called by these tests.
/// </summary>
internal sealed class FakeWaitMultiTransfer : IMultiTransfer
{
    public (CurlMultiCode Code, int ReadySockets) WaitResult { get; set; }

    public CurlMultiCode WakeupResult { get; set; } = CurlMultiCode.Ok;

    public (CurlMultiCode Code, long TimeoutMilliseconds) TimeoutResult { get; set; }

    public List<CurlWaitSocket> WaitSockets { get; } = [];

    public CurlMultiCode? WaitSocketsResult { get; set; }

    public int WaitSocketsResultFromCall { get; set; } = 1;

    public int WaitSocketsCalls { get; private set; }

    public string? LastWait { get; private set; }

    public TimeSpan LastTimeout { get; private set; }

    public CancellationToken LastToken { get; private set; }

    public int WakeupCount { get; private set; }

    public MultiOptions Options { get; set; } = new();

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> WaitAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) =>
        Record(nameof(WaitAsync), extraSockets, timeout, cancellationToken);

    public ValueTask<(CurlMultiCode Code, int ReadySockets)> PollAsync(Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken) =>
        Record(nameof(PollAsync), extraSockets, timeout, cancellationToken);

    public CurlMultiCode Wakeup()
    {
        WakeupCount++;
        return WakeupResult;
    }

    public (CurlMultiCode Code, long TimeoutMilliseconds) GetTimeout() => TimeoutResult;

    public CurlMultiCode GetWaitSockets(Span<CurlWaitSocket> sockets, out int socketCount)
    {
        WaitSocketsCalls++;
        socketCount = WaitSockets.Count;
        if (WaitSocketsResult is { } result && WaitSocketsCalls >= WaitSocketsResultFromCall)
        {
            return result;
        }

        if (sockets.Length < WaitSockets.Count)
        {
            return CurlMultiCode.OutOfMemory;
        }

        WaitSockets.CopyTo(sockets);
        return CurlMultiCode.Ok;
    }

    public CurlMultiCode Add(IEasyTransfer transfer) => throw new NotSupportedException();

    public CurlMultiCode Remove(IEasyTransfer transfer) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> PerformAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public bool TryReadInfo([NotNullWhen(true)] out CurlMultiMessage? message, out int remainingMessages) => throw new NotSupportedException();

    public ValueTask<(CurlMultiCode Code, int RunningTransfers)> SocketActionAsync(CurlSocket socket, CurlSocketSelect events, CancellationToken cancellationToken) => throw new NotSupportedException();

    public CurlMultiCode Assign(CurlSocket socket, object? socketState) => throw new NotSupportedException();

    public IReadOnlyList<IEasyTransfer> GetHandles() => throw new NotSupportedException();

    public (CurlMultiCode Code, long Value) GetInfo(CurlMultiInfo info) => throw new NotSupportedException();

    public CurlMultiCode EnableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public CurlMultiCode DisableNotification(CurlMultiNotification notification) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ValueTask<(CurlMultiCode Code, int ReadySockets)> Record(string wait, Memory<CurlWaitSocket> extraSockets, TimeSpan timeout, CancellationToken cancellationToken)
    {
        LastWait = wait;
        LastTimeout = timeout;
        LastToken = cancellationToken;
        var span = extraSockets.Span;
        for (var index = 0; index < span.Length; index++)
        {
            span[index] = span[index] with { ReturnedEvents = span[index].Events };
        }

        return ValueTask.FromResult(WaitResult);
    }
}
