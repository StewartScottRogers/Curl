using System.Text;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Pins <see cref="HpackHuffman" /> to RFC 7541 appendix B's code table and section 5.2's
/// padding rules.
/// </summary>
[TestClass]
public sealed class HpackHuffmanTests
{
    [TestMethod]
    [DataRow(0, 0x1ff8u, 13)]
    [DataRow(1, 0x7fffd8u, 23)]
    [DataRow(10, 0x3ffffffcu, 30)]
    [DataRow(' ', 0x14u, 6)]
    [DataRow('0', 0x0u, 5)]
    [DataRow('a', 0x3u, 5)]
    [DataRow('\\', 0x7fff0u, 19)]
    [DataRow('~', 0x1ffdu, 13)]
    [DataRow(127, 0xffffffcu, 28)]
    [DataRow(128, 0xfffe6u, 20)]
    [DataRow(199, 0x1ffffecu, 25)]
    [DataRow(249, 0xffffffeu, 28)]
    [DataRow(255, 0x3ffffeeu, 26)]
    [DataRow(256, 0x3fffffffu, 30)]
    public void GetCode_Symbol_IsTheCodeInRfc7541AppendixB(int symbol, uint code, int length)
    {
        Assert.AreEqual((code, length), HpackHuffman.GetCode(symbol));
    }

    [TestMethod]
    public void GetCode_EverySymbol_FormsACompletePrefixCode()
    {
        double kraftSum = 0;
        for (var symbol = 0; symbol <= 256; symbol++)
        {
            kraftSum += Math.Pow(2, -HpackHuffman.GetCode(symbol).Length);
        }

        Assert.AreEqual(1.0, kraftSum, 1e-12);
    }

    // RFC 7541 appendix C.4.1.
    [TestMethod]
    public void Encode_WwwExampleCom_IsAppendixC41sString()
    {
        CollectionAssert.AreEqual(
            FromHex("f1e3 c2e5 f23a 6ba0 ab90 f4ff"),
            HpackHuffman.Encode("www.example.com"u8));
    }

    [TestMethod]
    public void GetEncodedLength_WwwExampleCom_IsTwelve()
    {
        Assert.AreEqual(12, HpackHuffman.GetEncodedLength("www.example.com"u8));
    }

    [TestMethod]
    public void Encode_WholeBytesOfCode_AddsNoPadding()
    {
        CollectionAssert.AreEqual(new byte[5], HpackHuffman.Encode("00000000"u8));
    }

    [TestMethod]
    public void EncodeThenDecode_EveryByteValue_RoundTrips()
    {
        var everyByte = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();

        CollectionAssert.AreEqual(everyByte, HpackHuffman.Decode(HpackHuffman.Encode(everyByte)));
    }

    [TestMethod]
    public void EncodeAndDecode_Empty_AreEmpty()
    {
        Assert.IsEmpty(HpackHuffman.Encode([]));
        Assert.IsEmpty(HpackHuffman.Decode([]));
    }

    // RFC 7541 appendix C.4.3.
    [TestMethod]
    public void Decode_AppendixC43sCustomValue_IsCustomValue()
    {
        Assert.AreEqual("custom-value", Encoding.Latin1.GetString(HpackHuffman.Decode(FromHex("25a8 49e9 5bb8 e8b4 bf"))));
    }

    [TestMethod]
    public void Decode_PaddingOfZeros_FailsAsInvalidPadding()
    {
        // '0' is 00000; the remaining three bits must be ones.
        Assert.AreEqual(HpackDecodingError.InvalidHuffmanPadding, ErrorOf(() => HpackHuffman.Decode([0x00])));
    }

    [TestMethod]
    public void Decode_EightBitsOfPadding_FailsAsInvalidPadding()
    {
        // 'a' is 00011; three bits of padding end the first byte and a whole byte follows.
        Assert.AreEqual(HpackDecodingError.InvalidHuffmanPadding, ErrorOf(() => HpackHuffman.Decode([0x1F, 0xFF])));
    }

    [TestMethod]
    public void Decode_EndOfStringCode_FailsAsEndOfStringInData()
    {
        Assert.AreEqual(
            HpackDecodingError.HuffmanEndOfStringInData,
            ErrorOf(() => HpackHuffman.Decode([0xFF, 0xFF, 0xFF, 0xFF])));
    }
}
