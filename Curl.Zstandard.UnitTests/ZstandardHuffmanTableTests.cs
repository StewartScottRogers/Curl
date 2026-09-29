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
    private static readonly byte[] RfcTreeDescription = DirectTreeDescription(4, 3, 2, 0, 1);

    [TestMethod]
    public void Read_RfcExampleWeights_ReadsTheDirectDescription()
    {
        var table = ZstandardHuffmanTable.Read([.. RfcTreeDescription, 0xAA], out var bytesRead);

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

        var decoded = table.TryDecodeStream(stream, destination);

        Assert.IsTrue(decoded);
        CollectionAssert.AreEqual(new byte[] { 0, 4, 5, 2, 1, 0 }, destination);
    }

    [TestMethod]
    public void HuffmanCodes_RfcExampleWeights_AreTheRfcPrefixCodes()
    {
        var codes = HuffmanCodes([4, 3, 2, 0, 1]);

        CollectionAssert.AreEqual(
            new (uint, int)[] { (0b1, 1), (0b01, 2), (0b001, 3), (0, 0), (0b0000, 4), (0b0001, 4) },
            codes[..6]);
    }

    [TestMethod]
    public void Read_EmptySource_IsNull()
    {
        Assert.IsNull(ZstandardHuffmanTable.Read([], out var bytesRead));
        Assert.AreEqual(0, bytesRead);
    }
}
