using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options;

namespace Curl.Engine;

/// <summary>
/// A second multi handle polling the sockets the first one's <c>curl_multi_waitfds</c> gave, as upstream's
/// lib2405 does (upstream test2407, BL-5943, ADR-1388).
/// </summary>
public sealed partial class ManagedMultiTransferTests
{
    [TestMethod]
    public async Task PollAsync_SocketOfAnotherHandleWithACallbackQueued_IsReadableBeforeTheTimeout()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer running = engine.CreateMulti();
        await using IMultiTransfer polling = engine.CreateMulti();
        TaskCompletionSource socketTaken = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource dataArrives = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ManagedEasyTransfer inner = (ManagedEasyTransfer)engine.CreateEasy();
        WrappingEasyTransfer transfer = new(inner, async (wait, cancellationToken) =>
        {
            TransferCallbackMarshal callbacks = inner.MultiCallbacks!;
            wait.TakeSocket(null);
            socketTaken.SetResult();
            await dataArrives.Task.WaitAsync(cancellationToken);
            WriteCallback write = callbacks.Over(new EasyCallbackOptions { Write = data => data.Length }).Write!;
            await Task.Run(() => write(new byte[1]), cancellationToken);
            return CurlCode.Ok;
        });
        running.Add(transfer);
        await using IMultiTransfer idle = engine.CreateMulti();
        await using ControlledEasyTransfer notManaged = new();
        idle.Add(notManaged);
        await using IEasyTransfer notStarted = engine.CreateEasy();
        idle.Add(notStarted);
        await running.PerformAsync(CancellationToken.None);
        await socketTaken.Task.WaitAsync(LongestSocketWait);
        CurlWaitSocket[] sockets = new CurlWaitSocket[2];
        running.GetWaitSockets(sockets, out int count);

        (CurlMultiCode Code, int ReadySockets) beforeData = await polling.PollAsync(sockets, TimeSpan.Zero, CancellationToken.None);
        CurlWaitSocket[] ownSockets = [.. sockets];
        await running.PollAsync(ownSockets, TimeSpan.Zero, CancellationToken.None);
        dataArrives.SetResult();
        (CurlMultiCode Code, int ReadySockets) afterData = await polling.PollAsync(sockets, LongestSocketWait, CancellationToken.None);

        Assert.AreEqual(2, count);
        Assert.AreEqual((CurlMultiCode.Ok, 0), beforeData);
        Assert.AreEqual(CurlWaitEvents.None, ownSockets[0].ReturnedEvents);
        Assert.AreEqual((CurlMultiCode.Ok, 1), afterData);
        Assert.AreEqual(CurlWaitEvents.PollIn, sockets[0].ReturnedEvents);
        Assert.AreEqual(CurlWaitEvents.None, sockets[1].ReturnedEvents);
        await running.PerformAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task PollAsync_SocketOfAnotherHandleWhosePerformFinished_IsReadableUntilThatHandleReportsIt()
    {
        await using ManagedCurlEngine engine = new();
        await using IMultiTransfer running = engine.CreateMulti();
        await using IMultiTransfer polling = engine.CreateMulti();
        TaskCompletionSource<CurlCode> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WrappingEasyTransfer transfer = new((ManagedEasyTransfer)engine.CreateEasy(), (wait, cancellationToken) =>
        {
            wait.TakeSocket(null);
            return finish.Task.WaitAsync(cancellationToken);
        });
        running.Add(transfer);
        await running.PerformAsync(CancellationToken.None);
        CurlWaitSocket[] sockets = new CurlWaitSocket[2];
        running.GetWaitSockets(sockets, out _);

        finish.SetResult(CurlCode.Ok);
        (CurlMultiCode Code, int ReadySockets) finished = await polling.PollAsync(sockets, LongestSocketWait, CancellationToken.None);
        await running.PerformAsync(CancellationToken.None);
        (CurlMultiCode Code, int ReadySockets) reported = await polling.PollAsync(sockets, TimeSpan.Zero, CancellationToken.None);

        Assert.AreEqual((CurlMultiCode.Ok, 1), finished);
        Assert.AreEqual((CurlMultiCode.Ok, 0), reported);
    }

    [TestMethod]
    public void IsReadableOn_TheSocketOfAWaitOutsideAMultiHandle_IsFalse()
    {
        TransferSocketWait wait = new(TimeProvider.System);
        wait.TakeSocket(null);

        bool readable = wait.IsReadableOn(wait.Socket!.Value);

        Assert.IsFalse(readable);
    }
}
