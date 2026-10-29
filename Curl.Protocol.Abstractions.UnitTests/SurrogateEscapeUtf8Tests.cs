using System.Text;

namespace Curl.Protocol.Abstractions;

[TestClass]
public sealed class SurrogateEscapeUtf8Tests
{
    [TestMethod]
    public void Decode_InvalidBytes_BecomeLoneSurrogates()
    {
        Assert.AreEqual("a\uDCFFb\u00e9", SurrogateEscapeUtf8.Decode([0x61, 0xFF, 0x62, 0xC3, 0xA9]));
    }

    [TestMethod]
    public void Encode_EscapesAndOtherSurrogates_GiveBytesAndReplacement()
    {
        CollectionAssert.AreEqual(new byte[] { 0x61, 0xFF, 0xEF, 0xBF, 0xBD, 0xC3, 0xA9 }, SurrogateEscapeUtf8.Encode("a\uDCFF\uD800\u00e9"));
    }

    [TestMethod]
    public void HoldsEscape_WithAndWithoutEscape_Tells()
    {
        Assert.IsTrue(SurrogateEscapeUtf8.HoldsEscape("a\uDC80"));
        Assert.IsFalse(SurrogateEscapeUtf8.HoldsEscape("abc"));
    }

    [TestMethod]
    public void Encoding_RoundTripsThroughEveryMember()
    {
        Encoding encoding = SurrogateEscapeUtf8.Encoding;
        byte[] raw = [0x61, 0xFF, 0xC3, 0xA9, 0x80];
        Assert.AreEqual(65001, encoding.CodePage);
        string text = encoding.GetString(raw);
        Assert.AreEqual("a\uDCFF\u00e9\uDC80", text);
        Assert.AreEqual(text.Length, encoding.GetCharCount(raw, 0, raw.Length));
        char[] chars = new char[encoding.GetMaxCharCount(raw.Length)];
        Assert.AreEqual(text.Length, encoding.GetChars(raw, 0, raw.Length, chars, 0));
        Assert.AreEqual(raw.Length, encoding.GetByteCount(text.ToCharArray(), 0, text.Length));
        byte[] back = new byte[encoding.GetMaxByteCount(text.Length)];
        Assert.AreEqual(raw.Length, encoding.GetBytes(text.ToCharArray(), 0, text.Length, back, 0));
        CollectionAssert.AreEqual(raw, back[..raw.Length]);
    }
}
