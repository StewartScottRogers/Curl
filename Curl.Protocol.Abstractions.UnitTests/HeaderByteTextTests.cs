namespace Curl.Protocol.Abstractions;

[TestClass]
public sealed class HeaderByteTextTests
{
    [TestMethod]
    public void Mark_HighBytes_ShiftsEachToItsPrivateUseCharacter()
    {
        string marked = HeaderByteText.Mark("a\u0080éÿĀ");

        Assert.AreEqual("aĀ", marked);
    }

    [TestMethod]
    public void Mark_AsciiOnly_ReturnsTheSameText()
    {
        const string text = "http://x.test/";

        string marked = HeaderByteText.Mark(text);

        Assert.AreSame(text, marked);
    }

    [TestMethod]
    public void Unmark_MarkedText_GivesTheHeaderTextBack()
    {
        string unmarked = HeaderByteText.Unmark("aé");

        Assert.AreEqual("a\u0080éÿé", unmarked);
    }

    [TestMethod]
    [DataRow("a", true)]
    [DataRow("a", true)]
    [DataRow("a", true)]
    [DataRow("aé", false)]
    public void HasMarks_Text_SaysWhetherItHoldsAMarkedByte(string text, bool expected) =>
        Assert.AreEqual(expected, HeaderByteText.HasMarks(text));

    [TestMethod]
    public void Methods_Null_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => HeaderByteText.Mark(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => HeaderByteText.Unmark(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => HeaderByteText.HasMarks(null!));
    }
}
