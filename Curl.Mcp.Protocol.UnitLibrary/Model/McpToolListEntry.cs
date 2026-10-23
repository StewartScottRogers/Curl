using System.Text.Json;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>One entry of the <c>tools</c> array of a <c>tools/list</c> result.</summary>
public sealed class McpToolListEntry
{
    /// <summary>Initializes a new instance of the <see cref="McpToolListEntry" /> class.</summary>
    /// <param name="name">The tool's name.</param>
    /// <param name="description">The tool's description.</param>
    /// <param name="inputSchema">The tool's parsed input schema.</param>
    /// <param name="annotations">The tool's hints, or <see langword="null" /> for none.</param>
    public McpToolListEntry(string name, string description, JsonElement inputSchema, McpToolAnnotations? annotations)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        Annotations = annotations;
    }

    /// <summary>Gets the tool's name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>Gets the tool's description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; }

    /// <summary>Gets the tool's input schema, a JSON object with <c>"type": "object"</c>.</summary>
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; }

    /// <summary>Gets the tool's hints; not written when <see langword="null" />.</summary>
    [JsonPropertyName("annotations")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpToolAnnotations? Annotations { get; }
}
