using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.Model;

/// <summary>
/// The source-generated JSON contracts of the tool and resource wire shapes, so none is serialized by reflection.
/// Used on its own, never combined with another resolver (ADR-0621).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0575: the JSON source generator writes these members; the Model serialization tests reach them.")]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(McpToolListResult))]
[JsonSerializable(typeof(McpToolCallResult))]
[JsonSerializable(typeof(McpResourceListResult))]
[JsonSerializable(typeof(McpResourceReadResult))]
internal sealed partial class McpModelJsonContext : JsonSerializerContext
{
}
