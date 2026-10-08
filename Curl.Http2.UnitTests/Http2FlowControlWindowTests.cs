using Curl.Testing;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="Http2FlowControlWindow" /> refuses to be overrun or to grow past
/// 2^31 - 1 (RFC 9113 section 6.9.1), and may go negative (section 6.9.2).
/// </summary>
[TestClass]
public sealed class Http2FlowControlWindowTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryConsume_WithinTheWindow_TakesTheBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(10);
        diagnostics.Arrange("window size", window.Size);

        var consumed = window.TryConsume(10);
        diagnostics.Act("TryConsume(10)", consumed);
        diagnostics.Act("window size", window.Size);

        diagnostics.Assert("consumed", true, consumed);
        diagnostics.Assert("window size", 0, window.Size);
        Assert.IsTrue(consumed);
        Assert.AreEqual(0, window.Size);
    }

    [TestMethod]
    public void TryConsume_PastTheWindow_LeavesItUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(10);
        diagnostics.Arrange("window size", window.Size);

        var consumed = window.TryConsume(11);
        diagnostics.Act("TryConsume(11)", consumed);
        diagnostics.Act("window size", window.Size);

        diagnostics.Assert("consumed", false, consumed);
        diagnostics.Assert("window size", 10, window.Size);
        Assert.IsFalse(consumed);
        Assert.AreEqual(10, window.Size);
    }

    [TestMethod]
    public void TryConsume_NegativeCount_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("window size and count", "10, -1");

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Http2FlowControlWindow(10).TryConsume(-1));
        diagnostics.Act("exception type", error.GetType().Name);

        diagnostics.Assert("exception type", nameof(ArgumentOutOfRangeException), error.GetType().Name);
    }

    [TestMethod]
    public void TryAdjust_ToTheMaximum_GrowsTheWindow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(1);
        diagnostics.Arrange("window size", window.Size);

        var adjusted = window.TryAdjust(int.MaxValue - 1);
        diagnostics.Act("TryAdjust(int.MaxValue - 1)", adjusted);
        diagnostics.Act("available", window.Available);

        diagnostics.Assert("adjusted", true, adjusted);
        diagnostics.Assert("available", int.MaxValue, window.Available);
        Assert.IsTrue(adjusted);
        Assert.AreEqual(int.MaxValue, window.Available);
    }

    [TestMethod]
    public void TryAdjust_PastTheMaximum_LeavesItUnchanged()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(1);
        diagnostics.Arrange("window size", window.Size);

        var adjusted = window.TryAdjust(int.MaxValue);
        diagnostics.Act("TryAdjust(int.MaxValue)", adjusted);
        diagnostics.Act("window size", window.Size);

        diagnostics.Assert("adjusted", false, adjusted);
        diagnostics.Assert("window size", 1, window.Size);
        Assert.IsFalse(adjusted);
        Assert.AreEqual(1, window.Size);
    }

    [TestMethod]
    public void TryAdjust_DeltaOverflowingLong_RefusesAndKeepsTheSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(1);
        diagnostics.Arrange("window size", window.Size);

        var adjusted = window.TryAdjust(long.MaxValue);
        diagnostics.Act("TryAdjust(long.MaxValue)", adjusted);
        diagnostics.Act("window size", window.Size);

        diagnostics.Assert("adjusted", false, adjusted);
        diagnostics.Assert("window size", 1, window.Size);
        Assert.IsFalse(adjusted);
        Assert.AreEqual(1, window.Size);
    }

    [TestMethod]
    public void TryAdjust_DeltaUnderflowingLong_RefusesAndKeepsTheSize()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(-1);
        diagnostics.Arrange("window size", window.Size);

        var adjusted = window.TryAdjust(long.MinValue);
        diagnostics.Act("TryAdjust(long.MinValue)", adjusted);
        diagnostics.Act("window size", window.Size);

        diagnostics.Assert("adjusted", false, adjusted);
        diagnostics.Assert("window size", -1, window.Size);
        Assert.IsFalse(adjusted);
        Assert.AreEqual(-1, window.Size);
    }

    [TestMethod]
    public void TryAdjust_BelowZero_LeavesNothingAvailable()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var window = new Http2FlowControlWindow(10);
        diagnostics.Arrange("window size", window.Size);

        var adjusted = window.TryAdjust(-30);
        diagnostics.Act("TryAdjust(-30)", adjusted);
        diagnostics.Act("window size", window.Size);
        diagnostics.Act("available", window.Available);
        var consumed = window.TryConsume(1);
        diagnostics.Act("TryConsume(1)", consumed);

        diagnostics.Assert("adjusted", true, adjusted);
        diagnostics.Assert("window size", -20, window.Size);
        diagnostics.Assert("available", 0, window.Available);
        diagnostics.Assert("consumed", false, consumed);
        Assert.IsTrue(adjusted);
        Assert.AreEqual(-20, window.Size);
        Assert.AreEqual(0, window.Available);
        Assert.IsFalse(consumed);
    }
}
