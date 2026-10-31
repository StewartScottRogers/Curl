namespace Curl.Extension.Protocol.McpClient.Stdio;

[TestClass]
public sealed class McpServerExitedExceptionTests
{
    [TestMethod]
    public void Constructors_KeepMessageAndInnerException()
    {
        var inner = new IOException("pipe closed");

        Assert.IsNotNull(new McpServerExitedException().Message);
        Assert.AreEqual("gone", new McpServerExitedException("gone").Message);
        Assert.AreSame(inner, new McpServerExitedException("gone", inner).InnerException);
    }
}
