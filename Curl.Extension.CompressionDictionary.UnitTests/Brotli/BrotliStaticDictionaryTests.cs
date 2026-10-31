using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliStaticDictionaryTests
{
    private const int WordsOfLengthFour = 1024;

    private static byte[] WordAndTransform(int wordId, int transformId)
    {
        var output = new BrotliOutput([]);
        BrotliStaticDictionary.WriteWord(output, 4, wordId + (transformId * WordsOfLengthFour));
        return output.ToArray();
    }

    private static int FirstFourByteWordStartingIn(byte low, byte high)
    {
        var wordId = 0;
        while (BrotliStaticDictionary.Data[wordId * 4] < low || BrotliStaticDictionary.Data[wordId * 4] > high)
        {
            wordId++;
        }

        return wordId;
    }

    // RFC 7932 section 8: word 0 of length 4 is "time"; transform 9 uppercases its first letter, 44 all of it.
    [TestMethod]
    [DataRow(0, "time")]
    [DataRow(9, "Time")]
    [DataRow(44, "TIME")]
    [DataRow(3, "ime")]
    [DataRow(12, "tim")]
    public void WriteWord_Transform_GivesAppendixBResult(int transformId, string expected) =>
        CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes(expected), WordAndTransform(0, transformId));

    // ToUpperCase: a two-byte UTF-8 character flips bit 5 of its second byte, a longer one bits 0 and 2 of its third.
    [TestMethod]
    [DataRow((byte)0xC0, (byte)0xDF, 1, 32)]
    [DataRow((byte)0xE0, (byte)0xFF, 2, 5)]
    public void WriteWord_UppercaseMultiByteCharacter_FlipsItsBits(byte low, byte high, int flippedIndex, int mask)
    {
        var wordId = FirstFourByteWordStartingIn(low, high);
        var expected = BrotliStaticDictionary.Data.Slice(wordId * 4, 4).ToArray();
        expected[flippedIndex] ^= (byte)mask;

        CollectionAssert.AreEqual(expected, WordAndTransform(wordId, 9));
    }

    [TestMethod]
    [DataRow(3, 0)]
    [DataRow(25, 0)]
    [DataRow(4, 121 * WordsOfLengthFour)]
    public void WriteWord_BadLengthOrTransform_Throws(int length, int reference) =>
        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliStaticDictionary.WriteWord(new BrotliOutput([]), length, reference));
}
