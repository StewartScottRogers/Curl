using Curl.Http2;
using Curl.Testing;
using static Curl.Http3.Qpack;

namespace Curl.Http3;

/// <summary>
/// Pins the static table (RFC 9204 appendix A), the dynamic table's eviction arithmetic
/// (section 3.2) and the Required Insert Count's encoding (section 4.5.1.1).
/// </summary>
[TestClass]
public sealed class QpackTablesTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void StaticTable_FirstAndLastEntries_MatchAppendixA()
    {
        Diagnostics.Arrange("indexes", "0 and 98");

        var first = QpackStaticTable.Get(0, QpackErrorCode.DecompressionFailed);
        var last = QpackStaticTable.Get(98, QpackErrorCode.DecompressionFailed);

        Diagnostics.Act("entry 0", first);
        Diagnostics.Act("entry 98", last);
        Diagnostics.Assert("entry 0", new HeaderField(":authority", string.Empty), first);
        Assert.AreEqual(new HeaderField(":authority", string.Empty), first);
        Diagnostics.Assert("entry 98", new HeaderField("x-frame-options", "sameorigin"), last);
        Assert.AreEqual(new HeaderField("x-frame-options", "sameorigin"), last);
    }

    [TestMethod]
    public void StaticTable_IndexPastTheTable_FailsWithTheGivenError()
    {
        Diagnostics.Arrange("index", 99);

        var error = ErrorOf(() => QpackStaticTable.Get(99, QpackErrorCode.EncoderStreamError));

        Diagnostics.Act("error", error);
        Diagnostics.Assert("error", QpackErrorCode.EncoderStreamError, error);
        Assert.AreEqual(QpackErrorCode.EncoderStreamError, error);
    }

    [TestMethod]
    [DataRow(":method", "PUT", 21, true)]
    [DataRow(":method", "PATCH", 15, false)]
    [DataRow("x-custom", "1", -1, false)]
    public void StaticTable_Find_PrefersAnExactMatchThenTheFirstEntryWithTheName(string name, string value, int index, bool isExact)
    {
        Diagnostics.Arrange("field", $"{name}: {value}");

        var found = QpackStaticTable.Find(name, value);

        Diagnostics.Act("found", found);
        Diagnostics.Assert("found", (index, isExact), found);
        Assert.AreEqual((index, isExact), found);
    }

    [TestMethod]
    public void DynamicTable_DroppedCountAfterFitting_CountsOnlyTheEvictionsNeeded()
    {
        Diagnostics.Arrange("table", "capacity 100, entries a: 1 and b: 2 (34 bytes each)");
        var table = new QpackDynamicTable();
        table.SetCapacity(100);
        table.Insert(new HeaderField("a", "1"));
        table.Insert(new HeaderField("b", "2"));

        var none = table.DroppedCountAfterFitting(0, 100);
        var one = table.DroppedCountAfterFitting(34, 100);
        var both = table.DroppedCountAfterFitting(0, 0);

        Diagnostics.Act("dropped (0, 100), (34, 100), (0, 0)", $"{none}, {one}, {both}");
        Diagnostics.Assert("dropped", "0, 1, 2", $"{none}, {one}, {both}");
        Assert.AreEqual(0, none);
        Assert.AreEqual(1, one);
        Assert.AreEqual(2, both);
    }

    [TestMethod]
    public void DynamicTable_FindNewest_MatchesTheNewestUsableEntry()
    {
        Diagnostics.Arrange("table", "capacity 200, entries a: 1 (never indexed) and a: 2");
        var table = new QpackDynamicTable();
        table.SetCapacity(200);
        table.Insert(new HeaderField("a", "1", IsNeverIndexed: true));
        table.Insert(new HeaderField("a", "2"));

        var nameOnly = table.FindNewest("a", "x", false, long.MaxValue);
        var exact = table.FindNewest("a", "1", true, long.MaxValue);
        var limited = table.FindNewest("a", "x", false, 0);
        var missing = table.FindNewest("b", "1", false, long.MaxValue);

        Diagnostics.Act("found", $"name only {nameOnly}, exact {exact}, limited {limited}, missing {missing}");
        Diagnostics.Assert("found", "name only 1, exact 0, limited 0, missing -1", $"name only {nameOnly}, exact {exact}, limited {limited}, missing {missing}");
        Assert.AreEqual(1, nameOnly);
        Assert.AreEqual(0, exact);
        Assert.AreEqual(0, limited);
        Assert.AreEqual(-1, missing);
        Assert.IsFalse(table.Get(0).IsNeverIndexed);
    }

    [TestMethod]
    public void DynamicTable_Contains_HoldsOnlyForInsertedEntriesNotEvicted()
    {
        Diagnostics.Arrange("table", "capacity 34, entries a: 1 (evicted) and b: 2");
        var table = new QpackDynamicTable();
        table.SetCapacity(34);
        table.Insert(new HeaderField("a", "1"));
        table.Insert(new HeaderField("b", "2"));

        var contains = $"{table.Contains(0)}, {table.Contains(1)}, {table.Contains(2)}";

        Diagnostics.Act("contains 0, 1, 2", contains);
        Diagnostics.Assert("contains 0, 1, 2", "False, True, False", contains);
        Assert.IsFalse(table.Contains(0));
        Assert.IsTrue(table.Contains(1));
        Assert.IsFalse(table.Contains(2));
    }

    [TestMethod]
    [DataRow(0L, 0L)]
    [DataRow(2L, 3L)]
    [DataRow(12L, 1L)]
    public void RequiredInsertCount_Encode_IsTheCountModuloTwiceMaxEntriesPlusOne(long requiredInsertCount, long encoded)
    {
        Diagnostics.Arrange("required insert count, maximum entries", $"{requiredInsertCount}, 6");

        var actual = QpackRequiredInsertCount.Encode(requiredInsertCount, 6);

        Diagnostics.Act("encoded", actual);
        Diagnostics.Assert("encoded", encoded, actual);
        Assert.AreEqual(encoded, actual);
    }

    [TestMethod]
    [DataRow(0L, 0L, 0L)]
    [DataRow(3L, 2L, 2L)]
    [DataRow(12L, 10L, 11L)]
    [DataRow(1L, 12L, 12L)]
    public void RequiredInsertCount_Decode_RecoversTheCountNearestTheInsertCount(long encoded, long totalInsertCount, long expected)
    {
        Diagnostics.Arrange("encoded, maximum entries, total insert count", $"{encoded}, 6, {totalInsertCount}");

        var actual = QpackRequiredInsertCount.Decode(encoded, 6, totalInsertCount);

        Diagnostics.Act("required insert count", actual);
        Diagnostics.Assert("required insert count", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(13L, 6L, 0L)]
    [DataRow(8L, 6L, 0L)]
    [DataRow(1L, 6L, 0L)]
    [DataRow(1L, 0L, 0L)]
    public void RequiredInsertCount_Decode_UnreachableEncoding_FailsAsDecompressionFailed(long encoded, long maximumEntries, long totalInsertCount)
    {
        Diagnostics.Arrange("encoded, maximum entries, total insert count", $"{encoded}, {maximumEntries}, {totalInsertCount}");

        var error = ErrorOf(() => QpackRequiredInsertCount.Decode(encoded, maximumEntries, totalInsertCount));

        Diagnostics.Act("error", error);
        Diagnostics.Assert("error", QpackErrorCode.DecompressionFailed, error);
        Assert.AreEqual(QpackErrorCode.DecompressionFailed, error);
    }
}
