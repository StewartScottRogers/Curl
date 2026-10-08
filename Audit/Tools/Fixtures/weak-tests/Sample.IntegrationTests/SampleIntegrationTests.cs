namespace Sample.IntegrationTests;

// Fixture for Find-WeakTests.ps1 -SelfTest (BL-1607). Nothing here is real: one weak test in an
// IntegrationTests project, so the scan is shown to read those projects as well as UnitTests ones.
[TestClass]
public sealed class SampleIntegrationTests
{
    // Weak: the only assertion is that the reply is not null.
    [TestMethod]
    [TestCategory("Integration")]
    public void Fetch_Loopback_ReturnsTheBody()
    {
        var reply = new System.Text.StringBuilder("body");
        Assert.IsNotNull(reply.ToString());
    }
}
