using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Engine;
using Libcurl.Easy;
using Libcurl.Global;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="ParallelTransferMulti" /> (ADR-0628): each attempt adds its easy handle to the run's one multi
/// handle, its transfer starts in the attempt's own flow, and it ends with its own <c>CURLMSG_DONE</c> result in
/// whatever order the transfers finish, its handle removed again.
/// </summary>
[TestClass]
public sealed class ParallelTransferMultiTests
{
    private static readonly AsyncLocal<string?> Flow = new();

    [TestMethod]
    public async Task PerformAsync_TwoAttemptsFinishingInReverse_EachEndsWithItsOwnResult()
    {
        ControlledCurlEngine engine = new();
        CurlLibrary library = CurlGlobal.Init(engine);
        CurlEasy first = library.EasyInit();
        CurlEasy second = library.EasyInit();
        ParallelTransferMulti multi = new(library.MultiInit());

        Task<CurlCode> firstEnded = PerformInFlowAsync(multi, first, "first");
        await engine.Transfers[0].Started;
        Task<CurlCode> secondEnded = PerformInFlowAsync(multi, second, "second");
        await engine.Transfers[1].Started;
        engine.Transfers[1].Finish(CurlCode.CouldntConnect);
        CurlCode secondResult = await secondEnded;
        bool firstStillRunning = !firstEnded.IsCompleted;
        engine.Transfers[0].Finish(CurlCode.Ok);
        CurlCode firstResult = await firstEnded;
        await multi.DisposeAsync();

        Assert.AreEqual(CurlCode.CouldntConnect, secondResult);
        Assert.IsTrue(firstStillRunning);
        Assert.AreEqual(CurlCode.Ok, firstResult);
        CollectionAssert.AreEqual(new[] { "first", "second" }, engine.Transfers.Select(transfer => transfer.StartedInFlow).ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_SameHandleAgainAfterItEnded_IsAddedAgainAndPerformedAgain()
    {
        ControlledCurlEngine engine = new() { FinishesAt = CurlCode.Ok };
        CurlLibrary library = CurlGlobal.Init(engine);
        CurlEasy easy = library.EasyInit();
        ParallelTransferMulti multi = new(library.MultiInit());

        CurlCode firstResult = await multi.PerformAsync(easy);
        CurlCode secondResult = await multi.PerformAsync(easy);
        await multi.DisposeAsync();

        Assert.AreEqual(CurlCode.Ok, firstResult);
        Assert.AreEqual(CurlCode.Ok, secondResult);
        Assert.AreEqual(2, engine.Transfers[0].Performs);
    }

    [TestMethod]
    public async Task PerformAsync_ATransferThrows_EveryWaitingAttemptEndsWithTheException()
    {
        ControlledCurlEngine engine = new();
        CurlLibrary library = CurlGlobal.Init(engine);
        CurlEasy first = library.EasyInit();
        CurlEasy second = library.EasyInit();
        ParallelTransferMulti multi = new(library.MultiInit());
        InvalidOperationException thrown = new("handler fault");

        Task<CurlCode> firstEnded = multi.PerformAsync(first);
        await engine.Transfers[0].Started;
        Task<CurlCode> secondEnded = multi.PerformAsync(second);
        await engine.Transfers[1].Started;
        engine.Transfers[0].Fail(thrown);

        InvalidOperationException firstException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => firstEnded);
        InvalidOperationException secondException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => secondEnded);
        await multi.DisposeAsync();
        Assert.AreSame(thrown, firstException);
        Assert.AreSame(thrown, secondException);
    }

    private static async Task<CurlCode> PerformInFlowAsync(ParallelTransferMulti multi, CurlEasy easy, string flow)
    {
        Flow.Value = flow;
        return await multi.PerformAsync(easy);
    }

    /// <summary>An engine whose easy transfers finish when the test says, over the managed engine's multi handle.</summary>
    private sealed class ControlledCurlEngine : ICurlEngine
    {
        private readonly ManagedCurlEngine inner = new();

        public CurlCode? FinishesAt { get; init; }

        public List<ControlledEasyTransfer> Transfers { get; } = [];

        public CurlVersionInfo VersionInfo => inner.VersionInfo;

        public IEasyTransfer CreateEasy()
        {
            ControlledEasyTransfer transfer = new(FinishesAt);
            Transfers.Add(transfer);
            return transfer;
        }

        public IMultiTransfer CreateMulti() => inner.CreateMulti();

        public IShareHandle CreateShare() => throw new NotSupportedException();

        public ICurlUrlHandle CreateUrl() => throw new NotSupportedException();

        public IMimeBuilder CreateMime() => throw new NotSupportedException();

        public ICurlFormBuilder CreateForm() => throw new NotSupportedException();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    /// <summary>An easy transfer whose perform ends when the test says, or at once with <paramref name="finishesAt" />.</summary>
    /// <param name="finishesAt">The result every perform ends with at once, or <see langword="null" /> to wait for the test.</param>
    private sealed class ControlledEasyTransfer(CurlCode? finishesAt) : IEasyTransfer
    {
        private readonly TaskCompletionSource<CurlCode> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => started.Task;

        public string? StartedInFlow { get; private set; }

        public int Performs { get; private set; }

        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => string.Empty;

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public void Finish(CurlCode code) => finish.SetResult(code);

        public void Fail(Exception exception) => finish.SetException(exception);

        public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            Performs++;
            StartedInFlow = Flow.Value;
            started.TrySetResult();
            return finishesAt is { } code ? ValueTask.FromResult(code) : new ValueTask<CurlCode>(finish.Task);
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
