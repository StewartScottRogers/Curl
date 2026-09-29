using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins QPACK's integer and string literal primitives to RFC 9204 section 4.1.
/// </summary>
[TestClass]
public sealed class QpackPrimitivesTests
{
    [TestMethod]
    [DataRow(10L, 5, "0a")]
    [DataRow(1337L, 5, "1f9a0a")]
    [DataRow(220L, 5, "1fbd01")]
    public void WriteAndReadInteger_RoundTripTheValueThroughThePublishedBytes(long value, int prefixBits, string hex)
    {
        var output = new List<byte>();
        QpackPrimitives.WriteInteger(output, value, prefixBits, 0x00);
        var position = 0;

        CollectionAssert.AreEqual(FromHex(hex), output.ToArray());
        Assert.AreEqual(value, QpackPrimitives.ReadInteger(FromHex(hex), ref position, prefixBits, QpackErrorCode.DecompressionFailed));
        Assert.AreEqual(output.Count, position);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(8)]
    public void WriteAndReadInteger_LargestInteger_RoundTrips(int prefixBits)
    {
        var output = new List<byte>();
        QpackPrimitives.WriteInteger(output, QpackPrimitives.LargestInteger, prefixBits, 0x00);
        var position = 0;

        Assert.AreEqual(
            QpackPrimitives.LargestInteger,
            QpackPrimitives.ReadInteger(output.ToArray(), ref position, prefixBits, QpackErrorCode.DecompressionFailed));
    }

    [TestMethod]
    public void ReadInteger_LargerThan62Bits_FailsWithTheGivenError()
    {
        var output = new List<byte>();
        QpackPrimitives.WriteInteger(output, QpackPrimitives.LargestInteger + 1, 8, 0x00);
        var position = 0;

        Assert.AreEqual(
            QpackErrorCode.EncoderStreamError,
            ErrorOf(() => QpackPrimitives.ReadInteger(output.ToArray(), ref position, 8, QpackErrorCode.EncoderStreamError)));
    }

    [TestMethod]
    public void ReadInteger_MoreContinuationBytesThan62BitsNeed_FailsWithTheGivenError()
    {
        var position = 0;

        Assert.AreEqual(
            QpackErrorCode.DecoderStreamError,
            ErrorOf(() => QpackPrimitives.ReadInteger(FromHex("ff 80 80 80 80 80 80 80 80 80 00"), ref position, 8, QpackErrorCode.DecoderStreamError)));
    }

    [TestMethod]
    public void ReadInteger_InputEndsInsideTheInteger_ReportsAnIncompleteInstruction()
    {
        var position = 0;

        Assert.ThrowsExactly<QpackIncompleteInstructionException>(
            () => QpackPrimitives.ReadInteger(FromHex("1f 9a"), ref position, 5, QpackErrorCode.DecompressionFailed));
    }

    [TestMethod]
    [DataRow("custom-key", true, "a8 25a849e95ba97d7f")]
    [DataRow("a", true, "81 61")]
    [DataRow("custom-key", false, "8a 637573746f6d2d6b6579")]
    public void WriteString_HuffmanCodesOnlyWhenAllowedAndStrictlyShorter(string text, bool huffman, string hex)
    {
        var output = new List<byte>();

        QpackPrimitives.WriteString(output, text, 5, 0x80, huffman);

        CollectionAssert.AreEqual(FromHex(hex), output.ToArray());
    }

    [TestMethod]
    [DataRow("88 25a849e95ba97d7f", "custom-key")]
    [DataRow("0a 637573746f6d2d6b6579", "custom-key")]
    public void ReadString_ReadsHuffmanAndRawLiterals(string hex, string expected)
    {
        var position = 0;

        Assert.AreEqual(expected, QpackPrimitives.ReadString(FromHex(hex), ref position, 7, QpackErrorCode.DecompressionFailed));
        Assert.AreEqual(FromHex(hex).Length, position);
    }

    [TestMethod]
    public void ReadString_InvalidHuffmanCoding_FailsWithTheGivenError()
    {
        var position = 0;

        Assert.AreEqual(
            QpackErrorCode.EncoderStreamError,
            ErrorOf(() => QpackPrimitives.ReadString(FromHex("84 ffffffff"), ref position, 7, QpackErrorCode.EncoderStreamError)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("05 6162")]
    public void ReadString_InputEndsInsideTheLiteral_ReportsAnIncompleteInstruction(string hex)
    {
        var position = 0;

        Assert.ThrowsExactly<QpackIncompleteInstructionException>(
            () => QpackPrimitives.ReadString(FromHex(hex), ref position, 7, QpackErrorCode.DecompressionFailed));
    }
}
