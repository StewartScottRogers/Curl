using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Engine;
using Libcurl.Multi;
using Libcurl.UnitTests.Multi.Core;

namespace Libcurl.UnitTests;

/// <summary>CGR-012: <c>curl_multi_cleanup</c> called from inside a transfer callback answers <c>CURLM_RECURSIVE_API_CALL</c>.</summary>
[TestClass]
public sealed class Cgr012Tests
{
    [TestMethod]
    public async Task CleanupAsync_FromInsideATransferCallback_AnswersRecursiveApiCallAndTheTransferCompletes()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer engineMulti = engine.CreateMulti();
        CurlMulti multi = new(engineMulti);
        CleaningUpEasyTransfer transfer = new(multi);
        engineMulti.Add(transfer);

        await multi.PerformAsync();
        CurlMultiCode inCallback = await transfer.CleanupAnswer.WaitAsync(TimeSpan.FromSeconds(30));
        bool read;
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            await Task.Delay(1);
            await multi.PerformAsync();
            read = engineMulti.TryReadInfo(out _, out _);
        }
        while (!read && DateTime.UtcNow < deadline);
        bool cleanedUpBefore = multi.IsCleanedUp;
        CurlMultiCode final = await multi.CleanupAsync();

        Assert.AreEqual(CurlMultiCode.RecursiveApiCall, inCallback);
        Assert.IsTrue(read);
        Assert.IsFalse(cleanedUpBefore);
        Assert.AreEqual(CurlMultiCode.Ok, final);
    }

    [TestMethod]
    public async Task CleanupAsync_OutsideEveryCallback_AnswersOkThenBadHandle()
    {
        FakeMultiTransfer engineMulti = new();
        CurlMulti multi = new(engineMulti);

        CurlMultiCode first = await multi.CleanupAsync();
        CurlMultiCode second = await multi.CleanupAsync();

        Assert.AreEqual(CurlMultiCode.Ok, first);
        Assert.AreEqual(CurlMultiCode.BadHandle, second);
        Assert.AreEqual(1, engineMulti.DisposeCount);
    }

    /// <summary>A fake transfer whose perform, as a write callback would, calls <c>curl_multi_cleanup</c>.</summary>
    private sealed class CleaningUpEasyTransfer(CurlMulti multi) : IEasyTransfer
    {
        private readonly TaskCompletionSource<CurlMultiCode> cleanupAnswer = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CurlMultiCode> CleanupAnswer => cleanupAnswer.Task;

        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => throw new NotSupportedException();

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            cleanupAnswer.SetResult(await multi.CleanupAsync());
            return CurlCode.Ok;
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
