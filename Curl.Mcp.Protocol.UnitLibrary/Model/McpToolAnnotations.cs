using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>The <c>annotations</c> of a tool in <c>tools/list</c>: hints a client may use, each written only when set.</summary>
public sealed class McpToolAnnotations
{
    /// <summary>Initializes a new instance of the <see cref="McpToolAnnotations" /> class.</summary>
    /// <param name="idempotentHint">Whether calling the tool again with the same arguments has no further effect, or <see langword="null" /> to leave it unsaid.</param>
    /// <param name="readOnlyHint">Whether the tool leaves its environment unchanged, or <see langword="null" /> to leave it unsaid.</param>
    public McpToolAnnotations(bool? idempotentHint, bool? readOnlyHint)
    {
        IdempotentHint = idempotentHint;
        ReadOnlyHint = readOnlyHint;
    }

    /// <summary>Gets whether calling the tool again with the same arguments has no further effect; not written when <see langword="null" />.</summary>
    [JsonPropertyName("idempotentHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IdempotentHint { get; }

    /// <summary>Gets whether the tool leaves its environment unchanged; not written when <see langword="null" />.</summary>
    [JsonPropertyName("readOnlyHint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ReadOnlyHint { get; }
}
