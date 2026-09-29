namespace Curl.Http2;

/// <summary>
/// Checks <see cref="HpackDynamicTable" /> evicts as RFC 7541 sections 4.3 and 4.4 say.
/// </summary>
[TestClass]
public sealed class HpackDynamicTableTests
{
    [TestMethod]
    public void Add_EntryBiggerThanTheTable_EmptiesTheTableAndIsNotAdded()
    {
        var table = new HpackDynamicTable(100);
        table.Add(new HeaderField("a", "b"));

        table.Add(new HeaderField("x", new string('y', 100)));

        Assert.AreEqual(0, table.Count);
        Assert.AreEqual(0, table.Size);
    }

    [TestMethod]
    public void Add_EntryBiggerThanAnEmptyTable_IsNotAdded()
    {
        var table = new HpackDynamicTable(0);

        table.Add(new HeaderField("a", "b"));

        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void Add_TableFull_EvictsTheOldestAndKeepsTheNewestFirst()
    {
        var table = new HpackDynamicTable(67);
        table.Add(new HeaderField("a", "1"));

        table.Add(new HeaderField("b", "2", IsNeverIndexed: true));

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(new HeaderField("b", "2"), table.Get(1));
    }

    [TestMethod]
    public void Resize_Smaller_EvictsTheOldest()
    {
        var table = new HpackDynamicTable(4096);
        table.Add(new HeaderField("a", "1"));
        table.Add(new HeaderField("b", "2"));

        table.Resize(34);

        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(new HeaderField("b", "2"), table.Get(1));
        Assert.AreEqual(34, table.MaximumSize);
    }

    [TestMethod]
    public void HeaderFieldSize_IsNameAndValueLengthPlus32()
    {
        Assert.AreEqual(55, new HeaderField("custom-key", "custom-header").Size);
    }
}
