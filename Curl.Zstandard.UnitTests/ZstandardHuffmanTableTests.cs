using Curl.Testing;
using static Curl.Zstandard.ZstandardTestLiterals;

namespace Curl.Zstandard;

/// <summary>
/// Pins <see cref="ZstandardHuffmanTable" /> to RFC 8878 section 4.2.1's worked example:
/// weights 4, 3, 2, 0, 1 describe a 4-bit tree whose prefix codes are 1, 01, 001, 0000 and
/// 0001 for literals 0, 1, 2, 4 and 5 (BL-859).
/// </summary>
[TestClass]
public sealed class ZstandardHuffmanTableTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly byte[] RfcTreeDescription = DirectTreeDescription(4, 3, 2, 0, 1);

    [TestMethod]
    public void Read_RfcExampleWeights_ReadsTheDirectDescription()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("weights", "4, 3, 2, 0, 1 (RFC 8878 section 4.2.1)");
        diagnostics.Bytes("direct tree description, then 0xAA", [.. RfcTreeDescription, 0xAA]);

        var table = ZstandardHuffmanTable.Read([.. RfcTreeDescription, 0xAA], out var bytesRead);

        diagnostics.Act("table read", table is not null);
        diagnostics.Act("max number of bits", table?.MaxNumberOfBits);
        diagnostics.Act("bytes read", bytesRead);
        diagnostics.Assert("max number of bits", 4, table?.MaxNumberOfBits);
        diagnostics.Assert("bytes read", 4, bytesRead);
        diagnostics.Diff("description", [0x84, 0x43, 0x20, 0x10], RfcTreeDescription);
        Assert.IsNotNull(table);
        Assert.AreEqual(4, table.MaxNumberOfBits);
        Assert.AreEqual(4, bytesRead);
        CollectionAssert.AreEqual(new byte[] { 0x84, 0x43, 0x20, 0x10 }, RfcTreeDescription);
    }

    [TestMethod]
    public void TryDecodeStream_RfcExamplePrefixCodes_DecodesEachLiteral()
    {
        var table = ZstandardHuffmanTable.Read(RfcTreeDescription, out _)!;
        var stream = BackwardStream([(0b1, 1), (0b0000, 4), (0b0001, 4), (0b001, 3), (0b01, 2), (0b1, 1)]);
        var destination = new byte[6];
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("prefix codes written", "1, 0000, 0001, 001, 01, 1");
        diagnostics.Bytes("backward stream", stream);

        var decoded = table.TryDecodeStream(stream, destination);

        diagnostics.Act("decoded", decoded);
        diagnostics.ActOutput([0, 4, 5, 2, 1, 0], destination);
        Assert.IsTrue(decoded);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 5, 2, 1, 0 }, destination);
    }

    [TestMethod]
    public void HuffmanCodes_RfcExampleWeights_AreTheRfcPrefixCodes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("weights", "4, 3, 2, 0, 1");

        var codes = HuffmanCodes([4, 3, 2, 0, 1]);

        var actual = string.Join(", ", codes[..6]);
        diagnostics.Act("codes for literals 0 to 5 as (code, bits)", actual);
        diagnostics.Diff("codes for literals 0 to 5", "(1, 1), (1, 2), (1, 3), (0, 0), (0, 4), (1, 4)", actual);
        CollectionAssert.AreEqual(
            new (uint, int)[] { (0b1, 1), (0b01, 2), (0b001, 3), (0, 0), (0b0000, 4), (0b0001, 4) },
            codes[..6]);
    }

    [TestMethod]
    public void Read_EmptySource_IsNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("source", "empty");

        var table = ZstandardHuffmanTable.Read([], out var bytesRead);

        diagnostics.Act("table read", table is not null);
        diagnostics.Act("bytes read", bytesRead);
        diagnostics.Assert("table read and bytes read", "False and 0", $"{table is not null} and {bytesRead}");
        Assert.IsNull(table);
        Assert.AreEqual(0, bytesRead);
    }
}
