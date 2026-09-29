using Curl.Http2;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins the static table (RFC 9204 appendix A), the dynamic table's eviction arithmetic
/// (section 3.2) and the Required Insert Count's encoding (section 4.5.1.1).
/// </summary>
[TestClass]
public sealed class QpackTablesTests
{
    [TestMethod]
    public void StaticTable_FirstAndLastEntries_MatchAppendixA()
    {
        Assert.AreEqual(new HeaderField(":authority", string.Empty), QpackStaticTable.Get(0, QpackErrorCode.DecompressionFailed));
        Assert.AreEqual(new HeaderField("x-frame-options", "sameorigin"), QpackStaticTable.Get(98, QpackErrorCode.DecompressionFailed));
    }

    [TestMethod]
    public void StaticTable_IndexPastTheTable_FailsWithTheGivenError() =>
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, ErrorOf(() => QpackStaticTable.Get(99, QpackErrorCode.EncoderStreamError)));

    [TestMethod]
    [DataRow(":method", "PUT", 21, true)]
    [DataRow(":method", "PATCH", 15, false)]
    [DataRow("x-custom", "1", -1, false)]
    public void StaticTable_Find_PrefersAnExactMatchThenTheFirstEntryWithTheName(string name, string value, int index, bool isExact) =>
        Assert.AreEqual((index, isExact), QpackStaticTable.Find(name, value));

    [TestMethod]
    public void DynamicTable_DroppedCountAfterFitting_CountsOnlyTheEvictionsNeeded()
    {
        var table = new QpackDynamicTable();
        table.SetCapacity(100);
        table.Insert(new HeaderField("a", "1"));
        table.Insert(new HeaderField("b", "2"));

        Assert.AreEqual(0, table.DroppedCountAfterFitting(0, 100));
        Assert.AreEqual(1, table.DroppedCountAfterFitting(34, 100));
        Assert.AreEqual(2, table.DroppedCountAfterFitting(0, 0));
    }

    [TestMethod]
    public void DynamicTable_FindNewest_MatchesTheNewestUsableEntry()
    {
        var table = new QpackDynamicTable();
        table.SetCapacity(200);
        table.Insert(new HeaderField("a", "1", IsNeverIndexed: true));
        table.Insert(new HeaderField("a", "2"));

        Assert.AreEqual(1, table.FindNewest("a", "x", false, long.MaxValue));
        Assert.AreEqual(0, table.FindNewest("a", "1", true, long.MaxValue));
        Assert.AreEqual(0, table.FindNewest("a", "x", false, 0));
        Assert.AreEqual(-1, table.FindNewest("b", "1", false, long.MaxValue));
        Assert.IsFalse(table.Get(0).IsNeverIndexed);
    }

    [TestMethod]
    public void DynamicTable_Contains_HoldsOnlyForInsertedEntriesNotEvicted()
    {
        var table = new QpackDynamicTable();
        table.SetCapacity(34);
        table.Insert(new HeaderField("a", "1"));
        table.Insert(new HeaderField("b", "2"));

        Assert.IsFalse(table.Contains(0));
        Assert.IsTrue(table.Contains(1));
        Assert.IsFalse(table.Contains(2));
    }

    [TestMethod]
    [DataRow(0L, 0L)]
    [DataRow(2L, 3L)]
    [DataRow(12L, 1L)]
    public void RequiredInsertCount_Encode_IsTheCountModuloTwiceMaxEntriesPlusOne(long requiredInsertCount, long encoded) =>
        Assert.AreEqual(encoded, QpackRequiredInsertCount.Encode(requiredInsertCount, 6));

    [TestMethod]
    [DataRow(0L, 0L, 0L)]
    [DataRow(3L, 2L, 2L)]
    [DataRow(12L, 10L, 11L)]
    [DataRow(1L, 12L, 12L)]
    public void RequiredInsertCount_Decode_RecoversTheCountNearestTheInsertCount(long encoded, long totalInsertCount, long expected) =>
        Assert.AreEqual(expected, QpackRequiredInsertCount.Decode(encoded, 6, totalInsertCount));

    [TestMethod]
    [DataRow(13L, 6L, 0L)]
    [DataRow(8L, 6L, 0L)]
    [DataRow(1L, 6L, 0L)]
    [DataRow(1L, 0L, 0L)]
    public void RequiredInsertCount_Decode_UnreachableEncoding_FailsAsDecompressionFailed(long encoded, long maximumEntries, long totalInsertCount) =>
        Assert.AreEqual(
            QpackErrorCode.DecompressionFailed,
            ErrorOf(() => QpackRequiredInsertCount.Decode(encoded, maximumEntries, totalInsertCount)));
}
