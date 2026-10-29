using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

/// <summary>
/// CGR-153: a <c>curl_multi_wakeup</c> pending when <c>curl_multi_poll</c> returns for transfer activity is
/// taken by that poll, as libcurl 8.22.0's <c>multi_wait</c> drains its wakeup socket after every poll, so
/// it does not end the next poll early.
/// </summary>
[TestClass]
public sealed class Cgr153Tests
{
    [TestMethod]
    public async Task PollAsync_ReturningForTransferActivityWithAWakeupPending_TakesTheWakeup()
    {
        TimerManualTimeProvider time = new();
        await using ManagedMultiTransfer multi = ManagedMultiTransferTests.NewMulti(time);
        multi.Add(new ManagedMultiTransferTests.ControlledEasyTransfer());
        await multi.PerformAsync(CancellationToken.None);
        multi.Wakeup();

        (CurlMultiCode Code, int ReadySockets) first = await multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(1), CancellationToken.None);
        await multi.PerformAsync(CancellationToken.None);
        ValueTask<(CurlMultiCode Code, int ReadySockets)> second = multi.PollAsync(Memory<CurlWaitSocket>.Empty, TimeSpan.FromSeconds(1), CancellationToken.None);
        bool secondCompletedAtOnce = second.IsCompleted;
        time.Advance(1000);

        Assert.AreEqual((CurlMultiCode.Ok, 1), first);
        Assert.IsFalse(secondCompletedAtOnce);
        Assert.AreEqual((CurlMultiCode.Ok, 0), await second);
    }
}
