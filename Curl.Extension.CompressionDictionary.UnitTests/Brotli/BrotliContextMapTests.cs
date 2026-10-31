using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliContextMapTests
{
    // RFC 7932 section 7.3: NTREES 2, RLEMAX 1 and a code holding only symbol 1, a run of 2 or 3 zeros:
    // 21 runs of 3 fill 63 entries, and a run of 2 overruns the 64th.
    [TestMethod]
    public void Read_ZeroRunPastEnd_Throws()
    {
        var writer = new BrotliBitWriter().Bits(1, 1).Bits(0, 3).Bits(1, 1).Bits(0, 4).SimpleCode(3, 1);
        for (var run = 0; run < 21; run++)
        {
            writer.Bits(1, 1);
        }

        var reader = new BrotliBitReader(writer.Bits(0, 9).ToArray());

        Assert.ThrowsExactly<BrotliFormatException>(() => BrotliContextMap.Read(reader, 64, out _));
    }

    // NTREES 2, no runs, a code of symbols 0 and 1, the map 1, 1, 0, then the inverse move-to-front transform.
    [TestMethod]
    public void Read_InverseMoveToFront_UndoesTheTransform()
    {
        var reader = new BrotliBitReader(new BrotliBitWriter().Bits(1, 1).Bits(0, 3).Bits(0, 1).SimpleCode(2, 0, 1).Bits(1, 1).Bits(1, 1).Bits(0, 1).Bits(1, 1).Bits(0, 8).ToArray());

        var map = BrotliContextMap.Read(reader, 3, out var treeCount);

        Assert.AreEqual(2, treeCount);
        CollectionAssert.AreEqual(new byte[] { 1, 0, 0 }, map);
    }
}
