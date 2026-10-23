using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>The result of <c>tools/list</c>: every registered tool.</summary>
public sealed class McpToolListResult
{
    /// <summary>Initializes a new instance of the <see cref="McpToolListResult" /> class.</summary>
    /// <param name="tools">The tools, in the order they were registered.</param>
    public McpToolListResult(IReadOnlyList<McpToolListEntry> tools)
    {
        Tools = tools;
    }

    /// <summary>Gets the tools, in the order they were registered.</summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<McpToolListEntry> Tools { get; }
}
