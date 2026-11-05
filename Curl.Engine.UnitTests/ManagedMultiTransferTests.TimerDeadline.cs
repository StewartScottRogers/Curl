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

    private static readonly CurlSocket OtherSocket = new(0x4321);

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
