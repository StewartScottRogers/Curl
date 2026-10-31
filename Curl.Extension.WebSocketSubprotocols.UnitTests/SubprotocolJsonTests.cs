namespace Curl.Extension.WebSocketSubprotocols;

[TestClass]
public sealed class SubprotocolJsonTests
{
    [TestMethod]
    public void String_EscapesWhatJsonRequires_Rfc8259Section7()
    {
        Assert.AreEqual("\"a\\\"b\\\\c\\n<é>\"", SubprotocolJson.String("a\"b\\c\n<é>"));
    }
}
