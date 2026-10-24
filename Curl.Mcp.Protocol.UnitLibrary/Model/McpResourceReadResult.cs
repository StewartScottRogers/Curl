using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>The result of <c>resources/read</c>: the resource's contents.</summary>
public sealed class McpResourceReadResult
{
    /// <summary>Initializes a new instance of the <see cref="McpResourceReadResult" /> class.</summary>
    /// <param name="contents">The contents read.</param>
    public McpResourceReadResult(IReadOnlyList<McpResourceContents> contents)
    {
        Contents = contents;
    }

    /// <summary>Gets the contents read.</summary>
    [JsonPropertyName("contents")]
    public IReadOnlyList<McpResourceContents> Contents { get; }
}
