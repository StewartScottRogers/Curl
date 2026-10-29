using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

/// <summary>
/// Transfers that hold a real socket of their own, so the multi handle's tests that count told or waited sockets
/// count that socket rather than the engine's synthetic transfer numbers (BL-4070).
/// </summary>
public sealed partial class ManagedMultiTransferTests
{
    /// <summary>
    /// A transfer to <paramref name="url" /> whose perform takes <paramref name="socket" /> as its own and then
    /// runs <paramref name="waitAfterTakingSocket" />.
    /// </summary>
    private static SocketHoldingTransfer HoldingSocket(
        ManagedCurlEngine engine,
        nint socket,
        Func<TransferSocketWait, CancellationToken, Task> waitAfterTakingSocket,
        string url = "http://h.example/",
        HttpVersion version = HttpVersion.None,
        bool pipeWait = false)
    {
        SocketHoldingTransfer transfer = new((ManagedEasyTransfer)engine.CreateEasy(), socket, waitAfterTakingSocket);
        transfer.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Http = new EasyHttpOptions { HttpVersion = version, PipeWait = pipeWait },
        };
        return transfer;
    }

    private static Task UntilReadable(TransferSocketWait wait, CancellationToken cancellationToken) =>
        wait.WaitUntilReadableAsync(cancellationToken).AsTask();

    private static Task UntilCancelled(TransferSocketWait wait, CancellationToken cancellationToken) =>
        Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

    private static Task NotAtAll(TransferSocketWait wait, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// A handle that wraps a <see cref="ManagedEasyTransfer" />: its perform takes its socket on the socket wait
    /// the multi handle gave the wrapped transfer, sets <see cref="SocketTaken" />, waits as it was given and
    /// ends with <see cref="CurlCode.Ok" />.
    /// </summary>
    private sealed class SocketHoldingTransfer(ManagedEasyTransfer inner, nint socket, Func<TransferSocketWait, CancellationToken, Task> waitAfterTakingSocket) : IEasyTransfer
    {
        public TaskCompletionSource SocketTaken { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EasyOptions Options
        {
            get => inner.Options;
            set => inner.Options = value;
        }

        public TransferInfo Info => inner.Info;

        public string ErrorText => inner.ErrorText;

        public ITransferHeaders Headers => inner.Headers;

        public IEasyWebSocket WebSocket => inner.WebSocket;

        public IConnectOnlyConnection Connection => inner.Connection;

        public IEasyTransfer PerformingTransfer => inner;

        public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            TransferSocketWait wait = inner.SocketWait!;
            wait.TakeSocket(socket);
            SocketTaken.TrySetResult();
            await waitAfterTakingSocket(wait, cancellationToken).ConfigureAwait(false);
            return CurlCode.Ok;
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => inner.Reset();

        public CurlCode Pause(CurlPause directions) => inner.Pause(directions);

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => inner.UpkeepAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
