using System.Text.Json;

namespace Curl.Mcp.Protocol.Model;

[TestClass]
public sealed class McpServerRegistryTests
{
    [TestMethod]
    public void Add_Tool_ListsEntryWithParsedSchemaAndFindsToolByName()
    {
        McpServerRegistry registry = new();
        StubTool tool = new("version", """{"type":"object","properties":{}}""", new McpToolAnnotations(true, true));

        registry.Add(tool);

        McpToolListEntry entry = registry.ToolEntries.Single();
        Assert.AreEqual("version", entry.Name);
        Assert.AreEqual("Describes version.", entry.Description);
        Assert.AreEqual("""{"type":"object","properties":{}}""", entry.InputSchema.GetRawText());
        Assert.AreSame(tool.Annotations, entry.Annotations);
        Assert.AreSame(tool, registry.FindTool("version"));
    }

    [TestMethod]
    public void FindTool_UnknownName_ReturnsNull()
    {
        McpServerRegistry registry = new();
        registry.Add(new StubTool("version", """{"type":"object"}""", null));

        Assert.IsNull(registry.FindTool("Version"));
    }

    [TestMethod]
    public void Add_TwoTools_ListsThemInOrderAdded()
    {
        McpServerRegistry registry = new();

        registry.Add(new StubTool("b", """{"type":"object"}""", null));
        registry.Add(new StubTool("a", """{"type":"object"}""", null));

        CollectionAssert.AreEqual(new[] { "b", "a" }, registry.ToolEntries.Select(entry => entry.Name).ToArray());
    }

    [TestMethod]
    public void Add_DuplicateToolName_ThrowsArgumentExceptionAndKeepsFirst()
    {
        McpServerRegistry registry = new();
        StubTool first = new("curl", """{"type":"object"}""", null);
        registry.Add(first);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => registry.Add(new StubTool("curl", """{"type":"object"}""", null)));

        StringAssert.Contains(exception.Message, "'curl' is already registered");
        Assert.AreSame(first, registry.FindTool("curl"));
        Assert.HasCount(1, registry.ToolEntries);
    }

    [TestMethod]
    [DataRow("""not json""", DisplayName = "not JSON")]
    [DataRow("""["object"]""", DisplayName = "array")]
    [DataRow("""{"properties":{}}""", DisplayName = "no type")]
    [DataRow("""{"type":["object"]}""", DisplayName = "type not a string")]
    [DataRow("""{"type":"array"}""", DisplayName = "type array")]
    public void Add_ToolSchemaNotAnObjectTypeObject_ThrowsArgumentException(string schema)
    {
        McpServerRegistry registry = new();

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => registry.Add(new StubTool("bad", schema, null)));

        StringAssert.Contains(exception.Message, "input schema of tool 'bad'");
        Assert.IsEmpty(registry.ToolEntries);
        Assert.IsNull(registry.FindTool("bad"));
    }

    [TestMethod]
    public void Add_NullTool_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpServerRegistry().Add((IMcpTool)null!));
    }

    [TestMethod]
    public void Add_ResourceProvider_ListsResourcesAndFindsProviderByUri()
    {
        McpServerRegistry registry = new();
        StubProvider provider = new("curl://a", "curl://b");

        registry.Add(provider);

        CollectionAssert.AreEqual(new[] { "curl://a", "curl://b" }, registry.Resources.Select(resource => resource.Uri).ToArray());
        Assert.AreSame(provider, registry.FindResourceProvider("curl://b"));
        Assert.IsNull(registry.FindResourceProvider("curl://c"));
    }

    [TestMethod]
    public void Add_ProviderWithUriAlreadyRegistered_ThrowsAndAddsNoneOfItsResources()
    {
        McpServerRegistry registry = new();
        StubProvider first = new("curl://a");
        registry.Add(first);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => registry.Add(new StubProvider("curl://b", "curl://a")));

        StringAssert.Contains(exception.Message, "'curl://a' is already registered");
        Assert.HasCount(1, registry.Resources);
        Assert.IsNull(registry.FindResourceProvider("curl://b"));
        Assert.AreSame(first, registry.FindResourceProvider("curl://a"));
    }

    [TestMethod]
    public void Add_ProviderListingOneUriTwice_ThrowsAndAddsNothing()
    {
        McpServerRegistry registry = new();

        Assert.ThrowsExactly<ArgumentException>(() => registry.Add(new StubProvider("curl://a", "curl://a")));

        Assert.IsEmpty(registry.Resources);
        Assert.IsNull(registry.FindResourceProvider("curl://a"));
    }

    [TestMethod]
    public void Add_NullProvider_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new McpServerRegistry().Add((IMcpResourceProvider)null!));
    }

    private sealed class StubTool(string name, string inputSchemaJson, McpToolAnnotations? annotations) : IMcpTool
    {
        public string Name => name;

        public string Description => $"Describes {name}.";

        public string InputSchemaJson => inputSchemaJson;

        public McpToolAnnotations? Annotations => annotations;

        public ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken) =>
            ValueTask.FromResult(McpToolCallResult.Success(arguments.GetRawText()));
    }

    private sealed class StubProvider(params string[] uris) : IMcpResourceProvider
    {
        public IReadOnlyList<McpResource> ListResources() => [.. uris.Select(uri => new McpResource(uri, uri, null, "text/plain"))];

        public ValueTask<McpResourceContents?> ReadAsync(string uri, CancellationToken cancellationToken) =>
            ValueTask.FromResult<McpResourceContents?>(new McpResourceContents(uri, "text/plain", uri));
    }
}
