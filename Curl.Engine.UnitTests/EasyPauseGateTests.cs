using Curl.Api.EngineContracts;

namespace Curl.Engine;

[TestClass]
public sealed class EasyPauseGateTests
{
    [TestMethod]
    public async Task WhenResumedAsync_DirectionNotPaused_CompletesAtOnce()
    {
        EasyPauseGate gate = new();
        gate.Pause(CurlPause.Send, gate.Unpauses);

        await gate.WhenResumedAsync(CurlPause.Receive, CancellationToken.None);

        Assert.AreEqual(CurlPause.Send, gate.Directions);
    }

    [TestMethod]
    public async Task WhenResumedAsync_DirectionPaused_WaitsUntilSetClearsIt()
    {
        EasyPauseGate gate = new();
        gate.Pause(CurlPause.Receive, gate.Unpauses);
        gate.Pause(CurlPause.Send, gate.Unpauses);

        Task waiting = gate.WhenResumedAsync(CurlPause.Receive, CancellationToken.None);
        gate.Set(CurlPause.All);
        bool stillWaitingAfterAll = !waiting.IsCompleted;
        gate.Set(CurlPause.Send);
        await waiting;

        Assert.IsTrue(stillWaitingAfterAll);
        Assert.AreEqual(CurlPause.Send, gate.Directions);
    }

    [TestMethod]
    public async Task Pause_SetRanSinceTheCountWasRead_IgnoresThePause()
    {
        EasyPauseGate gate = new();
        int before = gate.Unpauses;
        gate.Set(CurlPause.Continue);

        gate.Pause(CurlPause.Receive, before);
        await gate.WhenResumedAsync(CurlPause.Receive, CancellationToken.None);

        Assert.AreEqual(CurlPause.Continue, gate.Directions);
        Assert.AreEqual(before + 1, gate.Unpauses);
    }

    [TestMethod]
    public async Task WhenResumedAsync_TokenCancelled_ThrowsOperationCanceled()
    {
        EasyPauseGate gate = new();
        gate.Pause(CurlPause.Receive, gate.Unpauses);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await gate.WhenResumedAsync(CurlPause.Receive, new CancellationToken(canceled: true)));
    }
}
