namespace Curl.Protocol.Abstractions;

/// <summary>
/// <see cref="ConnectTarget.FailsOnHttpErrorStatus" /> is off unless a handler sets it for
/// <c>-f</c> (BL-6049, ADR-1467).
/// </summary>
[TestClass]
public sealed class ConnectTargetFailsOnHttpErrorStatusTests
{
    [TestMethod]
    public void FailsOnHttpErrorStatus_NotSet_IsFalse()
    {
        var target = new ConnectTarget("example.com", 443, UseTls: true);

        Assert.IsFalse(target.FailsOnHttpErrorStatus);
    }

    [TestMethod]
    public void FailsOnHttpErrorStatus_SetWithAWithExpression_IsTrue()
    {
        var target = new ConnectTarget("example.com", 443, UseTls: true) with { FailsOnHttpErrorStatus = true };

        Assert.IsTrue(target.FailsOnHttpErrorStatus);
    }
}
