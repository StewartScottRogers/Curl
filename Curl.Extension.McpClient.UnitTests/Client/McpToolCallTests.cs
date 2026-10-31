using System.Text;

namespace Curl.Extension.McpClient.Client;

/// <summary>Checks the <c>-d</c> body that names a <c>tools/call</c> ("Server Features &gt; Tools &gt; Calling Tools").</summary>
[TestClass]
public sealed class McpToolCallTests
{
    [TestMethod]
    public void From_NameAndArguments_GivesBoth() =>
        Assert.AreEqual(new McpToolCall("add", """{"a":1}"""), McpToolCall.From(Encoding.UTF8.GetBytes("""{"name":"add","arguments":{"a":1}}""")));

    [TestMethod]
    public void From_NameOnly_GivesEmptyArguments() =>
        Assert.AreEqual(new McpToolCall("now", "{}"), McpToolCall.From(Encoding.UTF8.GetBytes("""{"name":"now"}""")));

    [TestMethod]
    [DataRow("[]")]
    [DataRow("""{"name":1}""")]
    [DataRow("""{"name":"x","arguments":[]}""")]
    [DataRow("{")]
    public void From_NotAToolCall_GivesNull(string body) =>
        Assert.IsNull(McpToolCall.From(Encoding.UTF8.GetBytes(body)));
}
