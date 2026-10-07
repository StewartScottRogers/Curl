using Curl.Testing;
using static Curl.Http2.Hpack;

namespace Curl.Http2;

/// <summary>
/// Pins HPACK's integer and string literal primitives to RFC 7541 sections 5.1 and 5.2 and
/// appendix C.1.
/// </summary>
[TestClass]
public sealed class HpackPrimitivesTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // RFC 7541 appendix C.1.1, C.1.2 and C.1.3.
    [TestMethod]
    [DataRow(10, 5, "0a")]
    [DataRow(1337, 5, "1f9a0a")]
    [DataRow(42, 8, "2a")]
    public void WriteInteger_AppendixC1Examples_GiveThePublishedBytes(int value, int prefixBits, string hex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new List<byte>();
        diagnostics.Arrange("value", value);
        diagnostics.Arrange("prefix bits", prefixBits);

        HpackPrimitives.WriteInteger(output, value, prefixBits, 0x00);
        var actual = output.ToArray();
        diagnostics.Act("written byte count", actual.Length);
        diagnostics.Bytes("written", actual);

        diagnostics.Diff("written", FromHex(hex), actual);
        CollectionAssert.AreEqual(FromHex(hex), actual);
    }

    [TestMethod]
    [DataRow(10, 5, "0a")]
    [DataRow(1337, 5, "1f9a0a")]
    [DataRow(42, 8, "2a")]
    [DataRow(int.MaxValue, 5, "1fe0ffffff07")]
    public void ReadInteger_AppendixC1Examples_GiveTheValueAndConsumeEveryByte(int value, int prefixBits, string hex)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var bytes = FromHex(hex);
        var position = 0;
        diagnostics.Arrange("prefix bits", prefixBits);
        diagnostics.Bytes("encoded", bytes);

        var actual = HpackPrimitives.ReadInteger(bytes, ref position, prefixBits);
        diagnostics.Act("value read", actual);
        diagnostics.Act("position after", position);

        diagnostics.Assert("value", value, actual);
        diagnostics.Assert("position", bytes.Length, position);
        Assert.AreEqual(value, actual);
        Assert.AreEqual(bytes.Length, position);
    }

    [TestMethod]
    public void ReadInteger_ValueBeyond31Bits_FailsAsIntegerOverflow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var position = 0;
        diagnostics.Arrange("encoded", "1fe1ffffff07");

        var error = ErrorOf(() => HpackPrimitives.ReadInteger(FromHex("1fe1ffffff07"), ref position, 5));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.IntegerOverflow, error);
        Assert.AreEqual(HpackDecodingError.IntegerOverflow, error);
    }

    [TestMethod]
    public void ReadInteger_TooManyContinuationBytes_FailsAsIntegerOverflow()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var position = 0;
        diagnostics.Arrange("encoded", "1f8080808080 00");

        var error = ErrorOf(() => HpackPrimitives.ReadInteger(FromHex("1f8080808080 00"), ref position, 5));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.IntegerOverflow, error);
        Assert.AreEqual(HpackDecodingError.IntegerOverflow, error);
    }

    [TestMethod]
    public void ReadInteger_MissingContinuationByte_FailsAsTruncatedBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var position = 0;
        diagnostics.Arrange("encoded", "1f9a");

        var error = ErrorOf(() => HpackPrimitives.ReadInteger(FromHex("1f9a"), ref position, 5));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.TruncatedBlock, error);
        Assert.AreEqual(HpackDecodingError.TruncatedBlock, error);
    }

    [TestMethod]
    public void WriteString_HuffmanIsShorter_WritesHuffman()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new List<byte>();
        diagnostics.Arrange("string", "www.example.com");

        HpackPrimitives.WriteString(output, "www.example.com");
        var actual = output.ToArray();
        diagnostics.Act("written byte count", actual.Length);
        diagnostics.Bytes("written", actual);

        diagnostics.Diff("written", FromHex("8c f1e3 c2e5 f23a 6ba0 ab90 f4ff"), actual);
        CollectionAssert.AreEqual(FromHex("8c f1e3 c2e5 f23a 6ba0 ab90 f4ff"), actual);
    }

    [TestMethod]
    public void WriteString_HuffmanIsNoShorter_WritesRawBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var output = new List<byte>();
        diagnostics.Arrange("string", "*/*");

        HpackPrimitives.WriteString(output, "*/*");
        var actual = output.ToArray();
        diagnostics.Act("written byte count", actual.Length);
        diagnostics.Bytes("written", actual);

        diagnostics.Diff("written", FromHex("03 2a2f2a"), actual);
        CollectionAssert.AreEqual(FromHex("03 2a2f2a"), actual);
    }

    [TestMethod]
    public void ReadString_LengthBeyondBlock_FailsAsTruncatedBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var position = 0;
        diagnostics.Arrange("encoded", "05 6162");

        var error = ErrorOf(() => HpackPrimitives.ReadString(FromHex("05 6162"), ref position));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.TruncatedBlock, error);
        Assert.AreEqual(HpackDecodingError.TruncatedBlock, error);
    }

    [TestMethod]
    public void ReadString_AtEndOfBlock_FailsAsTruncatedBlock()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var position = 0;
        diagnostics.Arrange("encoded", "empty block");

        var error = ErrorOf(() => HpackPrimitives.ReadString([], ref position));
        diagnostics.Act("error", error);

        diagnostics.Assert("error", HpackDecodingError.TruncatedBlock, error);
        Assert.AreEqual(HpackDecodingError.TruncatedBlock, error);
    }
}
