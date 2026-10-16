using Curl.Api.EngineContracts;

namespace Curl.Engine;

[TestClass]
public sealed class EasyPauseGateTests
{
    [TestMethod]
    public async Task WhenResumedAsync_DirectionNotPaused_CompletesAtOnce()
    {
        EasyPauseGate gate = new();
        gate.Pause(CurlPause.Send);

        await gate.WhenResumedAsync(CurlPause.Receive, CancellationToken.None);

        Assert.AreEqual(CurlPause.Send, gate.Directions);
    }

    [TestMethod]
    public async Task WhenResumedAsync_DirectionPaused_WaitsUntilSetClearsIt()
    {
        EasyPauseGate gate = new();
        gate.Pause(CurlPause.Receive);
        gate.Pause(CurlPause.Send);

        Task waiting = gate.WhenResumedAsync(CurlPause.Receive, CancellationToken.None);
        gate.Set(CurlPause.All);
        bool stillWaitingAfterAll = !waiting.IsCompleted;
        gate.Set(CurlPause.Send);
        await waiting;

        Assert.IsTrue(stillWaitingAfterAll);
        Assert.AreEqual(CurlPause.Send, gate.Directions);
    }

    [TestMethod]
    public async Task WhenResumedAsync_TokenCancelled_ThrowsOperationCanceled()
    {
        EasyPauseGate gate = new();
        gate.Pause(CurlPause.Receive);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await gate.WhenResumedAsync(CurlPause.Receive, new CancellationToken(canceled: true)));
    }
}
