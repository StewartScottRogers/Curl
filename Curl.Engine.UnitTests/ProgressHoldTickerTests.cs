namespace Curl.Engine;

[TestClass]
public sealed class ProgressHoldTickerTests
{
    [TestMethod]
    public async Task Start_PausedAtSomeTicks_TicksOnlyWhileNotPaused()
    {
        TimerManualTimeProvider clock = new();
        int ticks = 0;
        bool paused = true;
        await using ProgressHoldTicker ticker = ProgressHoldTicker.Start(() => Interlocked.Increment(ref ticks), () => paused, clock);

        for (int i = 0; i < 3; i++)
        {
            clock.Advance(1000);
            await Task.Delay(5);
        }

        Assert.AreEqual(0, Volatile.Read(ref ticks));
        paused = false;
        for (int i = 0; i < 100 && Volatile.Read(ref ticks) == 0; i++)
        {
            clock.Advance(1000);
            await Task.Delay(5);
        }

        Assert.IsGreaterThan(0, Volatile.Read(ref ticks));
    }
}
