namespace Curl.Extended.Protocol.McpClient.Wire;

/// <summary>Pins the standard exception constructors the reader's callers may use.</summary>
[TestClass]
public sealed class McpWireFormatExceptionTests
{
    [TestMethod]
    public void Constructors_StandardShapes_KeepMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var withNothing = new McpWireFormatException();
        var withMessage = new McpWireFormatException("bad message");
        var withInner = new McpWireFormatException("bad message", inner);

        Assert.IsNull(withNothing.InnerException);
        Assert.AreEqual("bad message", withMessage.Message);
        Assert.AreSame(inner, withInner.InnerException);
    }
}
