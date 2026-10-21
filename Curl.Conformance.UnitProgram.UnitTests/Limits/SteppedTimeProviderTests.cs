using Curl.Conformance.UnitProgram.Limits;

namespace Curl.Conformance.UnitProgram.UnitTests.Limits;

[TestClass]
public sealed class SteppedTimeProviderTests
{
    [TestMethod]
    public void GetTimestamp_TwoSteps_CountsTicksFromZero()
    {
        SteppedTimeProvider clock = new();

        clock.Step(TimeSpan.FromSeconds(1));
        clock.Step(TimeSpan.FromMilliseconds(500));

        Assert.AreEqual(TimeSpan.FromSeconds(1.5).Ticks, clock.GetTimestamp());
        Assert.AreEqual(TimeSpan.FromSeconds(1.5), clock.GetElapsedTime(0));
        Assert.AreEqual(TimeSpan.TicksPerSecond, clock.TimestampFrequency);
    }

    [TestMethod]
    public void Step_LiveTimer_FiresOncePerStepWithItsState()
    {
        SteppedTimeProvider clock = new();
        List<object?> fired = [];
        using ITimer timer = clock.CreateTimer(fired.Add, "state", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        clock.Step(TimeSpan.FromSeconds(1));
        clock.Step(TimeSpan.FromSeconds(1));

        CollectionAssert.AreEqual(new object?[] { "state", "state" }, fired);
        Assert.IsTrue(timer.Change(TimeSpan.Zero, TimeSpan.Zero));
    }

    [TestMethod]
    public async Task Step_DisposedTimer_DoesNotFire()
    {
        SteppedTimeProvider clock = new();
        int fired = 0;
        ITimer disposed = clock.CreateTimer(_ => fired++, null, TimeSpan.Zero, TimeSpan.Zero);
        ITimer disposedAsync = clock.CreateTimer(_ => fired++, null, TimeSpan.Zero, TimeSpan.Zero);

        disposed.Dispose();
        await disposedAsync.DisposeAsync();
        clock.Step(TimeSpan.FromSeconds(1));

        Assert.AreEqual(0, fired);
    }

    [TestMethod]
    public void CreateTimer_NullCallback_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SteppedTimeProvider().CreateTimer(null!, null, TimeSpan.Zero, TimeSpan.Zero));
    }
}
