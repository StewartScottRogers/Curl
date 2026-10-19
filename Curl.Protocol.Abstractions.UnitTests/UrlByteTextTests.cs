namespace Curl.Protocol.Abstractions;

[TestClass]
public sealed class UrlByteTextTests
{
    [TestMethod]
    public void ToText_ValidUtf8_IsItsCharacters() =>
        Assert.AreEqual("räk", UrlByteText.ToText([0x72, 0xC3, 0xA4, 0x6B]));

    [TestMethod]
    public void ToText_ByteThatIsNotUtf8_IsDc00PlusTheByte() =>
        Assert.AreEqual("_\uDCC0_ä\uDCFF", UrlByteText.ToText([0x5F, 0xC0, 0x5F, 0xC3, 0xA4, 0xFF]));

    [TestMethod]
    public void ToBytes_EscapedByte_IsTheByte() =>
        CollectionAssert.AreEqual(new byte[] { 0x5F, 0xC0, 0xC3, 0xA4, 0xFF }, UrlByteText.ToBytes("_\uDCC0ä\uDCFF"));

    [TestMethod]
    public void ToBytes_SurrogatePairAndOtherLoneSurrogate_AreUtf8AndReplacement() =>
        CollectionAssert.AreEqual(
            new byte[] { 0xF0, 0x9F, 0x98, 0x80, 0xEF, 0xBF, 0xBD },
            UrlByteText.ToBytes("😀\uD800"));

    [TestMethod]
    public void ToBytes_OfToText_GivesTheBytesBack()
    {
        byte[] bytes = [0x41, 0x80, 0xE2, 0x82, 0xAC, 0xF0, 0x9F, 0x98];

        CollectionAssert.AreEqual(bytes, UrlByteText.ToBytes(UrlByteText.ToText(bytes)));
    }
}
