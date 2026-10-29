using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;

namespace Curl.Engine;

/// <summary>
/// CGR-148: <c>curl_multi_perform</c> never calls the socket callback, and a socket or timer callback's -1
/// kills the multi handle as libcurl 8.22.0's: the call answers <c>CURLM_ABORTED_BY_CALLBACK</c>, every later
/// socket action answers 0 with the transfer still running, no <c>CURLMSG_DONE</c> and no callback, while the
/// next perform ends the transfer with 42 and calls no callback (measured with libcurl-4.dll 8.22.0).
/// </summary>
[TestClass]
public sealed class Cgr148Tests
{
    [TestMethod]
    public async Task SocketActionAsync_SocketCallbackReturnsMinusOne_LeavesTheTransferRunningWithNoMessageAndNoMoreCallbacks()
    {
        List<string> calls = [];
        TimerManualTimeProvider time = new();
        await using ManagedCurlEngine engine = new(time, null);
        await using ManagedMultiTransfer multi = ManagedMultiTransferTests.NewMulti(time);
        multi.Options = RecordingOptions(calls, socketAnswer: _ => -1, timerAnswer: _ => 0);
        multi.SocketWaitSettleLimit = TimeSpan.FromSeconds(30);
        await using ManagedMultiTransferTests.WrappingEasyTransfer transfer = ManagedMultiTransferTests.ReadableWaitingTransfer(engine);
        multi.Add(transfer);
        CurlSocket socket = ManagedMultiTransferTests.WrappedSocket;

        List<(CurlMultiCode, int)> codes =
        [
            await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None),
            await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None),
            await multi.SocketActionAsync(socket, CurlSocketSelect.In, CancellationToken.None),
            await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None),
        ];

        CollectionAssert.AreEqual(new[] { (CurlMultiCode.AbortedByCallback, 1), (CurlMultiCode.Ok, 1), (CurlMultiCode.Ok, 1), (CurlMultiCode.Ok, 1) }, codes);
        CollectionAssert.AreEqual(new[] { "timer 0", $"socket {socket.Handle} In" }, calls);
        Assert.IsFalse(multi.TryReadInfo(out _, out _));
    }

    [TestMethod]
    public async Task SocketActionAsync_TimerCallbackReturnsMinusOneOnItsSecondCall_LeavesTheTransferRunningWithNoMessageAndNoMoreCallbacks()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = ManagedMultiTransferTests.NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls, socketAnswer: _ => 0, timerAnswer: count => count == 2 ? -1 : 0);
        ManagedMultiTransferTests.ControlledEasyTransfer transfer = new();
        multi.Add(transfer);

        List<(CurlMultiCode, int)> codes = await DriveWithSocketActionsAsync(multi, transfer);

        CollectionAssert.AreEqual(new[] { (CurlMultiCode.AbortedByCallback, 1), (CurlMultiCode.Ok, 1), (CurlMultiCode.Ok, 1), (CurlMultiCode.Ok, 1) }, codes);
        CollectionAssert.AreEqual(new[] { "timer 0", "timer 0" }, calls);
        Assert.IsFalse(multi.TryReadInfo(out _, out _));
    }

    [TestMethod]
    public async Task PerformAsync_SocketCallbackSet_NeverCallsItAndTheTransferCompletes()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = ManagedMultiTransferTests.NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls, socketAnswer: _ => -1, timerAnswer: _ => 0);
        ManagedMultiTransferTests.ControlledEasyTransfer transfer = new();
        multi.Add(transfer);

        (CurlMultiCode, int) started = await multi.PerformAsync(CancellationToken.None);
        transfer.Finish(CurlCode.Ok);
        (CurlMultiCode, int) finished = await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 1), started);
        Assert.AreEqual((CurlMultiCode.Ok, 0), finished);
        CollectionAssert.AreEqual(new[] { "timer 0", "timer -1" }, calls);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.Ok, message.Result);
    }

    [TestMethod]
    public async Task PerformAsync_TimerCallbackReturnsMinusOneOnItsSecondCall_EndsTheTransferWith42OnTheNextPerformWithNoMoreCallbacks()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = ManagedMultiTransferTests.NewMulti(new TimerManualTimeProvider());
        // Without a socket callback the timer is the transfer timeout once it runs, so the perform calls it a second time.
        multi.Options = RecordingOptions(calls, socketAnswer: _ => 0, timerAnswer: count => count == 2 ? -1 : 0) with { Socket = null };
        ManagedMultiTransferTests.ControlledEasyTransfer transfer = new() { Options = new EasyOptions { Transfer = new EasyTransferOptions { Timeout = TimeSpan.FromMilliseconds(4) } } };
        multi.Add(transfer);

        (CurlMultiCode, int) aborted = await multi.PerformAsync(CancellationToken.None);
        (CurlMultiCode, int) ended = await multi.PerformAsync(CancellationToken.None);
        (CurlMultiCode, int) after = await multi.PerformAsync(CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.AbortedByCallback, 1), aborted);
        Assert.AreEqual((CurlMultiCode.Ok, 0), ended);
        Assert.AreEqual((CurlMultiCode.Ok, 0), after);
        CollectionAssert.AreEqual(new[] { "timer 0", "timer 4" }, calls);
        Assert.IsTrue(multi.TryReadInfo(out CurlMultiMessage? message, out _));
        Assert.AreEqual(CurlCode.AbortedByCallback, message.Result);
        Assert.IsFalse(multi.TryReadInfo(out _, out _));
    }

    [TestMethod]
    public async Task SocketActionAsync_TransferPerformReportedDone_NeverTellsTheSocketCallbackOfIt()
    {
        List<string> calls = [];
        await using ManagedMultiTransfer multi = ManagedMultiTransferTests.NewMulti(new TimerManualTimeProvider());
        multi.Options = RecordingOptions(calls, socketAnswer: _ => 0, timerAnswer: _ => 0);
        ManagedMultiTransferTests.ControlledEasyTransfer transfer = new();
        multi.Add(transfer);
        await multi.PerformAsync(CancellationToken.None);
        transfer.Finish(CurlCode.Ok);
        await multi.PerformAsync(CancellationToken.None);

        (CurlMultiCode, int) result = await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 0), result);
        CollectionAssert.AreEqual(new[] { "timer 0", "timer -1" }, calls);
    }

    private static async Task<List<(CurlMultiCode, int)>> DriveWithSocketActionsAsync(ManagedMultiTransfer multi, ManagedMultiTransferTests.ControlledEasyTransfer transfer)
    {
        List<(CurlMultiCode, int)> codes = [];
        codes.Add(await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None));
        codes.Add(await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None));
        transfer.Finish(CurlCode.Ok);
        codes.Add(await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None));
        codes.Add(await multi.SocketActionAsync(CurlMultiSockets.Timeout, CurlSocketSelect.None, CancellationToken.None));
        return codes;
    }

    private static MultiOptions RecordingOptions(List<string> calls, Func<int, int> socketAnswer, Func<int, int> timerAnswer)
    {
        int socketCalls = 0;
        int timerCalls = 0;
        return new MultiOptions
        {
            Socket = (_, socket, what, _) =>
            {
                calls.Add($"socket {socket.Handle} {what}");
                return socketAnswer(++socketCalls);
            },
            Timer = timeout =>
            {
                calls.Add($"timer {timeout}");
                return timerAnswer(++timerCalls);
            },
        };
    }
}
