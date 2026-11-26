using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    [DataRow(false, DisplayName = "wait")]
    [DataRow(true, DisplayName = "poll")]
    public async Task WaitOrPoll_TransferFinishingBetweenTheReadyCountAndTheWaitForAnEvent_ReturnsWithoutTheTimeout(bool poll)
    {
        // The clock is read between counting the ready transfers and waiting for an event, so finishing the
        // transfer from that read lands in the gap a perform's own thread can finish it in (BL-5994).
        HookedTimeProvider time = new();
        await using ManagedMultiTransfer multi = NewMulti(time);
        ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);
        time.OnSecondReadFromNow(() => transfer.Finish(CurlCode.Ok));

        ValueTask<(CurlMultiCode Code, int ReadySockets)> waiting = poll
            ? multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(30), CancellationToken.None)
            : multi.WaitAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(30), CancellationToken.None);

        // The clock never moves, so only the finished transfer can end the wait; the limit here is the test's own.
        (CurlMultiCode code, int ready) = await waiting.AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(CurlMultiCode.Ok, code);
        Assert.AreEqual(1, ready);
    }

    /// <summary>A stopped clock that runs an action as one of its coming timestamp reads is made.</summary>
    private sealed class HookedTimeProvider : TimeProvider
    {
        private Action? action;
        private int readsLeft;

        public void OnSecondReadFromNow(Action run) => (action, readsLeft) = (run, 2);

        public override long GetTimestamp()
        {
            if (action is { } run && --readsLeft == 0)
            {
                action = null;
                run();
            }

            return 0;
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new TimerManualTimeProvider().CreateTimer(callback, state, dueTime, period);
    }
}
