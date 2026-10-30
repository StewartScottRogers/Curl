using System.Collections.Concurrent;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Curl.Engine;

/// <summary>The multi handle's timeout, wait, poll and wakeup (BL-2425), on a fake clock.</summary>
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task GetTimeout_NoTransfers_IsMinusOne()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());

        Assert.AreEqual((CurlMultiCode.Ok, -1L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task GetTimeout_TransferAddedButNotStarted_IsZero()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Add(new ControlledEasyTransfer());

        Assert.AreEqual((CurlMultiCode.Ok, 0L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task GetTimeout_TransferFinishedButNotReported_IsZeroUntilTheNextPerform()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        transfer.Finish(CurlCode.Ok);

        (CurlMultiCode Code, long TimeoutMilliseconds) beforePerform = multi.GetTimeout();
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 0L), beforePerform);
        Assert.AreEqual((CurlMultiCode.Ok, -1L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task GetTimeout_RunningTransfersWithTimeouts_CountsDownToTheFirstOnTheTimeProvider()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(WithTimeout(TimeSpan.FromSeconds(5)));
        multi.Add(WithTimeout(TimeSpan.FromSeconds(8)));
        multi.Add(new ControlledEasyTransfer());
        await multi.PerformAsync(CancellationToken.None);

        long atStart = multi.GetTimeout().TimeoutMilliseconds;
        time.Advance(1200);
        long afterSome = multi.GetTimeout().TimeoutMilliseconds;
        time.Advance(4000);
        long afterExpiry = multi.GetTimeout().TimeoutMilliseconds;

        Assert.AreEqual(5000, atStart);
        Assert.AreEqual(3800, afterSome);
        Assert.AreEqual(0, afterExpiry);
    }

    [TestMethod]
    public async Task GetTimeout_RunningTransferWithoutATimeout_IsMinusOne()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Add(new ControlledEasyTransfer());
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, -1L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task GetTimeout_TransferWithTimeoutHeldPendingByConnectionLimit_IsMinusOne()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Options = new MultiOptions { MaxTotalConnections = 1 };
        multi.Add(new ControlledEasyTransfer());
        multi.Add(WithTimeout(TimeSpan.FromSeconds(5)));
        await multi.PerformAsync(CancellationToken.None);
        time.Advance(1200);

        Assert.AreEqual((CurlMultiCode.Ok, -1L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task GetTimeout_FinishedTransferWithTimeoutAlreadyReported_IsMinusOne()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        ControlledEasyTransfer transfer = WithTimeout(TimeSpan.FromSeconds(5));
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        transfer.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, -1L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task WaitAsync_NothingToWaitOn_ReturnsAtOnce()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.IsTrue(wait.IsCompleted);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await wait);
    }

    [TestMethod]
    public async Task WaitAsync_RunningTransferFinishes_ReturnsWithItReady()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), CancellationToken.None);
        bool completedBeforeFinish = wait.IsCompleted;
        transfer.Finish(CurlCode.Ok);

        Assert.IsFalse(completedBeforeFinish);
        Assert.AreEqual((CurlMultiCode.Ok, 1), await wait);
    }

    [TestMethod]
    public async Task WaitAsync_TimeoutPasses_ReturnsWithNothingReady()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(new ControlledEasyTransfer());
        await multi.PerformAsync(CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(1000), CancellationToken.None);
        time.Advance(999);
        bool completedEarly = wait.IsCompleted;
        time.Advance(1);

        Assert.IsFalse(completedEarly);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await wait);
    }

    [TestMethod]
    public async Task WaitAsync_HandleTimeoutSoonerThanTheWait_EndsWhenTheTransferTimeoutExpires()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(WithTimeout(TimeSpan.FromMilliseconds(300)));
        await multi.PerformAsync(CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), CancellationToken.None);
        time.Advance(300);

        Assert.AreEqual((CurlMultiCode.Ok, 0), await wait);
    }

    [TestMethod]
    public async Task WaitAsync_TransferAddedButNotStarted_ReturnsAtOnceForThePerform()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Add(new ControlledEasyTransfer());

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.IsTrue(wait.IsCompleted);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await wait);
    }

    [TestMethod]
    public async Task WaitAsync_Cancelled_ThrowsOperationCanceled()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Add(new ControlledEasyTransfer());
        await multi.PerformAsync(CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);
        using CancellationTokenSource cancellation = new();

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await wait);
    }

    [TestMethod]
    public async Task PollAsync_NothingToWaitOn_WaitsTheWholeTimeout()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);

        ValueTask<(CurlMultiCode Code, int ReadySockets)> poll = multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(1000), CancellationToken.None);
        time.Advance(999);
        bool completedEarly = poll.IsCompleted;
        time.Advance(1);

        Assert.IsFalse(completedEarly);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await poll);
    }

    [TestMethod]
    public async Task Wakeup_DuringAPoll_EndsIt()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());

        ValueTask<(CurlMultiCode Code, int ReadySockets)> poll = multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), CancellationToken.None);
        bool completedBeforeWakeup = poll.IsCompleted;
        CurlMultiCode code = multi.Wakeup();

        Assert.IsFalse(completedBeforeWakeup);
        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await poll);
    }

    [TestMethod]
    public async Task Wakeup_BeforeAPoll_EndsOnlyTheNextPoll()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);

        multi.Wakeup();
        multi.Wakeup();
        ValueTask<(CurlMultiCode Code, int ReadySockets)> first = multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), CancellationToken.None);
        ValueTask<(CurlMultiCode Code, int ReadySockets)> second = multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        bool secondCompletedAtOnce = second.IsCompleted;
        time.Advance(50);

        Assert.IsTrue(first.IsCompleted);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await first);
        Assert.IsFalse(secondCompletedAtOnce);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await second);
    }

    [TestMethod]
    public async Task PollAsync_ExtraSocketsWithEvents_SetsTheirReturnedEventsAndCountsThem()
    {
        ScriptedWaitSocketPoller poller = new();
        poller.Ready[new CurlSocket(7)] = CurlWaitEvents.PollIn;
        poller.Ready[new CurlSocket(9)] = CurlWaitEvents.PollOut;
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider(), poller);
        CurlWaitSocket[] sockets =
        [
            new(new CurlSocket(7), CurlWaitEvents.PollIn | CurlWaitEvents.PollOut, CurlWaitEvents.PollPriority),
            new(new CurlSocket(8), CurlWaitEvents.PollIn, CurlWaitEvents.PollIn),
            new(new CurlSocket(9), CurlWaitEvents.PollOut, CurlWaitEvents.None),
        ];

        (CurlMultiCode Code, int ReadySockets) result = await multi.PollAsync(sockets, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 2), result);
        Assert.AreEqual(CurlWaitEvents.PollIn, sockets[0].ReturnedEvents);
        Assert.AreEqual(CurlWaitEvents.None, sockets[1].ReturnedEvents);
        Assert.AreEqual(CurlWaitEvents.PollOut, sockets[2].ReturnedEvents);
    }

    [TestMethod]
    public async Task PollAsync_EngineNumberedExtraSockets_ReportNoEventWithoutAskingThePoller()
    {
        ScriptedWaitSocketPoller poller = new();
        CurlSocket transferSocket = new((nint)(TransferSocketWait.FirstEngineSocket + 1));
        CurlSocket wakeupSocket = new(ManagedMultiTransfer.ResolverWakeupSocket);
        poller.Ready[transferSocket] = CurlWaitEvents.PollIn;
        poller.Ready[wakeupSocket] = CurlWaitEvents.PollIn;
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider(), poller);
        CurlWaitSocket[] sockets =
        [
            new(transferSocket, CurlWaitEvents.PollIn, CurlWaitEvents.None),
            new(wakeupSocket, CurlWaitEvents.PollIn, CurlWaitEvents.None),
        ];

        (CurlMultiCode Code, int ReadySockets) result = await multi.PollAsync(sockets, TimeSpan.Zero, CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 0), result);
        Assert.AreEqual(CurlWaitEvents.None, sockets[0].ReturnedEvents);
        Assert.AreEqual(CurlWaitEvents.None, sockets[1].ReturnedEvents);
    }

    [TestMethod]
    public async Task PollAsync_ExtraSocketGetsAnEventDuringThePoll_FindsItAtTheNextSocketPollInterval()
    {
        TimerManualTimeProvider time = new();
        ScriptedWaitSocketPoller poller = new();
        await using ManagedMultiTransfer multi = NewMulti(time, poller);
        CurlWaitSocket[] sockets = [new(new CurlSocket(7), CurlWaitEvents.PollIn, CurlWaitEvents.None)];

        ValueTask<(CurlMultiCode Code, int ReadySockets)> poll = multi.PollAsync(sockets, TimeSpan.FromSeconds(10), CancellationToken.None);
        bool completedBeforeEvent = poll.IsCompleted;
        poller.Ready[new CurlSocket(7)] = CurlWaitEvents.PollIn;
        time.Advance((long)ManagedMultiTransfer.SocketPollInterval.TotalMilliseconds);

        Assert.IsFalse(completedBeforeEvent);
        Assert.AreEqual((CurlMultiCode.Ok, 1), await poll);
        Assert.AreEqual(CurlWaitEvents.PollIn, sockets[0].ReturnedEvents);
    }

    [TestMethod]
    public async Task WaitAsync_ExtraSocketWithoutAnEvent_WaitsTheTimeoutInSocketPollIntervals()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        CurlWaitSocket[] sockets = [new(new CurlSocket(7), CurlWaitEvents.PollIn, CurlWaitEvents.None)];

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(sockets, TimeSpan.FromMilliseconds(15), CancellationToken.None);
        time.Advance(10);
        await Task.Yield();
        while (!wait.IsCompleted)
        {
            time.Advance(5);
            await Task.Yield();
        }

        Assert.AreEqual((CurlMultiCode.Ok, 0), await wait);
    }

    [TestMethod]
    public async Task PollAsync_TransfersWithNoConnectOnlySocket_AsksThePollerForTheExtraSocket()
    {
        ScriptedWaitSocketPoller poller = new();
        poller.Ready[new CurlSocket(7)] = CurlWaitEvents.PollOut;
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider(), poller);
        await using ManagedCurlEngine engine = new();
        await using IEasyTransfer managed = engine.CreateEasy();
        multi.Add(new ControlledEasyTransfer());
        multi.Add(managed);
        CurlWaitSocket[] sockets = [new(new CurlSocket(7), CurlWaitEvents.PollOut, CurlWaitEvents.None)];

        (CurlMultiCode Code, int ReadySockets) result = await multi.PollAsync(sockets, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 1), result);
        Assert.AreEqual(CurlWaitEvents.PollOut, sockets[0].ReturnedEvents);
    }

    [TestMethod]
    public async Task WaitAndPoll_NegativeTimeout_ReturnBadFunctionArgument()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());

        Assert.AreEqual((CurlMultiCode.BadFunctionArgument, 0), await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(-1), CancellationToken.None));
        Assert.AreEqual((CurlMultiCode.BadFunctionArgument, 0), await multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(-1), CancellationToken.None));
    }

    [TestMethod]
    public async Task TimerMembers_DisposedHandle_AnswerBadHandle()
    {
        ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        await multi.DisposeAsync();

        Assert.AreEqual((CurlMultiCode.BadHandle, -1L), multi.GetTimeout());
        Assert.AreEqual(CurlMultiCode.BadHandle, multi.Wakeup());
        Assert.AreEqual((CurlMultiCode.BadHandle, 0), await multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.Zero, CancellationToken.None));
        Assert.AreEqual((CurlMultiCode.BadHandle, 0), await multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.Zero, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetTimeout_EngineMulti_RunsOnTheEnginesTimeProvider()
    {
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using IMultiTransfer multi = engine.CreateMulti();
        multi.Add(WithTimeout(TimeSpan.FromSeconds(2)));
        await multi.PerformAsync(CancellationToken.None);

        time.Advance(500);

        Assert.AreEqual((CurlMultiCode.Ok, 1500L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task SocketActionAsync_TransferStillComputingAfterTheSettleLimit_AsksForTheTimerAtOnceNotAfterTheCheckInterval()
    {
        List<string> calls = [];
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.SocketWaitSettleLimit = TimeSpan.FromMilliseconds(1);
        multi.Options = RecordingOptions(calls);
        TaskCompletionSource<CurlCode> computed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using WrappingEasyTransfer transfer = new((ManagedEasyTransfer)engine.CreateEasy(), (_, _) => computed.Task);
        multi.Add(transfer);
        calls.Clear();

        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        computed.SetResult(CurlCode.Ok);

        CollectionAssert.AreEqual(new[] { "timer 0" }, calls, string.Join(" | ", calls));
    }

    internal static ManagedMultiTransfer NewMulti(TimeProvider time, IWaitSocketPoller? poller = null) =>
        new(new ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance), time, poller ?? new ScriptedWaitSocketPoller(), () => ValueTask.CompletedTask, _ => ValueTask.CompletedTask, () => { });

    private static ControlledEasyTransfer WithTimeout(TimeSpan timeout) =>
        new() { Options = new EasyOptions { Transfer = new EasyTransferOptions { Timeout = timeout } } };

    /// <summary>A socket poller whose sockets have the events a test puts in <see cref="Ready" />.</summary>
    private sealed class ScriptedWaitSocketPoller : IWaitSocketPoller
    {
        public ConcurrentDictionary<CurlSocket, CurlWaitEvents> Ready { get; } = new();

        public CurlWaitEvents Poll(CurlSocket socket, CurlWaitEvents events) =>
            Ready.TryGetValue(socket, out CurlWaitEvents ready) ? ready & events : CurlWaitEvents.None;
    }
}
