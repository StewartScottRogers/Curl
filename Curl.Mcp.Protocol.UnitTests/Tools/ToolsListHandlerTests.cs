using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Protocol.Tools;

[TestClass]
public sealed class ToolsListHandlerTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static McpServerRegistry Registry(params IMcpTool[] tools)
    {
        McpServerRegistry registry = new();
        foreach (IMcpTool tool in tools)
        {
            registry.Add(tool);
        }

        return registry;
    }

    [TestMethod]
    public void Constructor_NullRegistry_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new ToolsListHandler(null!));

    [TestMethod]
    public void Method_IsToolsList() => Assert.AreEqual("tools/list", new ToolsListHandler(new McpServerRegistry()).Method);

    [TestMethod]
    public void Capability_IsToolsWithListChanged()
    {
        ToolsListHandler handler = new(new McpServerRegistry());

        Assert.AreEqual("tools", handler.CapabilityName);
        Assert.IsTrue(JsonElement.DeepEquals(Json("""{"listChanged":true}"""), handler.Capability));
    }

    [TestMethod]
    public async Task HandleAsync_EmptyRegistry_ListsNoTools()
    {
        JsonElement result = await new ToolsListHandler(new McpServerRegistry()).HandleAsync(Json("{}"), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(Json("""{"tools":[]}"""), result), result.GetRawText());
    }

    [TestMethod]
    public async Task HandleAsync_SeveralTools_ListsThemInRegistrationOrder()
    {
        ToolsListHandler handler = new(Registry(FakeMcpTool.Succeeding("zeta"), FakeMcpTool.Succeeding("alpha"), FakeMcpTool.Succeeding("mid")));

        JsonElement result = await handler.HandleAsync(null, CancellationToken.None);

        string[] names = [.. result.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!)];
        CollectionAssert.AreEqual(new[] { "zeta", "alpha", "mid" }, names);
    }

    [TestMethod]
    public async Task HandleAsync_Cursor_IsIgnored()
    {
        ToolsListHandler handler = new(Registry(FakeMcpTool.Succeeding("one"), FakeMcpTool.Succeeding("two")));

        JsonElement withoutCursor = await handler.HandleAsync(Json("{}"), CancellationToken.None);
        JsonElement withCursor = await handler.HandleAsync(Json("""{"cursor":"page-2"}"""), CancellationToken.None);

        Assert.IsTrue(JsonElement.DeepEquals(withoutCursor, withCursor), withCursor.GetRawText());
        Assert.AreEqual(2, withCursor.GetProperty("tools").GetArrayLength());
    }

    [TestMethod]
    public async Task HandleAsync_ToolsEquivalentToTheSdkServers_AnswersTheGoldenToolsList()
    {
        JsonElement golden = Json(GoldenToolsResponses.ToolsListResult);
        IMcpTool[] tools = [.. golden.GetProperty("tools").EnumerateArray().Select(EquivalentTool)];

        JsonElement result = await new ToolsListHandler(Registry(tools)).HandleAsync(Json("{}"), CancellationToken.None);

        Assert.AreEqual(5, tools.Length);
        Assert.IsTrue(JsonElement.DeepEquals(golden, result), result.GetRawText());
    }

    private static FakeMcpTool EquivalentTool(JsonElement entry)
    {
        McpToolAnnotations? annotations = entry.TryGetProperty("annotations", out JsonElement hints)
            ? new McpToolAnnotations(ReadHint(hints, "idempotentHint"), ReadHint(hints, "readOnlyHint"))
            : null;
        return new FakeMcpTool(
            entry.GetProperty("name").GetString()!,
            (_, _) => McpToolCallResult.Success("ok"),
            entry.GetProperty("description").GetString()!,
            entry.GetProperty("inputSchema").GetRawText(),
            annotations);
    }

    private static bool? ReadHint(JsonElement hints, string name) => hints.TryGetProperty(name, out JsonElement hint) ? hint.GetBoolean() : null;
}
