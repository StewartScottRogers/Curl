using Curl.Testing;

namespace Curl.Http2;

/// <summary>
/// Checks <see cref="HpackDynamicTable" /> evicts as RFC 7541 sections 4.3 and 4.4 say.
/// </summary>
[TestClass]
public sealed class HpackDynamicTableTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Add_EntryBiggerThanTheTable_EmptiesTheTableAndIsNotAdded()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var table = new HpackDynamicTable(100);
        table.Add(new HeaderField("a", "b"));
        diagnostics.Arrange("maximum size", 100);
        diagnostics.Arrange("count before", table.Count);

        table.Add(new HeaderField("x", new string('y', 100)));
        diagnostics.Act("count", table.Count);
        diagnostics.Act("size", table.Size);

        diagnostics.Assert("count", 0, table.Count);
        diagnostics.Assert("size", 0, table.Size);
        Assert.AreEqual(0, table.Count);
        Assert.AreEqual(0, table.Size);
    }

    [TestMethod]
    public void Add_EntryBiggerThanAnEmptyTable_IsNotAdded()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var table = new HpackDynamicTable(0);
        diagnostics.Arrange("maximum size", 0);

        table.Add(new HeaderField("a", "b"));
        diagnostics.Act("count", table.Count);

        diagnostics.Assert("count", 0, table.Count);
        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void Add_TableFull_EvictsTheOldestAndKeepsTheNewestFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var table = new HpackDynamicTable(67);
        table.Add(new HeaderField("a", "1"));
        diagnostics.Arrange("maximum size", 67);
        diagnostics.Arrange("count before", table.Count);

        table.Add(new HeaderField("b", "2", IsNeverIndexed: true));
        diagnostics.Act("count", table.Count);
        diagnostics.Act("newest entry", table.Get(1));

        diagnostics.Assert("count", 1, table.Count);
        diagnostics.Assert("newest entry", new HeaderField("b", "2"), table.Get(1));
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(new HeaderField("b", "2"), table.Get(1));
    }

    [TestMethod]
    public void Resize_Smaller_EvictsTheOldest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var table = new HpackDynamicTable(4096);
        table.Add(new HeaderField("a", "1"));
        table.Add(new HeaderField("b", "2"));
        diagnostics.Arrange("maximum size", 4096);
        diagnostics.Arrange("count before", table.Count);

        table.Resize(34);
        diagnostics.Act("count", table.Count);
        diagnostics.Act("newest entry", table.Get(1));
        diagnostics.Act("maximum size", table.MaximumSize);

        diagnostics.Assert("count", 1, table.Count);
        diagnostics.Assert("maximum size", 34, table.MaximumSize);
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(new HeaderField("b", "2"), table.Get(1));
        Assert.AreEqual(34, table.MaximumSize);
    }

    [TestMethod]
    public void HeaderFieldSize_IsNameAndValueLengthPlus32()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var field = new HeaderField("custom-key", "custom-header");
        diagnostics.Arrange("field", field);

        var size = field.Size;
        diagnostics.Act("size", size);

        diagnostics.Assert("size", 55, size);
        Assert.AreEqual(55, new HeaderField("custom-key", "custom-header").Size);
    }
}
