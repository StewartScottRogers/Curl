namespace Curl.Conformance.UnitTests;

[TestClass]
public sealed class UpstreamUtf8WordsTests
{
    [TestMethod]
    public void AsSystemText_ValidUtf8Bytes_ReadsThemAsUtf8()
    {
        Assert.AreEqual("漢字-ä.txt", UpstreamUtf8Words.AsSystemText("æ¼¢å­\u0097-Ã¤.txt"));
    }

    [TestMethod]
    public void AsSystemText_InvalidUtf8Bytes_ReturnsTheWordUnchanged()
    {
        Assert.AreEqual("café.txt", UpstreamUtf8Words.AsSystemText("café.txt"));
    }

    [TestMethod]
    public void AsSystemText_Ascii_ReturnsTheWordUnchanged()
    {
        Assert.AreEqual("log/test1703.txt", UpstreamUtf8Words.AsSystemText("log/test1703.txt"));
    }
}
