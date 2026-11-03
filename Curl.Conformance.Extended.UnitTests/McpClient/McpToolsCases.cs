using System.Text.Json;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.McpClient;
using static Curl.Conformance.Extended.UnitTests.McpClient.McpClientRun;

namespace Curl.Conformance.Extended.UnitTests.McpClient;

/// <summary>MCP 2025-06-18 "Server Features &gt; Tools" and "Utilities &gt; Pagination" cases over Streamable HTTP.</summary>
[TestClass]
public sealed class McpToolsCases
{
    private const string MixedResult = """
        {"content":[
        {"type":"text","text":"a caption"},
        {"type":"image","data":"iVBORw0=","mimeType":"image/png"},
        {"type":"resource","resource":{"uri":"file:///dir/x","mimeType":"text/plain","text":"inside"}}
        ],"isError":false}
        """;

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Tools-1")]
    [SpecRequirement("McpClient:MCP20250618-Pagination-1")]
    public async Task PaginatedList_FollowsEachOpaqueCursorUntilTheLastPage()
    {
        McpReferenceTool[] tools = [Echo, new("add", "Adds two numbers", "{}"), new("time", "Tells the time", "{}")];
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer(tools) { PageSize = 2 });

        CurlRunResult result = await RunAsync(server, Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        Assert.AreEqual("echo\tEchoes its text\nadd\tAdds two numbers\ntime\tTells the time\n", Text(result.StandardOutput));
        McpHttpRequestRecord[] lists = [.. server.Requests.Where(request => request.JsonRpcMethod == "tools/list")];
        Assert.HasCount(2, lists);
        using JsonDocument second = JsonDocument.Parse(lists[1].Body);
        Assert.AreEqual("opaque:2", second.RootElement.GetProperty("params").GetProperty("cursor").GetString());
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Tools-2")]
    public async Task CallReturningTextImageAndResource_WritesEachContentItem()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([new("mixed", "Mixed content", MixedResult)]));

        CurlRunResult result = await RunAsync(server, "-d", """{"name":"mixed","arguments":{"n":1}}""", Url);

        Assert.AreEqual(0, result.ExitCode, Text(result.StandardError));
        string[] lines = Text(result.StandardOutput).Split('\n');
        Assert.AreEqual("a caption", lines[0]);
        StringAssert.Contains(lines[1], "\"iVBORw0=\"");
        StringAssert.Contains(lines[2], "file:///dir/x");
        Assert.AreEqual(string.Empty, lines[3]);
        using JsonDocument call = JsonDocument.Parse(server.Requests.Single(request => request.JsonRpcMethod == "tools/call").Body);
        Assert.AreEqual("mixed", call.RootElement.GetProperty("params").GetProperty("name").GetString());
        Assert.AreEqual(1, call.RootElement.GetProperty("params").GetProperty("arguments").GetProperty("n").GetInt32());
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Tools-3")]
    public async Task ToolReportsIsError_WritesItsContentAndExits22()
    {
        McpReferenceTool failing = new("fail", "Always fails", """{"content":[{"type":"text","text":"disk full"}],"isError":true}""");
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([failing]));

        CurlRunResult result = await RunAsync(server, "-d", """{"name":"fail","arguments":{}}""", Url);

        Assert.AreEqual(22, result.ExitCode);
        Assert.AreEqual("disk full\n", Text(result.StandardOutput));
    }

    [TestMethod]
    [SpecRequirement("McpClient:MCP20250618-Base-4")]
    public async Task JsonRpcError_ReportsCodeAndMessageAndExits8()
    {
        McpStreamableHttpReferenceServer server = new(new McpReferenceToolServer([Echo]));

        CurlRunResult result = await RunAsync(server, "-d", """{"name":"missing","arguments":{}}""", Url);

        Assert.AreEqual(8, result.ExitCode);
        Assert.AreEqual(string.Empty, Text(result.StandardOutput));
        StringAssert.Contains(Text(result.StandardError), "-32602");
        StringAssert.Contains(Text(result.StandardError), "Unknown tool: missing");
    }
}
