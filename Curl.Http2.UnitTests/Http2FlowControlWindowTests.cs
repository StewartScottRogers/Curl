namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2FlowControlWindow" /> refuses to be overrun or to grow past
/// 2^31 - 1 (RFC 9113 section 6.9.1), and may go negative (section 6.9.2).
/// </summary>
[TestClass]
public sealed class Http2FlowControlWindowTests
{
    [TestMethod]
    public void TryConsume_WithinTheWindow_TakesTheBytes()
    {
        var window = new Http2FlowControlWindow(10);

        Assert.IsTrue(window.TryConsume(10));
        Assert.AreEqual(0, window.Size);
    }

    [TestMethod]
    public void TryConsume_PastTheWindow_LeavesItUnchanged()
    {
        var window = new Http2FlowControlWindow(10);

        Assert.IsFalse(window.TryConsume(11));
        Assert.AreEqual(10, window.Size);
    }

    [TestMethod]
    public void TryAdjust_ToTheMaximum_GrowsTheWindow()
    {
        var window = new Http2FlowControlWindow(1);

        Assert.IsTrue(window.TryAdjust(int.MaxValue - 1));
        Assert.AreEqual(int.MaxValue, window.Available);
    }

    [TestMethod]
    public void TryAdjust_PastTheMaximum_LeavesItUnchanged()
    {
        var window = new Http2FlowControlWindow(1);

        Assert.IsFalse(window.TryAdjust(int.MaxValue));
        Assert.AreEqual(1, window.Size);
    }

    [TestMethod]
    public void TryAdjust_BelowZero_LeavesNothingAvailable()
    {
        var window = new Http2FlowControlWindow(10);

        Assert.IsTrue(window.TryAdjust(-30));
        Assert.AreEqual(-20, window.Size);
        Assert.AreEqual(0, window.Available);
        Assert.IsFalse(window.TryConsume(1));
    }
}
