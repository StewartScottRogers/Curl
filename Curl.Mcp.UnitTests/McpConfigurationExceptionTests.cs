namespace Curl.Mcp;

[TestClass]
public sealed class McpConfigurationExceptionTests
{
    [TestMethod]
    public void Constructors_KeepTheMessageAndTheCause()
    {
        InvalidOperationException cause = new("cause");

        Assert.IsNull(new McpConfigurationException().InnerException);
        Assert.AreEqual("bad", new McpConfigurationException("bad").Message);
        McpConfigurationException withCause = new("bad", cause);
        Assert.AreEqual("bad", withCause.Message);
        Assert.AreSame(cause, withCause.InnerException);
    }
}
