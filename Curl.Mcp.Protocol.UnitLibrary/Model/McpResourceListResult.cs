using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>The result of <c>resources/list</c>: every registered resource.</summary>
public sealed class McpResourceListResult
{
    /// <summary>Initializes a new instance of the <see cref="McpResourceListResult" /> class.</summary>
    /// <param name="resources">The resources, in the order they were registered.</param>
    public McpResourceListResult(IReadOnlyList<McpResource> resources)
    {
        Resources = resources;
    }

    /// <summary>Gets the resources, in the order they were registered.</summary>
    [JsonPropertyName("resources")]
    public IReadOnlyList<McpResource> Resources { get; }
}
