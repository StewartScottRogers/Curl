using System.Collections.Concurrent;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.ConnectOnly;
using Curl.Api.EngineContracts.Headers;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Curl.Engine;

/// <summary>
/// The wait a multi handle's wait and poll compute for a running transfer with a timeout (BL-3206): the
/// time left until the timeout while it is ahead, and none once it has passed, when the transfer's own
/// perform ends it and the wait ends with that perform, so a perform-and-poll loop never spins.
/// </summary>
[TestClass]
public sealed class MultiPollTimeoutWaitTests
{
    [TestMethod]
    public async Task WaitTimerMilliseconds_TimeoutAhead_IsTheMillisecondsLeft()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(new TimedTransfer(TimeSpan.FromMilliseconds(300)));
        await multi.PerformAsync(CancellationToken.None);

        time.Advance(100);

        Assert.AreEqual(200L, multi.WaitTimerMilliseconds());
        Assert.AreEqual((CurlMultiCode.Ok, 200L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task WaitTimerMilliseconds_TimeoutPassedWhileThePerformRuns_IsMinusOneThoughGetTimeoutIsZero()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(new TimedTransfer(TimeSpan.FromMilliseconds(300)));
        await multi.PerformAsync(CancellationToken.None);

        time.Advance(300);

        Assert.AreEqual(-1L, multi.WaitTimerMilliseconds());
        Assert.AreEqual((CurlMultiCode.Ok, 0L), multi.GetTimeout());
    }

    [TestMethod]
    public async Task WaitTimerMilliseconds_OneTimeoutPassedAndOneAhead_IsTheOneAhead()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(new TimedTransfer(TimeSpan.FromMilliseconds(300)));
        multi.Add(new TimedTransfer(TimeSpan.FromMilliseconds(800)));
        await multi.PerformAsync(CancellationToken.None);

        time.Advance(500);

        Assert.AreEqual(300L, multi.WaitTimerMilliseconds());
    }

    [TestMethod]
    public async Task WaitTimerMilliseconds_TransferAddedButNotStarted_IsZero()
    {
        await using ManagedMultiTransfer multi = NewMulti(new TimerManualTimeProvider());
        multi.Add(new TimedTransfer(TimeSpan.FromMilliseconds(300)));

        Assert.AreEqual(0L, multi.WaitTimerMilliseconds());
    }

    [TestMethod]
    public async Task PollAsync_TimeoutAheadSoonerThanThePoll_EndsWhenItExpires()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(new TimedTransfer(TimeSpan.FromMilliseconds(300)));
        await multi.PerformAsync(CancellationToken.None);

        ValueTask<(CurlMultiCode Code, int ReadySockets)> poll = multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(10), CancellationToken.None);
        time.Advance(299);
        bool completedEarly = poll.IsCompleted;
        time.Advance(1);

        Assert.IsFalse(completedEarly);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await poll);
    }

    [TestMethod]
    public async Task PollAsync_TimeoutPassedWhileThePerformRuns_WaitsUntilThePerformEnds()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        TimedTransfer transfer = new(TimeSpan.FromMilliseconds(300));
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        time.Advance(300);

        ValueTask<(CurlMultiCode Code, int ReadySockets)> poll = multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        time.Advance(20);
        bool completedBeforeThePerformEnded = poll.IsCompleted;
        transfer.Finish(CurlCode.OperationTimedOut);
        (CurlMultiCode Code, int ReadySockets) result = await poll;
        await multi.PerformAsync(CancellationToken.None);

        Assert.IsFalse(completedBeforeThePerformEnded);
        Assert.AreEqual((CurlMultiCode.Ok, 1), result);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.OperationTimedOut, message!.Result);
    }

    [TestMethod]
    public async Task WaitAsync_TimeoutPassedWhileThePerformRuns_WaitsTheWholeTimeout()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Add(new TimedTransfer(TimeSpan.FromMilliseconds(300)));
        await multi.PerformAsync(CancellationToken.None);
        time.Advance(300);

        ValueTask<(CurlMultiCode Code, int ReadySockets)> wait = multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        time.Advance(99);
        bool completedEarly = wait.IsCompleted;
        time.Advance(1);

        Assert.IsFalse(completedEarly);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await wait);
    }

    private static ManagedMultiTransfer NewMulti(TimeProvider time) =>
        new(new ConcurrentDictionary<IEasyTransfer, ManagedMultiTransfer>(ReferenceEqualityComparer.Instance), time, SocketWaitPoller.Instance, () => ValueTask.CompletedTask, _ => ValueTask.CompletedTask, () => { });

    /// <summary>A fake transfer with <c>CURLOPT_TIMEOUT</c> set, whose perform ends when the test says.</summary>
    private sealed class TimedTransfer(TimeSpan timeout) : IEasyTransfer
    {
        private readonly TaskCompletionSource<CurlCode> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EasyOptions Options { get; set; } = new() { Transfer = new EasyTransferOptions { Timeout = timeout } };

        public TransferInfo Info => throw new NotSupportedException();

        public string ErrorText => throw new NotSupportedException();

        public ITransferHeaders Headers => throw new NotSupportedException();

        public IEasyWebSocket WebSocket => throw new NotSupportedException();

        public IConnectOnlyConnection Connection => throw new NotSupportedException();

        public void Finish(CurlCode code) => finish.TrySetResult(code);

        public ValueTask<CurlCode> PerformAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => finish.TrySetCanceled(cancellationToken));
            return new ValueTask<CurlCode>(finish.Task);
        }

        public IEasyTransfer Duplicate() => throw new NotSupportedException();

        public void Reset() => throw new NotSupportedException();

        public CurlCode Pause(CurlPause directions) => throw new NotSupportedException();

        public ValueTask<CurlCode> UpkeepAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
