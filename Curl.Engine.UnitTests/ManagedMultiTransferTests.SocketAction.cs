using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Network;

namespace Curl.Engine;

/// <summary>The multi handle's socket-action API and its socket and timer callbacks (BL-2426).</summary>
public sealed partial class ManagedMultiTransferTests
{
    private static readonly CurlSocket FirstSocket = new(ManagedMultiTransfer.FirstTransferSocket);

    [TestMethod]
    public async Task SocketActionAsync_TransferDrivenOnlyBySocketActions_CallsTheCallbacksInLibcurlsOrder()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        ControlledEasyTransfer transfer = new();

        CurlMultiCode added = multi.Add(transfer);
        (CurlMultiCode Code, int RunningTransfers) started = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        CurlMultiCode assigned = multi.Assign(FirstSocket, "state");
        (CurlMultiCode Code, int RunningTransfers) stillRunning = await multi.SocketActionAsync(FirstSocket, CurlSocketSelect.In, CancellationToken.None);
        transfer.Finish(CurlCode.Ok);
        (CurlMultiCode Code, int RunningTransfers) finished = await multi.SocketActionAsync(FirstSocket, CurlSocketSelect.In, CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, added);
        Assert.AreEqual((CurlMultiCode.Ok, 1), started);
        Assert.AreEqual(CurlMultiCode.Ok, assigned);
        Assert.AreEqual((CurlMultiCode.Ok, 1), stillRunning);
        Assert.AreEqual((CurlMultiCode.Ok, 0), finished);
        Assert.AreEqual(1, transfer.PerformCount);
        CollectionAssert.AreEqual(
            new[] { "timer 0", $"socket {FirstSocket.Handle} In null", "timer 10", $"socket {FirstSocket.Handle} Remove state", "timer -1" },
            calls);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, transfer, CurlCode.Ok), message);
    }

    [TestMethod]
    public async Task SocketActionAsync_TwoTransfers_GivesEachItsOwnSocket()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        multi.Add(new ControlledEasyTransfer());
        multi.Add(new ControlledEasyTransfer());

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "timer 0", $"socket {FirstSocket.Handle} In null", $"socket {FirstSocket.Handle + 1} In null", "timer 10" },
            calls);
    }

    [TestMethod]
    public async Task SocketActionAsync_TimerFiresWithTheSameTimeout_ReportsItAgainButASocketEventDoesNot()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        multi.Add(new ControlledEasyTransfer());
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        calls.Clear();

        await multi.SocketActionAsync(FirstSocket, CurlSocketSelect.In, CancellationToken.None);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "timer 10" }, calls);
    }

    [TestMethod]
    public async Task SocketActionAsync_TimerFiresWithAnUnchangedZeroTimeout_ReportsZeroAgain()
    {
        List<string> calls = [];
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Options = RecordingOptions(calls);
        multi.Add(WithTimeout(TimeSpan.FromMilliseconds(4)));
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        time.Advance(4);
        calls.Clear();

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "timer 0", "timer 0" }, calls);
    }

    [TestMethod]
    public async Task SocketActionAsync_TransferTimeoutSoonerThanTheCheckInterval_IsTheTimer()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        multi.Add(WithTimeout(TimeSpan.FromMilliseconds(4)));

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual("timer 4", calls[^1]);
    }

    [TestMethod]
    public async Task SocketActionAsync_TransferFinishedBeforeItsFirstAction_IsGivenASocketThenRemovedOnTheNextAction()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        transfer.Finish(CurlCode.UnsupportedProtocol);

        (CurlMultiCode Code, int RunningTransfers) result = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 1), result);
        string socket = $"socket {ManagedMultiTransfer.FirstTransferSocket}";
        CollectionAssert.AreEqual(new[] { "timer 0", $"{socket} In null", "timer 0", $"{socket} Remove null", "timer -1" }, calls);
    }

    [TestMethod]
    public async Task SocketActionAsync_SocketCallbackReturnsMinusOne_AbortsAndEndsEveryTransferOnTheNextCall()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = new MultiOptions { Socket = (_, _, _, _) => -1 };
        ControlledEasyTransfer started = new();
        multi.Add(started);

        (CurlMultiCode Code, int RunningTransfers) aborted = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        ControlledEasyTransfer addedLater = new();
        multi.Add(addedLater);
        (CurlMultiCode Code, int RunningTransfers) after = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.AbortedByCallback, 1), aborted);
        Assert.AreEqual(CurlMultiCode.AbortedByCallback, after.Code);
        Assert.AreEqual(0, after.RunningTransfers);
        Assert.IsTrue(started.PerformToken.IsCancellationRequested);
        Assert.AreEqual(0, addedLater.PerformCount);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? first, out _));
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? second, out _));
        Assert.AreEqual(CurlCode.AbortedByCallback, first.Result);
        Assert.AreEqual(CurlCode.AbortedByCallback, second.Result);
    }

    [TestMethod]
    public async Task SocketActionAsync_AbortedBeforeTheSecondTransfersSocket_RemovesOnlyTheFirstsSocket()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = new MultiOptions
        {
            Socket = (_, socket, what, _) =>
            {
                calls.Add($"socket {socket.Handle} {what}");
                return calls.Count == 1 ? -1 : 0;
            },
        };
        multi.Add(new ControlledEasyTransfer());
        multi.Add(new ControlledEasyTransfer());

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        (CurlMultiCode Code, int RunningTransfers) after = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual(0, after.RunningTransfers);
        CollectionAssert.AreEqual(new[] { $"socket {FirstSocket.Handle} In", $"socket {FirstSocket.Handle} Remove" }, calls);
    }

    [TestMethod]
    public async Task Add_TimerCallbackReturnsMinusOne_AbortsAndTheTransferNeverStarts()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = new MultiOptions { Timer = _ => -1 };
        ControlledEasyTransfer transfer = new();

        CurlMultiCode added = multi.Add(transfer);
        (CurlMultiCode Code, int RunningTransfers) result = await multi.PerformAsync(CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.AbortedByCallback, added);
        Assert.AreEqual(0, result.RunningTransfers);
        Assert.AreEqual(0, transfer.PerformCount);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out int remaining));
        Assert.AreEqual(CurlCode.AbortedByCallback, message.Result);
        Assert.AreEqual(0, remaining);
    }

    [TestMethod]
    public async Task PerformAsync_SocketCallbackSet_TellsItOfTheTransfersSockets()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = new MultiOptions { Socket = RecordingOptions(calls).Socket };
        multi.Add(new ControlledEasyTransfer());

        await multi.PerformAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { $"socket {FirstSocket.Handle} In null" }, calls);
    }

    [TestMethod]
    public async Task Remove_TransferWithASocket_TellsTheSocketCallbackToRemoveIt()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = new MultiOptions { Socket = RecordingOptions(calls).Socket };
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        multi.Assign(FirstSocket, 7);

        CurlMultiCode removed = multi.Remove(transfer);

        Assert.AreEqual(CurlMultiCode.Ok, removed);
        CollectionAssert.AreEqual(new[] { $"socket {FirstSocket.Handle} In null", $"socket {FirstSocket.Handle} Remove 7" }, calls);
        Assert.AreEqual(CurlMultiCode.BadSocket, multi.Assign(FirstSocket, null));
    }

    [TestMethod]
    public async Task Remove_SocketCallbackClearedSinceTheSocketWasGiven_RemovesTheTransfer()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = new MultiOptions { Socket = (_, _, _, _) => 0 };
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        multi.Options = new MultiOptions();

        CurlMultiCode removed = multi.Remove(transfer);

        Assert.AreEqual(CurlMultiCode.Ok, removed);
        Assert.IsTrue(transfer.PerformToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task Assign_SocketTheHandleNeverGave_ReturnsBadSocket()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Add(new ControlledEasyTransfer());
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.BadSocket, multi.Assign(FirstSocket, "state"));
    }

    [TestMethod]
    public async Task SocketActionAndAssign_DisposedHandle_ReturnBadHandle()
    {
        ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        await multi.DisposeAsync();

        Assert.AreEqual((CurlMultiCode.BadHandle, 0), await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None));
        Assert.AreEqual(CurlMultiCode.BadHandle, multi.Assign(FirstSocket, null));
    }

    [TestMethod]
    public async Task SocketActionAsync_CancelledToken_ThrowsWithoutStartingAnything()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, new CancellationToken(canceled: true)));
        Assert.AreEqual(0, transfer.PerformCount);
    }

    [TestMethod]
    public async Task SocketActionAsync_ProductionEasyHandle_RunsToItsDoneMessage()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer multi = engine.CreateMulti();
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "bogus://h/" } };
        multi.Options = new MultiOptions { Socket = (_, _, _, _) => 0, Timer = _ => 0 };
        multi.Add(easy);
        using CancellationTokenSource limit = new(TimeSpan.FromSeconds(30));

        (_, int running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, limit.Token);
        while (running > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), limit.Token);
            (_, running) = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, limit.Token);
        }

        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.UnsupportedProtocol, message.Result);
    }

    private static MultiOptions RecordingOptions(List<string> calls) => new()
    {
        Socket = (_, socket, what, state) =>
        {
            calls.Add($"socket {socket.Handle} {what} {state ?? "null"}");
            return 0;
        },
        Timer = timeout =>
        {
            calls.Add($"timer {timeout}");
            return 0;
        },
    };
}
