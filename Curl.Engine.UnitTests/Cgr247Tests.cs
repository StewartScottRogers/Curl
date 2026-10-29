namespace Curl.Engine.UnitTests;

/// <summary>
/// CGR-247: a state with caches of its own (a new library easy or multi handle's) numbers <c>%{xfer_id}</c> and
/// <c>%{conn_id}</c> from 0, whatever the engine's other handles have taken, as libcurl 8.22.0's per-perform multi does.
/// </summary>
[TestClass]
public sealed class Cgr247Tests
{
    [TestMethod]
    public void WithOwnCaches_AfterTheEngineTookIds_NumbersFromZeroAndLeavesTheEnginesCounts()
    {
        ManagedEngineSharedState engine = new(TimeProvider.System, _ => string.Empty);
        engine.Performer.TakeTransferId();
        engine.Performer.TakeConnectionId();

        ManagedEngineSharedState handle = engine.WithOwnCaches();

        Assert.AreEqual(0, handle.Performer.TakeTransferId());
        Assert.AreEqual(0, handle.Performer.TakeConnectionId());
        Assert.AreEqual(1, engine.Performer.NextTransferId);
    }
}
