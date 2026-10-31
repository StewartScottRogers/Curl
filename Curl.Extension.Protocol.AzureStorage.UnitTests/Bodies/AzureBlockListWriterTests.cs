namespace Curl.Extension.Protocol.AzureStorage.Bodies;

/// <summary>Pins the <c>BlockList</c> request body of the Blob service REST API "Put Block List".</summary>
[TestClass]
public sealed class AzureBlockListWriterTests
{
    [TestMethod]
    public void Write_ReferenceSampleBlocks_WritesOneElementPerBlockInOrder()
    {
        // "Put Block List", "Request body" sample: Committed, Uncommitted and Latest block IDs.
        var body = AzureBlockListWriter.Write(
        [
            new AzureBlockListEntry("AAAAAA==", AzureBlockListSource.Committed),
            new AzureBlockListEntry("AQAAAA==", AzureBlockListSource.Uncommitted),
            new AzureBlockListEntry("AgAAAA==", AzureBlockListSource.Latest),
        ]);

        Assert.AreEqual(
            "<BlockList><Committed>AAAAAA==</Committed><Uncommitted>AQAAAA==</Uncommitted><Latest>AgAAAA==</Latest></BlockList>",
            body);
    }

    [TestMethod]
    public void Write_NoBlocks_WritesAnEmptyBlockList() =>
        Assert.AreEqual("<BlockList />", AzureBlockListWriter.Write([]));

    [TestMethod]
    public void Write_NullBlocks_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureBlockListWriter.Write(null!));
}
