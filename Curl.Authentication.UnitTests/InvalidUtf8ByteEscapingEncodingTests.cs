using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Pins <see cref="InvalidUtf8ByteEscapingEncoding" />: UTF-8 that keeps each byte outside a valid
/// sequence as a lone surrogate U+DC80 to U+DCFF and encodes it back (CGR-053, ADR-0822).
/// </summary>
[TestClass]
public sealed class InvalidUtf8ByteEscapingEncodingTests
{
    private static readonly Encoding Encoding = InvalidUtf8ByteEscapingEncoding.Instance;

    [TestMethod]
    public void GetString_InvalidByte_IsItsSurrogateEscape()
    {
        byte[] bytes = [0x61, 0xFF, 0xC3, 0xA9];

        string text = Encoding.GetString(bytes);

        Assert.AreEqual("a\uDCFFé", text);
    }

    [TestMethod]
    public void GetBytes_SurrogateEscapesAndValidText_GiveBackTheBytes()
    {
        byte[] bytes = [0x61, 0xFF, 0x80, 0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80];

        byte[] encoded = Encoding.GetBytes(Encoding.GetString(bytes));

        CollectionAssert.AreEqual(bytes, encoded);
    }

    [TestMethod]
    public void GetBytes_LoneSurrogateOutsideTheEscapes_IsReplacementCharacter()
    {
        byte[] encoded = Encoding.GetBytes("\uD800");

        CollectionAssert.AreEqual(new byte[] { 0xEF, 0xBF, 0xBD }, encoded);
    }

    [TestMethod]
    public void GetByteCountAndGetCharCount_CountTheEncodedAndDecodedLengths()
    {
        int byteCount = Encoding.GetByteCount("a\uDCFFé");
        int charCount = Encoding.GetCharCount([0x61, 0xFF, 0xC3, 0xA9]);

        Assert.AreEqual(4, byteCount);
        Assert.AreEqual(3, charCount);
    }

    [TestMethod]
    public void GetMaxCounts_CoverTheWorstCase()
    {
        int maxBytes = Encoding.GetMaxByteCount(2);
        int maxChars = Encoding.GetMaxCharCount(2);

        Assert.AreEqual(9, maxBytes);
        Assert.AreEqual(3, maxChars);
    }

    [TestMethod]
    public void CodePage_IsUtf8s()
    {
        int codePage = Encoding.CodePage;

        Assert.AreEqual(65001, codePage);
    }
}
