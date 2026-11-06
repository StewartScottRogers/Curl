using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

/// <summary>
/// The timer callback is not told a deadline again that moved by no more than a millisecond, and is told one that moved
/// further (AF-0672).
/// </summary>
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task SocketActionAsync_DeadlineMovedByExactlyOneMillisecond_IsNotToldAgainButOneMovedByTwoIs()
    {
        List<string> calls = [];
        FrozenUtcTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Options = RecordingOptions(calls);
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        TransferSocketWait? socketWait = null;
        await using WrappingEasyTransfer transfer = new((ManagedEasyTransfer)engine.CreateEasy(), async (wait, token) =>
        {
            socketWait = wait;
            wait.TakeSocket(WrappedSocket.Handle);
            wait.BeginExpectContinueWait(TimeSpan.FromMilliseconds(1000));
            await wait.WaitUntilReadableAsync(token);
            return CurlCode.Ok;
        });
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        string[] started = [.. calls];

        socketWait!.BeginExpectContinueWait(TimeSpan.FromMilliseconds(1001));
        await multi.SocketActionAsync(OtherSocket, CurlSocketSelect.None, CancellationToken.None);
        string[] afterOneMillisecond = [.. calls];
        socketWait.BeginExpectContinueWait(TimeSpan.FromMilliseconds(1002));
        await multi.SocketActionAsync(OtherSocket, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual("timer 1000", started[^1], string.Join(" | ", started));
        CollectionAssert.AreEqual(started, afterOneMillisecond, string.Join(" | ", afterOneMillisecond));
        Assert.AreEqual("timer 1002", calls[^1], string.Join(" | ", calls));
    }

    [TestMethod]
    public async Task SocketActionAsync_ClockMovingWhileTheTimerIsWorkedOut_DoesNotTellAnUnmovedDeadlineAgain()
    {
        // Each read of the clock is later than the one before by 2 ms more (BL-4897): a loaded runner pausing between
        // the timer's two reads once moved its due time past the millisecond tolerance, and the same deadline was told
        // again.
        List<string> calls = [];
        AcceleratingUtcTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Options = RecordingOptions(calls);
        multi.SocketWaitSettleLimit = TimeSpan.FromDays(1);
        await using WrappingEasyTransfer transfer = new((ManagedEasyTransfer)engine.CreateEasy(), async (wait, token) =>
        {
            wait.TakeSocket(WrappedSocket.Handle);
            wait.BeginExpectContinueWait(TimeSpan.FromDays(1));
            await wait.WaitUntilReadableAsync(token);
            return CurlCode.Ok;
        });
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        string[] started = [.. calls];

        await multi.SocketActionAsync(OtherSocket, CurlSocketSelect.None, CancellationToken.None);
        await multi.SocketActionAsync(OtherSocket, CurlSocketSelect.None, CancellationToken.None);

        StringAssert.StartsWith(started[^1], "timer ", string.Join(" | ", started));
        Assert.AreNotEqual("timer 0", started[^1], string.Join(" | ", started));
        CollectionAssert.AreEqual(started, calls, string.Join(" | ", calls));
    }

    private static readonly CurlSocket OtherSocket = new(0x4321);

    /// <summary>
    /// A clock whose time of day moves on by 2 ms more at each read than at the read before, so no two pairs of
    /// reads are the same time apart; its timers are <see cref="TimerManualTimeProvider" />'s.
    /// </summary>
    private sealed class AcceleratingUtcTimeProvider : TimeProvider
    {
        private readonly TimerManualTimeProvider timers = new();
        private readonly Lock gate = new();
        private DateTimeOffset now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        private TimeSpan step;

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate)
            {
                step += TimeSpan.FromMilliseconds(2);
                now += step;
                return now;
            }
        }

        public override long TimestampFrequency => timers.TimestampFrequency;

        public override long GetTimestamp() => timers.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            timers.CreateTimer(callback, state, dueTime, period);
    }

    /// <summary>
    /// A clock whose time of day never moves, so the deadlines the test sets are exactly the milliseconds apart it
    /// says; its timers are <see cref="TimerManualTimeProvider" />'s, which fire only when advanced.
    /// </summary>
    private sealed class FrozenUtcTimeProvider : TimeProvider
    {
        private readonly TimerManualTimeProvider timers = new();
        private readonly DateTimeOffset now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public override long TimestampFrequency => timers.TimestampFrequency;

        public override long GetTimestamp() => timers.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            timers.CreateTimer(callback, state, dueTime, period);
    }
}
