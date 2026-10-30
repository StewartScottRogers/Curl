namespace Curl.Extension.McpClient.StreamableHttp;

[TestClass]
public sealed class McpHttpExceptionTests
{
    [TestMethod]
    public void Constructor_WithoutAStatus_CarriesTheFailureAndMessage()
    {
        var exception = new McpHttpException(McpHttpFailure.ClosedEarly, "closed");

        Assert.AreEqual(McpHttpFailure.ClosedEarly, exception.Failure);
        Assert.AreEqual("closed", exception.Message);
        Assert.IsNull(exception.StatusCode);
    }

    [TestMethod]
    public void Constructor_WithAStatus_CarriesIt() =>
        Assert.AreEqual(503, new McpHttpException(McpHttpFailure.UnexpectedStatus, "status", 503).StatusCode);
}
