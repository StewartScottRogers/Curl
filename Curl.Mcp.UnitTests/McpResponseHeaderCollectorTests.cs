using System.Text;

namespace Curl.Mcp;

[TestClass]
public sealed class McpResponseHeaderCollectorTests
{
    [TestMethod]
    public void Collect_EachLine_TakesEveryByte()
    {
        byte[] line = Encoding.UTF8.GetBytes("Server: x\r\n");

        Assert.AreEqual(line.Length, new McpResponseHeaderCollector().Collect(line));
    }

    [TestMethod]
    public void Collect_StatusLine_DropsTheEarlierResponsesHeaders()
    {
        McpResponseHeaderCollector collector = Collected("HTTP/1.1 100 Continue\r\n", "X-Early: 1\r\n", "\r\n", "HTTP/2 200\r\n", "X-Late: 2\r\n");

        CollectionAssert.AreEqual(new[] { new McpTransferHeader("X-Late", "2") }, collector.Headers.ToArray());
    }

    [TestMethod]
    public void Collect_RepeatedHeaders_KeepOrderAndRepeats()
    {
        McpResponseHeaderCollector collector = Collected("B: 1\r\n", "A: 2\r\n", "B: 3\r\n");

        CollectionAssert.AreEqual(
            new[] { new McpTransferHeader("B", "1"), new McpTransferHeader("A", "2"), new McpTransferHeader("B", "3") },
            collector.Headers.ToArray());
    }

    [TestMethod]
    public void Collect_FoldedLine_ContinuesTheHeaderBefore()
    {
        McpResponseHeaderCollector collector = Collected("X-Long: one\r\n", "  two\r\n", "\tthree\r\n");

        CollectionAssert.AreEqual(new[] { new McpTransferHeader("X-Long", "one two three") }, collector.Headers.ToArray());
    }

    [TestMethod]
    public void Collect_FoldedLineFirst_IsSkipped()
    {
        Assert.AreEqual(0, Collected("  orphan\r\n").Headers.Count);
    }

    [TestMethod]
    public void Collect_BlankAndNamelessLines_AreSkipped()
    {
        McpResponseHeaderCollector collector = Collected("\r\n", ": no name\r\n", "no colon\r\n", "Empty:\r\n");

        CollectionAssert.AreEqual(new[] { new McpTransferHeader("Empty", "") }, collector.Headers.ToArray());
    }

    private static McpResponseHeaderCollector Collected(params string[] lines)
    {
        McpResponseHeaderCollector collector = new();
        foreach (string line in lines)
        {
            collector.Collect(Encoding.UTF8.GetBytes(line));
        }

        return collector;
    }
}
