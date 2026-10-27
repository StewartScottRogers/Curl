using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;

namespace Curl.Engine;

/// <summary>CGR-012: <c>curl_multi_cleanup</c> called from inside a transfer's callback.</summary>
[TestClass]
public sealed class Cgr012Tests
{
    [TestMethod]
    public async Task DisposeAsync_FromInsideItsOwnTransfer_ReturnsAndLeavesTheTransferRunning()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        CleaningUpEasyTransfer transfer = new(multi);
        multi.Add(transfer);

        await multi.PerformAsync(CancellationToken.None);
        await transfer.Performed.WaitAsync(TimeSpan.FromSeconds(30));
        CurlMultiCode code;
        CurlMultiMessage? message;
        bool read;
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            await Task.Delay(1);
            (code, _) = await multi.PerformAsync(CancellationToken.None);
            read = multi.TryReadInfo(out message, out _);
        }
        while (!read && DateTime.UtcNow < deadline);

        Assert.IsTrue(transfer.InCallbackDuringPerform);
        Assert.IsFalse(multi.IsInTransferCallback);
        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.IsTrue(read);
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, transfer, CurlCode.Ok), message);
    }

    /// <summary>A fake transfer whose perform, as a write callback would, cleans up the multi handle running it.</summary>
    private sealed class CleaningUpEasyTransfer(IMultiTransfer multi) : IEasyTransfer
    {
        private readonly TaskCompletionSource performed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Performed => performed.Task;

        public bool InCallbackDuringPerform { get; private set; }

        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => throw new NotSupportedException();

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            InCallbackDuringPerform = multi.IsInTransferCallback;
            await multi.DisposeAsync();
            performed.SetResult();
            return CurlCode.Ok;
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
