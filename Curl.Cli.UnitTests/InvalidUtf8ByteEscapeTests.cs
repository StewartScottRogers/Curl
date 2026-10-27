namespace Curl.Cli;

/// <summary>
/// Pins <see cref="InvalidUtf8ByteEscape"/> (CGR-016, BL-3849): each byte outside a valid UTF-8
/// sequence becomes U+DC00 + the byte, and the encoding gives it back unchanged.
/// </summary>
[TestClass]
public sealed class InvalidUtf8ByteEscapeTests
{
    [TestMethod]
    public void Decode_ValidUtf8_ReadsItAsUtf8()
    {
        Assert.AreEqual("aé€😀", InvalidUtf8ByteEscape.Decode("aé€😀"u8));
    }

    [TestMethod]
    public void Decode_InvalidBytes_EscapesEachByte()
    {
        Assert.AreEqual("a\uDCFF\uDCE2\uDC82b", InvalidUtf8ByteEscape.Decode([0x61, 0xFF, 0xE2, 0x82, 0x62]));
    }

    [TestMethod]
    public void Decode_Empty_IsEmpty()
    {
        Assert.AreEqual(string.Empty, InvalidUtf8ByteEscape.Decode([]));
    }

    [TestMethod]
    public void EncodingGetBytes_EscapedText_GivesBackTheOriginalBytes()
    {
        byte[] original = [0x61, 0xFF, 0xC3, 0xA9, 0xE2, 0x82, 0xF0, 0x9F, 0x98, 0x80, 0x80];

        CollectionAssert.AreEqual(original, InvalidUtf8ByteEscape.Encoding.GetBytes(InvalidUtf8ByteEscape.Decode(original)));
    }

    [TestMethod]
    public void EncodingGetBytes_LoneSurrogateOutsideTheEscapes_EncodesTheReplacementCharacter()
    {
        CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBF, 0xBD, 0x78, 0xEF, 0xBF, 0xBD }, InvalidUtf8ByteEscape.Encoding.GetBytes("\uD800x\uDC7F"));
    }

    [TestMethod]
    public void EncodingGetBytes_Empty_IsEmpty()
    {
        Assert.IsEmpty(InvalidUtf8ByteEscape.Encoding.GetBytes(string.Empty));
    }

    [TestMethod]
    public void EncodingGetString_InvalidBytes_DecodesAsDecodeDoes()
    {
        Assert.AreEqual("x\uDCFF", InvalidUtf8ByteEscape.Encoding.GetString([0x78, 0xFF]));
    }

    [TestMethod]
    public void EncodingMaxCounts_CoverTheWorstCase()
    {
        Assert.AreEqual(9, InvalidUtf8ByteEscape.Encoding.GetMaxByteCount(2));
        Assert.AreEqual(3, InvalidUtf8ByteEscape.Encoding.GetMaxCharCount(2));
    }
}
