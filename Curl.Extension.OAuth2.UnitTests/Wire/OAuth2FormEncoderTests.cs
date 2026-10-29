namespace Curl.Extension.OAuth2.Wire;

/// <summary>Pins the <c>application/x-www-form-urlencoded</c> encoding of RFC 6749 Appendix B.</summary>
[TestClass]
public sealed class OAuth2FormEncoderTests
{
    [TestMethod]
    public void EncodeComponent_AppendixBExample_EncodesAsAppendixB()
    {
        // Appendix B: " %&+£€" is encoded as "+%25%26%2B%C2%A3%E2%82%AC".
        Assert.AreEqual("+%25%26%2B%C2%A3%E2%82%AC", OAuth2FormEncoder.EncodeComponent(" %&+£€"));
    }

    [TestMethod]
    public void EncodeComponent_UnreservedCharacters_LeavesThemAsTheyAre()
    {
        Assert.AreEqual("AZaz09-._*", OAuth2FormEncoder.EncodeComponent("AZaz09-._*"));
    }

    [TestMethod]
    public void EncodeFields_TwoFields_JoinsNameValuePairsInOrder()
    {
        Assert.AreEqual("b=1+2&a=%3D", OAuth2FormEncoder.EncodeFields([new("b", "1 2"), new("a", "=")]));
    }
}
