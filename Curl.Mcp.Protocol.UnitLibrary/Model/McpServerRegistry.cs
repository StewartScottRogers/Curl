using System.Text.Json;

namespace Curl.Mcp.Protocol.Model;

/// <summary>
/// The tools and resource providers the MCP server offers, filled by the host with one <c>Add</c> per tool or
/// provider. Tool names and resource URIs are unique; nothing is found by reflection.
/// </summary>
public sealed class McpServerRegistry
{
    private readonly List<McpToolListEntry> toolEntries = [];
    private readonly Dictionary<string, IMcpTool> toolsByName = new(StringComparer.Ordinal);
    private readonly List<McpResource> resources = [];
    private readonly Dictionary<string, IMcpResourceProvider> providersByUri = new(StringComparer.Ordinal);

    /// <summary>Gets the <c>tools/list</c> entries of the registered tools, in the order they were added.</summary>
    public IReadOnlyList<McpToolListEntry> ToolEntries => toolEntries;

    /// <summary>Gets the resources of the registered providers, in the order they were added.</summary>
    public IReadOnlyList<McpResource> Resources => resources;

    /// <summary>Adds a tool, parsing its input schema once.</summary>
    /// <param name="tool">The tool.</param>
    /// <exception cref="ArgumentException">
    /// The tool's <see cref="IMcpTool.InputSchemaJson" /> is not a JSON object whose <c>type</c> is the string
    /// <c>object</c>, or a tool with the same name is already registered.
    /// </exception>
    public void Add(IMcpTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        JsonElement schema = ParseInputSchema(tool);
        if (!toolsByName.TryAdd(tool.Name, tool))
        {
            throw new ArgumentException($"A tool named '{tool.Name}' is already registered.", nameof(tool));
        }

        toolEntries.Add(new McpToolListEntry(tool.Name, tool.Description, schema, tool.Annotations));
    }

    /// <summary>Adds a resource provider, reading its resource list once.</summary>
    /// <param name="provider">The provider.</param>
    /// <exception cref="ArgumentException">
    /// One of the provider's resources has a URI already registered; none of its resources is then added.
    /// </exception>
    public void Add(IMcpResourceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        IReadOnlyList<McpResource> listed = provider.ListResources();
        HashSet<string> uris = new(StringComparer.Ordinal);
        foreach (McpResource resource in listed)
        {
            if (providersByUri.ContainsKey(resource.Uri) || !uris.Add(resource.Uri))
            {
                throw new ArgumentException($"A resource with URI '{resource.Uri}' is already registered.", nameof(provider));
            }
        }

        foreach (McpResource resource in listed)
        {
            providersByUri.Add(resource.Uri, provider);
            resources.Add(resource);
        }
    }

    /// <summary>Finds a registered tool by name.</summary>
    /// <param name="name">The tool's name, compared ordinally.</param>
    /// <returns>The tool, or <see langword="null" /> when no tool has that name.</returns>
    public IMcpTool? FindTool(string name) => toolsByName.GetValueOrDefault(name);

    /// <summary>Finds the provider of a registered resource by URI.</summary>
    /// <param name="uri">The resource's URI, compared ordinally.</param>
    /// <returns>The provider, or <see langword="null" /> when no resource has that URI.</returns>
    public IMcpResourceProvider? FindResourceProvider(string uri) => providersByUri.GetValueOrDefault(uri);

    private static JsonElement ParseInputSchema(IMcpTool tool)
    {
        JsonElement schema;
        try
        {
            using JsonDocument document = JsonDocument.Parse(tool.InputSchemaJson);
            schema = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"The input schema of tool '{tool.Name}' is not valid JSON.", nameof(tool), exception);
        }

        if (schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("type", out JsonElement type)
            || type.ValueKind != JsonValueKind.String
            || type.GetString() != "object")
        {
            throw new ArgumentException($"The input schema of tool '{tool.Name}' is not a JSON object with \"type\": \"object\".", nameof(tool));
        }

        return schema;
    }
}
