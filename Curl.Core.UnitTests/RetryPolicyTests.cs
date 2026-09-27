namespace Curl.Core;

/// <summary>
/// Pins <see cref="RetryPolicy" />'s defaults to curl 8.21.0's when no <c>--retry</c>
/// option is given.
/// </summary>
[TestClass]
public sealed class RetryPolicyTests
{
    [TestMethod]
    public void New_NoOptions_NeverRetriesAndBacksOff()
    {
        RetryPolicy policy = new();

        Assert.AreEqual(0, policy.Retries);
        Assert.AreEqual(TimeSpan.Zero, policy.Delay);
        Assert.AreEqual(TimeSpan.Zero, policy.MaxTime);
        Assert.IsFalse(policy.RetryAllErrors);
        Assert.IsFalse(policy.RetryConnectionRefused);
    }

    [TestMethod]
    public void With_OneOptionChanged_KeepsTheOthers()
    {
        RetryPolicy policy = new() { Retries = 3 };

        RetryPolicy copy = policy with { Delay = TimeSpan.FromSeconds(2) };

        Assert.AreEqual(3, copy.Retries);
        Assert.AreEqual(TimeSpan.FromSeconds(2), copy.Delay);
    }
}
