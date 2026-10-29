using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

[TestClass]
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task PerformAsync_SeveralTransfers_RunAtOnceAndQueueADoneMessageAsEachFinishes()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer first = new(), second = new(), third = new();
        Assert.AreEqual(CurlMultiCode.Ok, multi.Add(first));
        Assert.AreEqual(CurlMultiCode.Ok, multi.Add(second));
        Assert.AreEqual(CurlMultiCode.Ok, multi.Add(third));

        (CurlMultiCode startCode, int startRunning) = await multi.PerformAsync(CancellationToken.None);
        bool messageBeforeAnyFinished = multi.TryReadInfo(out _, out _);
        second.Finish(CurlCode.Ok);
        (_, int runningAfterSecond) = await multi.PerformAsync(CancellationToken.None);
        bool readSecond = multi.TryReadInfo(out CurlMultiMessage? secondMessage, out int leftAfterSecond);
        third.Finish(CurlCode.CouldntConnect);
        first.Finish(CurlCode.PartialFile);
        (_, int runningAtEnd) = await multi.PerformAsync(CancellationToken.None);
        bool readFirst = multi.TryReadInfo(out CurlMultiMessage? firstMessage, out int leftAfterFirst);
        bool readThird = multi.TryReadInfo(out CurlMultiMessage? thirdMessage, out int leftAfterThird);
        bool readMore = multi.TryReadInfo(out CurlMultiMessage? none, out int leftWhenEmpty);

        Assert.AreEqual(CurlMultiCode.Ok, startCode);
        Assert.AreEqual(3, startRunning);
        Assert.AreEqual(1, first.PerformCount);
        Assert.AreEqual(1, second.PerformCount);
        Assert.AreEqual(1, third.PerformCount);
        Assert.IsFalse(messageBeforeAnyFinished);
        Assert.AreEqual(2, runningAfterSecond);
        Assert.IsTrue(readSecond);
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, second, CurlCode.Ok), secondMessage);
        Assert.AreEqual(0, leftAfterSecond);
        Assert.AreEqual(0, runningAtEnd);
        Assert.IsTrue(readFirst);
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, first, CurlCode.PartialFile), firstMessage);
        Assert.AreEqual(1, leftAfterFirst);
        Assert.IsTrue(readThird);
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, third, CurlCode.CouldntConnect), thirdMessage);
        Assert.AreEqual(0, leftAfterThird);
        Assert.IsFalse(readMore);
        Assert.IsNull(none);
        Assert.AreEqual(0, leftWhenEmpty);
    }

    [TestMethod]
    public async Task PerformAsync_FinishedTransfer_QueuesItsMessageOnceAndDoesNotPerformItAgain()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        transfer.Finish(CurlCode.Ok);

        await multi.PerformAsync(CancellationToken.None);
        (_, int running) = await multi.PerformAsync(CancellationToken.None);
        multi.TryReadInfo(out _, out int remaining);

        Assert.AreEqual(0, running);
        Assert.AreEqual(0, remaining);
        Assert.AreEqual(1, transfer.PerformCount);
    }

    [TestMethod]
    public async Task PerformAsync_TransferFinishedAsItStarts_IsStillRunningAfterThatCallAndDoneAfterTheNext()
    {
        await using ManagedCurlEngine engine = new();
        await using SocketHoldingTransfer transfer = HoldingSocket(engine, 0x1001, NotAtAll);
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Add(transfer);
        CurlWaitSocket[] sockets = new CurlWaitSocket[2];

        (_, int runningAfterStart) = await multi.PerformAsync(CancellationToken.None);
        bool messageAfterStart = multi.TryReadInfo(out _, out _);
        multi.GetWaitSockets(sockets, out int socketsAfterStart);
        (_, int runningAfterNext) = await multi.PerformAsync(CancellationToken.None);
        bool messageAfterNext = multi.TryReadInfo(out CurlMultiMessage? message, out _);

        Assert.AreEqual(1, runningAfterStart);
        Assert.IsFalse(messageAfterStart);
        Assert.AreEqual(2, socketsAfterStart);
        Assert.AreEqual((nint)0x1001, sockets[0].Socket.Handle);
        Assert.AreEqual(0, runningAfterNext);
        Assert.IsTrue(messageAfterNext);
        Assert.AreEqual(CurlCode.Ok, message!.Result);
    }

    [TestMethod]
    public async Task PerformAsync_ProductionEasyHandles_RunToCompletionWithTheirCodes()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IEasyTransfer first = engine.CreateEasy();
        await using IEasyTransfer second = engine.CreateEasy();
        first.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "bogus://h/" } };
        second.Options = new EasyOptions();
        multi.Add(first);
        multi.Add(second);
        using CancellationTokenSource limit = new(TimeSpan.FromSeconds(30));

        (_, int running) = await multi.PerformAsync(limit.Token);
        while (running > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), limit.Token);
            (_, running) = await multi.PerformAsync(limit.Token);
        }

        multi.TryReadInfo(out CurlMultiMessage? firstMessage, out _);
        multi.TryReadInfo(out CurlMultiMessage? secondMessage, out _);
        Dictionary<IEasyTransfer, CurlCode> codes = new() { [firstMessage!.Transfer] = firstMessage.Result, [secondMessage!.Transfer] = secondMessage.Result };
        Assert.AreEqual(CurlCode.UnsupportedProtocol, codes[first]);
        Assert.AreEqual(CurlCode.UrlMalformat, codes[second]);
    }

    [TestMethod]
    public async Task PerformAsync_CancelledToken_ThrowsWithoutStartingAnything()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await multi.PerformAsync(new CancellationToken(canceled: true)));

        Assert.AreEqual(0, transfer.PerformCount);
    }

    [TestMethod]
    public async Task PerformAsync_TransferThrew_TheExceptionComesOut()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        transfer.Fail(new InvalidOperationException("boom"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await multi.PerformAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task Add_TransferInAMultiHandleOfTheEngine_ReturnsAddedAlready()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IMultiTransfer other = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);

        Assert.AreEqual(CurlMultiCode.AddedAlready, multi.Add(transfer));
        Assert.AreEqual(CurlMultiCode.AddedAlready, other.Add(transfer));
    }

    [TestMethod]
    public async Task AddAndRemove_NullTransfer_ReturnBadEasyHandle()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();

        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.Add(null!));
        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.Remove(null!));
    }

    [TestMethod]
    public async Task Remove_WhileRunning_CancelsItsPerformAndQueuesNoMessage()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer removed = new(), kept = new();
        multi.Add(removed);
        multi.Add(kept);
        await multi.PerformAsync(CancellationToken.None);

        CurlMultiCode code = multi.Remove(removed);
        kept.Finish(CurlCode.Ok);
        (_, int running) = await multi.PerformAsync(CancellationToken.None);
        multi.TryReadInfo(out CurlMultiMessage? message, out int remaining);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.IsTrue(removed.PerformToken.IsCancellationRequested);
        Assert.IsFalse(kept.PerformToken.IsCancellationRequested);
        Assert.AreEqual(0, running);
        Assert.AreSame(kept, message!.Transfer);
        Assert.AreEqual(0, remaining);
    }

    [TestMethod]
    public async Task Remove_FinishedTransferWithAnUnreadMessage_DropsTheMessage()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        transfer.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);

        multi.Remove(transfer);

        Assert.IsFalse(multi.TryReadInfo(out _, out _));
    }

    [TestMethod]
    public async Task Remove_TransferInNoMultiHandle_ReturnsOk()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();

        Assert.AreEqual(CurlMultiCode.Ok, multi.Remove(new ControlledEasyTransfer()));
    }

    [TestMethod]
    public async Task Remove_TransferInAnotherMultiHandle_ReturnsBadEasyHandleAndLeavesItThere()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IMultiTransfer other = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        other.Add(transfer);

        Assert.AreEqual(CurlMultiCode.BadEasyHandle, multi.Remove(transfer));
        Assert.AreEqual(CurlMultiCode.AddedAlready, multi.Add(transfer));
    }

    [TestMethod]
    public async Task Add_RemovedTransfer_StartsAfreshOnTheNextPerform()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        multi.Remove(transfer);

        CurlMultiCode code = multi.Add(transfer);
        (_, int running) = await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(1, running);
        Assert.AreEqual(2, transfer.PerformCount);
        Assert.IsFalse(transfer.PerformToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task DisposeAsync_RunningAndWaitingTransfers_CancelsThemAndLaterCallsAnswerBadHandle()
    {
        await using ManagedCurlEngine engine = new();
        IMultiTransfer multi = engine.CreateMulti();
        await using IMultiTransfer other = engine.CreateMulti();
        ControlledEasyTransfer running = new(), finished = new(), waiting = new();
        multi.Add(running);
        multi.Add(finished);
        finished.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);
        multi.Add(waiting);

        await multi.DisposeAsync();
        await multi.DisposeAsync();

        Assert.IsTrue(running.PerformToken.IsCancellationRequested);
        Assert.AreEqual(0, waiting.PerformCount);
        Assert.IsFalse(multi.TryReadInfo(out _, out _));
        Assert.AreEqual(CurlMultiCode.BadHandle, multi.Add(new ControlledEasyTransfer()));
        Assert.AreEqual(CurlMultiCode.BadHandle, multi.Remove(running));
        Assert.AreEqual((CurlMultiCode.BadHandle, 0), await multi.PerformAsync(CancellationToken.None));
        Assert.AreEqual(CurlMultiCode.Ok, other.Add(running));
        Assert.AreEqual(CurlMultiCode.Ok, other.Add(waiting));
    }

    [TestMethod]
    public async Task Options_NewHandle_AreTheDefaultsAndCanBeReplaced()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        MultiOptions replaced = new() { MaxTotalConnections = 3 };

        MultiOptions initial = multi.Options;
        multi.Options = replaced;

        Assert.AreEqual(new MultiOptions(), initial);
        Assert.AreSame(replaced, multi.Options);
    }

    /// <summary>A fake easy transfer whose perform finishes when the test says.</summary>
    internal sealed class ControlledEasyTransfer : IEasyTransfer
    {
        private TaskCompletionSource<CurlCode> finish = new();

        public int PerformCount { get; private set; }

        public CancellationToken PerformToken { get; private set; }

        public EasyOptions Options { get; set; } = new();

        public TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => throw new NotSupportedException();

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public void Finish(CurlCode code) => finish.SetResult(code);

        public void Fail(Exception exception) => finish.SetException(exception);

        public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            PerformCount++;
            PerformToken = cancellationToken;
            if (finish.Task.IsCompleted && PerformCount > 1)
            {
                finish = new();
            }

            TaskCompletionSource<CurlCode> current = finish;
            cancellationToken.Register(() => current.TrySetCanceled(cancellationToken));
            return new ValueTask<CurlCode>(current.Task);
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
