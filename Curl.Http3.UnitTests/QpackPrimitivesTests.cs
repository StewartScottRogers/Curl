using Curl.Testing;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins QPACK's integer and string literal primitives to RFC 9204 section 4.1.
/// </summary>
[TestClass]
public sealed class QpackPrimitivesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(10L, 5, "0a")]
    [DataRow(1337L, 5, "1f9a0a")]
    [DataRow(220L, 5, "1fbd01")]
    public void WriteAndReadInteger_RoundTripTheValueThroughThePublishedBytes(long value, int prefixBits, string hex)
    {
        Diagnostics.Arrange("value", value);
        Diagnostics.Arrange("prefix bits", prefixBits);
        var output = new List<byte>();
        QpackPrimitives.WriteInteger(output, value, prefixBits, 0x00);
        var position = 0;
        Diagnostics.Bytes("written", output.ToArray());
        Diagnostics.Act("written length", output.Count);

        Diagnostics.Diff("written", FromHex(hex), output.ToArray());
        CollectionAssert.AreEqual(FromHex(hex), output.ToArray());
        var read = QpackPrimitives.ReadInteger(FromHex(hex), ref position, prefixBits, QpackErrorCode.DecompressionFailed);
        Diagnostics.Act("read back", read);
        Diagnostics.Assert("read back", value, read);
        Assert.AreEqual(value, read);
        Diagnostics.Assert("position", output.Count, position);
        Assert.AreEqual(output.Count, position);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(8)]
    public void WriteAndReadInteger_LargestInteger_RoundTrips(int prefixBits)
    {
        Diagnostics.Arrange("value", QpackPrimitives.LargestInteger);
        Diagnostics.Arrange("prefix bits", prefixBits);
        var output = new List<byte>();
        QpackPrimitives.WriteInteger(output, QpackPrimitives.LargestInteger, prefixBits, 0x00);
        var position = 0;
        Diagnostics.Bytes("written", output.ToArray());
        Diagnostics.Act("written length", output.Count);

        var read = QpackPrimitives.ReadInteger(output.ToArray(), ref position, prefixBits, QpackErrorCode.DecompressionFailed);

        Diagnostics.Act("read back", read);
        Diagnostics.Assert("read back", QpackPrimitives.LargestInteger, read);
        Assert.AreEqual(QpackPrimitives.LargestInteger, read);
    }

    [TestMethod]
    public void ReadInteger_LargerThan62Bits_FailsWithTheGivenError()
    {
        var output = new List<byte>();
        QpackPrimitives.WriteInteger(output, QpackPrimitives.LargestInteger + 1, 8, 0x00);
        var position = 0;
        Diagnostics.Arrange("value", QpackPrimitives.LargestInteger + 1);
        Diagnostics.Bytes("input", output.ToArray());

        var error = ErrorOf(() => QpackPrimitives.ReadInteger(output.ToArray(), ref position, 8, QpackErrorCode.EncoderStreamError));

        Diagnostics.Act("error", error);
        Diagnostics.Assert("error", QpackErrorCode.EncoderStreamError, error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, error);
    }

    [TestMethod]
    public void ReadInteger_MoreContinuationBytesThan62BitsNeed_FailsWithTheGivenError()
    {
        var position = 0;
        Diagnostics.Arrange("input", "ff 80 80 80 80 80 80 80 80 80 00");

        var error = ErrorOf(() => QpackPrimitives.ReadInteger(FromHex("ff 80 80 80 80 80 80 80 80 80 00"), ref position, 8, QpackErrorCode.DecoderStreamError));

        Diagnostics.Act("error", error);
        Diagnostics.Assert("error", QpackErrorCode.DecoderStreamError, error);
        Assert.AreEqual(QpackErrorCode.DecoderStreamError, error);
    }

    [TestMethod]
    public void ReadInteger_InputEndsInsideTheInteger_ReportsAnIncompleteInstruction()
    {
        var position = 0;
        Diagnostics.Arrange("input", "1f 9a, prefix bits 5");

        var failure = Assert.ThrowsExactly<QpackIncompleteInstructionException>(
            () => QpackPrimitives.ReadInteger(FromHex("1f 9a"), ref position, 5, QpackErrorCode.DecompressionFailed));

        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Assert("exception", nameof(QpackIncompleteInstructionException), failure.GetType().Name);
    }

    [TestMethod]
    [DataRow("custom-key", true, "a8 25a849e95ba97d7f")]
    [DataRow("a", true, "81 61")]
    [DataRow("custom-key", false, "8a 637573746f6d2d6b6579")]
    public void WriteString_HuffmanCodesOnlyWhenAllowedAndStrictlyShorter(string text, bool huffman, string hex)
    {
        Diagnostics.Arrange("text", text);
        Diagnostics.Arrange("Huffman allowed", huffman);
        var output = new List<byte>();

        QpackPrimitives.WriteString(output, text, 5, 0x80, huffman);

        Diagnostics.Bytes("written", output.ToArray());
        Diagnostics.Act("written length", output.Count);
        Diagnostics.Diff("written", FromHex(hex), output.ToArray());
        CollectionAssert.AreEqual(FromHex(hex), output.ToArray());
    }

    [TestMethod]
    [DataRow("88 25a849e95ba97d7f", "custom-key")]
    [DataRow("0a 637573746f6d2d6b6579", "custom-key")]
    public void ReadString_ReadsHuffmanAndRawLiterals(string hex, string expected)
    {
        var position = 0;
        Diagnostics.Arrange("input", hex);

        var text = QpackPrimitives.ReadString(FromHex(hex), ref position, 7, QpackErrorCode.DecompressionFailed);

        Diagnostics.Act("text", text);
        Diagnostics.Diff("text", expected, text);
        Assert.AreEqual(expected, text);
        Diagnostics.Assert("position", FromHex(hex).Length, position);
        Assert.AreEqual(FromHex(hex).Length, position);
    }

    [TestMethod]
    public void ReadString_InvalidHuffmanCoding_FailsWithTheGivenError()
    {
        var position = 0;
        Diagnostics.Arrange("input", "84 ffffffff (Huffman, all ones)");

        var error = ErrorOf(() => QpackPrimitives.ReadString(FromHex("84 ffffffff"), ref position, 7, QpackErrorCode.EncoderStreamError));

        Diagnostics.Act("error", error);
        Diagnostics.Assert("error", QpackErrorCode.EncoderStreamError, error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, error);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("05 6162")]
    public void ReadString_InputEndsInsideTheLiteral_ReportsAnIncompleteInstruction(string hex)
    {
        var position = 0;
        Diagnostics.Arrange("input", hex);

        var failure = Assert.ThrowsExactly<QpackIncompleteInstructionException>(
            () => QpackPrimitives.ReadString(FromHex(hex), ref position, 7, QpackErrorCode.DecompressionFailed));

        Diagnostics.Act("exception", failure.GetType().Name);
        Diagnostics.Assert("exception", nameof(QpackIncompleteInstructionException), failure.GetType().Name);
    }
}
