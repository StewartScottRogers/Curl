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

/// <summary>The multi handle's socket-action API and its socket and timer callbacks (BL-2426).</summary>
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task SocketActionAsync_TransferWithNoSocketDrivenByTimeoutActions_CallsOnlyTheTimerCallback()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        ControlledEasyTransfer transfer = new();

        CurlMultiCode added = multi.Add(transfer);
        (CurlMultiCode Code, int RunningTransfers) started = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        (CurlMultiCode Code, int RunningTransfers) stillRunning = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        transfer.Finish(CurlCode.Ok);
        (CurlMultiCode Code, int RunningTransfers) finished = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.Ok, added);
        Assert.AreEqual((CurlMultiCode.Ok, 1), started);
        Assert.AreEqual((CurlMultiCode.Ok, 1), stillRunning);
        Assert.AreEqual((CurlMultiCode.Ok, 0), finished);
        Assert.AreEqual(1, transfer.PerformCount);
        CollectionAssert.AreEqual(
            new[] { "timer 0", "timer 0", "timer 0", "timer -1" },
            calls);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(new CurlMultiMessage(CurlMultiMessageType.Done, transfer, CurlCode.Ok), message);
    }

    [TestMethod]
    public async Task SocketActionAsync_TwoTransfersWithNoSocket_TellTheSocketCallbackOfNone()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        multi.Add(new ControlledEasyTransfer());
        multi.Add(new ControlledEasyTransfer());

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "timer 0", "timer 0", "timer 0" },
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

        await multi.SocketActionAsync(WrappedSocket, CurlSocketSelect.In, CancellationToken.None);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "timer 0" }, calls);
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
    public async Task SocketActionAsync_RunningTransferWithATimeoutAndNoSocket_MakesTheTimerZero()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls);
        multi.Add(WithTimeout(TimeSpan.FromMilliseconds(4)));

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual("timer 0", calls[^1]);
    }

    [TestMethod]
    public async Task SocketActionAsync_TransferFinishedBeforeItsFirstAction_TellsTheSocketCallbackOfNoneAndEndsTheTimer()
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
        CollectionAssert.AreEqual(new[] { "timer 0", "timer 0", "timer -1" }, calls);
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
    public async Task Remove_TransferWithASocket_TellsTheSocketCallbackToRemoveIt()
    {
        List<string> calls = [];
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Options = new MultiOptions { Socket = RecordingOptions(calls).Socket };
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        await using WrappingEasyTransfer transfer = ReadableWaitingTransfer(engine);
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        multi.Assign(WrappedSocket, 7);

        CurlMultiCode removed = multi.Remove(transfer);

        Assert.AreEqual(CurlMultiCode.Ok, removed);
        CollectionAssert.AreEqual(new[] { $"socket {WrappedSocket.Handle} In null", $"socket {WrappedSocket.Handle} Remove 7" }, calls);
        Assert.AreEqual(CurlMultiCode.BadSocket, multi.Assign(WrappedSocket, null));
    }

    /// <summary>A wrapped managed transfer that takes <see cref="WrappedSocket" /> and waits until it is readable.</summary>
    internal static WrappingEasyTransfer ReadableWaitingTransfer(ManagedCurlEngine engine) =>
        new((ManagedEasyTransfer)engine.CreateEasy(), async (wait, token) =>
        {
            wait.TakeSocket(WrappedSocket.Handle);
            await wait.WaitUntilReadableAsync(token);
            return CurlCode.Ok;
        });

    [TestMethod]
    public async Task Remove_SocketCallbackClearedSinceTheSocketWasGiven_RemovesTheTransfer()
    {
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        List<CurlSocket> told = [];
        multi.Options = new MultiOptions { Socket = (_, socket, _, _) => { told.Add(socket); return 0; } };
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        await using WrappingEasyTransfer transfer = ReadableWaitingTransfer(engine);
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        multi.Options = new MultiOptions();

        CurlMultiCode removed = multi.Remove(transfer);

        Assert.AreEqual(CurlMultiCode.Ok, removed);
        CollectionAssert.AreEqual(new[] { WrappedSocket }, told);
        Assert.AreEqual(CurlMultiCode.BadSocket, multi.Assign(WrappedSocket, null));
    }

    [TestMethod]
    public async Task Assign_SocketTheHandleNeverGave_ReturnsBadSocket()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Add(new ControlledEasyTransfer());
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlMultiCode.BadSocket, multi.Assign(WrappedSocket, "state"));
    }

    [TestMethod]
    public async Task SocketActionAndAssign_DisposedHandle_ReturnBadHandle()
    {
        ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        await multi.DisposeAsync();

        Assert.AreEqual((CurlMultiCode.BadHandle, 0), await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None));
        Assert.AreEqual(CurlMultiCode.BadHandle, multi.Assign(WrappedSocket, null));
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

    [TestMethod]
    public async Task SocketActionAsync_WrapperOfAManagedTransfer_IsToldTheSocketOfItsOwnSocketWait()
    {
        List<string> calls = [];
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Options = RecordingOptions(calls);

        // The action lets the woken transfer run on to its end; a busy runner can take longer than the 200 ms default (BL-4050).
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        await using WrappingEasyTransfer transfer = new((ManagedEasyTransfer)engine.CreateEasy(), async (wait, token) =>
        {
            wait.TakeSocket(WrappedSocket.Handle);
            await wait.WaitUntilReadableAsync(token);
            return CurlCode.Ok;
        });
        multi.Add(transfer);

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        string[] started = [.. calls];
        (CurlMultiCode Code, int RunningTransfers) finished = await multi.SocketActionAsync(WrappedSocket, CurlSocketSelect.In, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "timer 0", $"socket {WrappedSocket.Handle} In null", "timer -1" }, started, string.Join(" | ", started));
        Assert.AreEqual((CurlMultiCode.Ok, 0), finished);
        Assert.AreEqual($"socket {WrappedSocket.Handle} Remove null", calls[3]);
    }

    [TestMethod]
    public void PerformingTransfer_TransferThatWrapsNone_IsItself()
    {
        ControlledEasyTransfer transfer = new();

        Assert.AreSame(transfer, ((IEasyTransfer)transfer).PerformingTransfer);
    }

    internal static readonly CurlSocket WrappedSocket = new(0x1234);

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

    /// <summary>
    /// A handle that is not a <see cref="ManagedEasyTransfer" /> but wraps one, as Curl.Api's store-bound handle does:
    /// its perform runs <c>perform</c> on the socket wait the multi handle gave the wrapped transfer.
    /// </summary>
    internal sealed class WrappingEasyTransfer(ManagedEasyTransfer inner, Func<TransferSocketWait, CancellationToken, Task<CurlCode>> perform) : IEasyTransfer
    {
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

        public async ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken) =>
            await perform(inner.SocketWait!, cancellationToken).ConfigureAwait(false);

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => inner.Reset();

        public CurlCode Pause(CurlPause directions) => inner.Pause(directions);

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => inner.UpkeepAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
