using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Pins HPACK's integer and string literal primitives to RFC 7541 sections 5.1 and 5.2 and
/// appendix C.1.
/// </summary>
[TestClass]
public sealed class HpackPrimitivesTests
{
    // RFC 7541 appendix C.1.1, C.1.2 and C.1.3.
    [TestMethod]
    [DataRow(10, 5, "0a")]
    [DataRow(1337, 5, "1f9a0a")]
    [DataRow(42, 8, "2a")]
    public void WriteInteger_AppendixC1Examples_GiveThePublishedBytes(int value, int prefixBits, string hex)
    {
        var output = new List<byte>();

        HpackPrimitives.WriteInteger(output, value, prefixBits, 0x00);

        CollectionAssert.AreEqual(FromHex(hex), output.ToArray());
    }

    [TestMethod]
    [DataRow(10, 5, "0a")]
    [DataRow(1337, 5, "1f9a0a")]
    [DataRow(42, 8, "2a")]
    [DataRow(int.MaxValue, 5, "1fe0ffffff07")]
    public void ReadInteger_AppendixC1Examples_GiveTheValueAndConsumeEveryByte(int value, int prefixBits, string hex)
    {
        var bytes = FromHex(hex);
        var position = 0;

        Assert.AreEqual(value, HpackPrimitives.ReadInteger(bytes, ref position, prefixBits));
        Assert.AreEqual(bytes.Length, position);
    }

    [TestMethod]
    public void ReadInteger_ValueBeyond31Bits_FailsAsIntegerOverflow()
    {
        var position = 0;

        Assert.AreEqual(
            HpackDecodingError.IntegerOverflow,
            ErrorOf(() => HpackPrimitives.ReadInteger(FromHex("1fe1ffffff07"), ref position, 5)));
    }

    [TestMethod]
    public void ReadInteger_TooManyContinuationBytes_FailsAsIntegerOverflow()
    {
        var position = 0;

        Assert.AreEqual(
            HpackDecodingError.IntegerOverflow,
            ErrorOf(() => HpackPrimitives.ReadInteger(FromHex("1f8080808080 00"), ref position, 5)));
    }

    [TestMethod]
    public void ReadInteger_MissingContinuationByte_FailsAsTruncatedBlock()
    {
        var position = 0;

        Assert.AreEqual(
            HpackDecodingError.TruncatedBlock,
            ErrorOf(() => HpackPrimitives.ReadInteger(FromHex("1f9a"), ref position, 5)));
    }

    [TestMethod]
    public void WriteString_HuffmanIsShorter_WritesHuffman()
    {
        var output = new List<byte>();

        HpackPrimitives.WriteString(output, "www.example.com");

        CollectionAssert.AreEqual(FromHex("8c f1e3 c2e5 f23a 6ba0 ab90 f4ff"), output.ToArray());
    }

    [TestMethod]
    public void WriteString_HuffmanIsNoShorter_WritesRawBytes()
    {
        var output = new List<byte>();

        HpackPrimitives.WriteString(output, "*/*");

        CollectionAssert.AreEqual(FromHex("03 2a2f2a"), output.ToArray());
    }

    [TestMethod]
    public void ReadString_LengthBeyondBlock_FailsAsTruncatedBlock()
    {
        var position = 0;

        Assert.AreEqual(
            HpackDecodingError.TruncatedBlock,
            ErrorOf(() => HpackPrimitives.ReadString(FromHex("05 6162"), ref position)));
    }

    [TestMethod]
    public void ReadString_AtEndOfBlock_FailsAsTruncatedBlock()
    {
        var position = 0;

        Assert.AreEqual(HpackDecodingError.TruncatedBlock, ErrorOf(() => HpackPrimitives.ReadString([], ref position)));
    }
}
