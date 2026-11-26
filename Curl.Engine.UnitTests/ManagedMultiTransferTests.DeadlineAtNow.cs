using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;

namespace Curl.Engine;

public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task SocketActionAsync_DeadlineExactlyNow_LetsTheTransferGoOnUntilItTimesOut()
    {
        SettableUtcClock time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = NewMulti(time);
        multi.Options = RecordingOptions([]);
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        TaskCompletionSource timedOut = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using WrappingEasyTransfer transfer = new((ManagedEasyTransfer)engine.CreateEasy(), async (wait, token) =>
        {
            wait.BeginExpectContinueWait(TimeSpan.FromMilliseconds(500));
            wait.TakeSocket(WrappedSocket.Handle);
            await await Task.WhenAny(wait.WaitUntilReadableAsync(token).AsTask(), timedOut.Task);
            return CurlCode.OperationTimedOut;
        });
        multi.Add(transfer);
        await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        time.Now += TimeSpan.FromMilliseconds(500);

        // The transfer's own timer ends it a real second later, so only an action that waits for it sees it end.
        Task ownTimer = Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ => timedOut.SetResult(), TaskScheduler.Default);
        (CurlMultiCode Code, int RunningTransfers) finished = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);
        await ownTimer;

        Assert.AreEqual((CurlMultiCode.Ok, 0), finished);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.OperationTimedOut, message.Result);
    }

    // A clock whose wall time stands still until the test sets it, so it can stop exactly on a deadline.
    private sealed class SettableUtcClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
