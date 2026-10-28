using Curl.Api.Callbacks;
using Curl.Api.EngineContracts;

namespace Curl.Engine;

[TestClass]
public sealed class CallbackReadStreamPauseByApiTests
{
    [TestMethod]
    public async Task ReadAsync_SendPausedByTheApplication_CallsNoCallbackUntilUnpaused()
    {
        EasyPauseGate gate = new();
        gate.Set(CurlPause.Send);
        int calls = 0;
        ReadCallback callback = buffer =>
        {
            calls++;
            buffer[0] = 9;
            return 1;
        };
        await using CallbackReadStream stream = new(callback, gate);
        byte[] buffer = new byte[4];

        Task<int> reading = stream.ReadAsync(buffer.AsMemory()).AsTask();
        await Task.Yield();
        int callsWhilePaused = calls;
        bool completedWhilePaused = reading.IsCompleted;
        gate.Set(CurlPause.Continue);
        int read = await reading;

        Assert.AreEqual(0, callsWhilePaused);
        Assert.IsFalse(completedWhilePaused);
        Assert.AreEqual(1, read);
        Assert.AreEqual(9, buffer[0]);
        Assert.AreEqual(1, calls);
    }

}
