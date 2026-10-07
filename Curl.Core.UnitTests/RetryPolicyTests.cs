using Curl.Testing;

namespace Curl.Core;

/// <summary>
/// Pins <see cref="RetryPolicy" />'s defaults to curl 8.21.0's when no <c>--retry</c>
/// option is given.
/// </summary>
[TestClass]
public sealed class RetryPolicyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void New_NoOptions_NeverRetriesAndBacksOff()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "(none)");

        RetryPolicy policy = new();

        diagnostics.Act("policy", policy);
        diagnostics.Assert("retries", 0, policy.Retries);
        Assert.AreEqual(0, policy.Retries);
        diagnostics.Assert("delay", TimeSpan.Zero, policy.Delay);
        Assert.AreEqual(TimeSpan.Zero, policy.Delay);
        diagnostics.Assert("max time", TimeSpan.Zero, policy.MaxTime);
        Assert.AreEqual(TimeSpan.Zero, policy.MaxTime);
        diagnostics.Assert("retry all errors", false, policy.RetryAllErrors);
        Assert.IsFalse(policy.RetryAllErrors);
        diagnostics.Assert("retry connection refused", false, policy.RetryConnectionRefused);
        Assert.IsFalse(policy.RetryConnectionRefused);
    }

    [TestMethod]
    public void With_OneOptionChanged_KeepsTheOthers()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        RetryPolicy policy = new() { Retries = 3 };
        diagnostics.Arrange("policy", policy);
        diagnostics.Arrange("changed", "Delay = 00:00:02");

        RetryPolicy copy = policy with { Delay = TimeSpan.FromSeconds(2) };

        diagnostics.Act("copy", copy);
        diagnostics.Assert("retries", 3, copy.Retries);
        Assert.AreEqual(3, copy.Retries);
        diagnostics.Assert("delay", TimeSpan.FromSeconds(2), copy.Delay);
        Assert.AreEqual(TimeSpan.FromSeconds(2), copy.Delay);
    }
}
