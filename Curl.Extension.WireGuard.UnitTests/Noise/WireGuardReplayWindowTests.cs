using Curl.Extension.WireGuard.Noise;

namespace Curl.Extension.WireGuard.UnitTests.Noise;

[TestClass]
public sealed class WireGuardReplayWindowTests
{
    private const ulong Limit = ulong.MaxValue;

    [TestMethod]
    public void IsAcceptable_NothingReceived_AcceptsZero()
    {
        var window = new WireGuardReplayWindow();

        Assert.IsTrue(window.IsAcceptable(0, Limit));
    }

    [TestMethod]
    public void IsAcceptable_MarkedCounter_IsFalseAndItsNeighboursStayAcceptable()
    {
        var window = new WireGuardReplayWindow();
        window.Mark(100);

        Assert.IsFalse(window.IsAcceptable(100, Limit));
        Assert.IsTrue(window.IsAcceptable(99, Limit));
        Assert.IsTrue(window.IsAcceptable(101, Limit));
    }

    [TestMethod]
    public void IsAcceptable_AtTheEdgeOfTheWindow_AcceptsTheLastAndRejectsTheNext()
    {
        var window = new WireGuardReplayWindow();
        window.Mark(WireGuardReplayWindow.WindowSize + 10);

        Assert.IsTrue(window.IsAcceptable(11, Limit));
        Assert.IsFalse(window.IsAcceptable(10, Limit));
    }

    [TestMethod]
    public void Mark_JumpBeyondTheRing_ForgetsEveryOlderBlock()
    {
        var window = new WireGuardReplayWindow();
        window.Mark(5);
        window.Mark(5 + (128 * 64));
        window.Mark(5 + (128 * 64) + 1);

        Assert.IsFalse(window.IsAcceptable(5 + (128 * 64), Limit));
        Assert.IsTrue(window.IsAcceptable(5 + (127 * 64) + 10, Limit));
    }

    [TestMethod]
    public void Mark_IntoANewBlock_ClearsTheReusedBlock()
    {
        var window = new WireGuardReplayWindow();
        window.Mark(64);
        window.Mark(64 + (128 * 64) - 1);

        Assert.IsFalse(window.IsAcceptable(64, Limit));
        window.Mark(64 + (128 * 64) + 5);
        Assert.IsTrue(window.IsAcceptable(64 + (128 * 64), Limit));
    }

    [TestMethod]
    public void IsAcceptable_AtTheLimit_IsFalse()
    {
        var window = new WireGuardReplayWindow();

        Assert.IsFalse(window.IsAcceptable(10, 10));
    }
}
