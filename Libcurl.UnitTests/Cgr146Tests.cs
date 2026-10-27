using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Engine;
using Libcurl.Easy;
using Libcurl.Multi;
using Libcurl.Options;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-146: <c>curl_multi_add_handle</c> called from inside one of the multi handle's transfer callbacks answers
/// <c>CURLM_RECURSIVE_API_CALL</c> (8) even for a handle the multi handle holds already, and
/// <c>curl_multi_info_read</c> answers no message there, as libcurl 8.22.0 does.
/// </summary>
[TestClass]
public sealed class Cgr146Tests
{
    [TestMethod]
    public async Task AddHandleAndInfoRead_FromInsideATransferCallback_AnswerRecursiveApiCallAndNoMessage()
    {
        await using ManagedCurlEngine engine = new();
        await using CurlMulti multi = new(engine.CreateMulti());
        CallingBackEasyTransfer transfer = new();
        using CurlEasy easy = new(transfer, EasyOptionDispatch.Default);
        CurlEasy other = new(new CallingBackEasyTransfer(), EasyOptionDispatch.Default);
        transfer.Callback = () => (multi.AddHandle(easy), multi.AddHandle(other), multi.InfoRead(out _) is null);
        Assert.AreEqual(CurlMultiCode.Ok, multi.AddHandle(easy));

        await multi.PerformAsync();
        (CurlMultiCode again, CurlMultiCode added, bool noMessage) = await transfer.Answers.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.AreEqual(CurlMultiCode.RecursiveApiCall, again);
        Assert.AreEqual(CurlMultiCode.RecursiveApiCall, added);
        Assert.IsTrue(noMessage);
        Assert.AreEqual(CurlMultiCode.AddedAlready, multi.AddHandle(easy));
        other.Dispose();
    }

    /// <summary>A fake transfer whose perform, as a write callback would, runs <see cref="Callback"/>.</summary>
    private sealed class CallingBackEasyTransfer : IEasyTransfer
    {
        private readonly TaskCompletionSource<(CurlMultiCode, CurlMultiCode, bool)> answers = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<(CurlMultiCode, CurlMultiCode, bool)> Callback { get; set; } = () => default;

        public Task<(CurlMultiCode, CurlMultiCode, bool)> Answers => answers.Task;

        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => throw new NotSupportedException();

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            answers.SetResult(Callback());
            return CurlCode.Ok;
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
