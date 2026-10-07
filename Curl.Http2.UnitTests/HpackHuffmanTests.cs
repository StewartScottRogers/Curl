using System.Text;
using Curl.Testing;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Pins <see cref="HpackHuffman" /> to RFC 7541 appendix B's code table and section 5.2's
/// padding rules.
/// </summary>
[TestClass]
public sealed class HpackHuffmanTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("symbol", symbol);
        diagnostics.Arrange("expected code and length", $"0x{code:x}/{length}");

        var actual = HpackHuffman.GetCode(symbol);
        diagnostics.Act("GetCode", $"0x{actual.Code:x}/{actual.Length}");

        diagnostics.Assert("code and length", (code, length), actual);
        Assert.AreEqual((code, length), actual);
    }

    [TestMethod]
    public void GetCode_EverySymbol_FormsACompletePrefixCode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("symbols", "0 to 256 inclusive");
        double kraftSum = 0;
        for (var symbol = 0; symbol <= 256; symbol++)
        {
            kraftSum += Math.Pow(2, -HpackHuffman.GetCode(symbol).Length);
        }

        diagnostics.Act("Kraft sum", kraftSum);

        diagnostics.Assert("Kraft sum", 1.0, kraftSum);
        Assert.AreEqual(1.0, kraftSum, 1e-12);
    }

    // RFC 7541 appendix C.4.1.
    [TestMethod]
    public void Encode_WwwExampleCom_IsAppendixC41sString()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var expected = FromHex("f1e3 c2e5 f23a 6ba0 ab90 f4ff");
        diagnostics.Arrange("input", "www.example.com");

        var actual = HpackHuffman.Encode("www.example.com"u8);
        diagnostics.Act("encoded length", actual.Length);
        diagnostics.Bytes("encoded", actual);

        diagnostics.Diff("encoded", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void GetEncodedLength_WwwExampleCom_IsTwelve()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input", "www.example.com");

        var length = HpackHuffman.GetEncodedLength("www.example.com"u8);
        diagnostics.Act("encoded length", length);

        diagnostics.Assert("encoded length", 12, length);
        Assert.AreEqual(12, length);
    }

    [TestMethod]
    public void Encode_WholeBytesOfCode_AddsNoPadding()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input", "00000000");

        var actual = HpackHuffman.Encode("00000000"u8);
        diagnostics.Act("encoded length", actual.Length);
        diagnostics.Bytes("encoded", actual);

        diagnostics.Diff("encoded", new byte[5], actual);
        CollectionAssert.AreEqual(new byte[5], actual);
    }

    [TestMethod]
    public void EncodeThenDecode_EveryByteValue_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var everyByte = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        diagnostics.Arrange("input byte count", everyByte.Length);

        var encoded = HpackHuffman.Encode(everyByte);
        var decoded = HpackHuffman.Decode(encoded);
        diagnostics.Act("encoded length", encoded.Length);
        diagnostics.Bytes("decoded", decoded);

        diagnostics.Diff("decoded", everyByte, decoded);
        CollectionAssert.AreEqual(everyByte, decoded);
    }

    [TestMethod]
    public void EncodeAndDecode_Empty_AreEmpty()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("input", "empty");

        var encoded = HpackHuffman.Encode([]);
        var decoded = HpackHuffman.Decode([]);
        diagnostics.Act("encoded length", encoded.Length);
        diagnostics.Act("decoded length", decoded.Length);

        diagnostics.Assert("encoded length", 0, encoded.Length);
        diagnostics.Assert("decoded length", 0, decoded.Length);
        Assert.IsEmpty(encoded);
        Assert.IsEmpty(decoded);
    }

    // RFC 7541 appendix C.4.3.
    [TestMethod]
    public void Decode_AppendixC43sCustomValue_IsCustomValue()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var encoded = FromHex("25a8 49e9 5bb8 e8b4 bf");
        diagnostics.Bytes("encoded", encoded);
        diagnostics.Arrange("encoded length", encoded.Length);

        var text = Encoding.Latin1.GetString(HpackHuffman.Decode(encoded));
        diagnostics.Act("decoded", text);

        diagnostics.Assert("decoded", "custom-value", text);
        Assert.AreEqual("custom-value", text);
    }

    [TestMethod]
    public void Decode_PaddingOfZeros_FailsAsInvalidPadding()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded", "0x00");

        // '0' is 00000; the remaining three bits must be ones.
        var error = ErrorOf(() => HpackHuffman.Decode([0x00]));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.InvalidHuffmanPadding, error);
        Assert.AreEqual(HpackDecodingError.InvalidHuffmanPadding, error);
    }

    [TestMethod]
    public void Decode_EightBitsOfPadding_FailsAsInvalidPadding()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded", "0x1f 0xff");

        // 'a' is 00011; three bits of padding end the first byte and a whole byte follows.
        var error = ErrorOf(() => HpackHuffman.Decode([0x1F, 0xFF]));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.InvalidHuffmanPadding, error);
        Assert.AreEqual(HpackDecodingError.InvalidHuffmanPadding, error);
    }

    [TestMethod]
    public void Decode_EndOfStringCode_FailsAsEndOfStringInData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded", "0xff 0xff 0xff 0xff");

        var error = ErrorOf(() => HpackHuffman.Decode([0xFF, 0xFF, 0xFF, 0xFF]));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.HuffmanEndOfStringInData, error);
        Assert.AreEqual(HpackDecodingError.HuffmanEndOfStringInData, error);
    }
}
