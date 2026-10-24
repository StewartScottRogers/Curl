using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Protocol.JsonRpc;

/// <summary>
/// The source-generated JSON contracts of the JSON-RPC 2.0 responses, so no response is serialized by
/// reflection. Used on its own, never combined with another resolver (ADR-0621).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0575: the JSON source generator writes these members; the tests reach them through JsonRpcResponseWriter.")]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(JsonRpcSuccessResponse))]
[JsonSerializable(typeof(JsonRpcErrorResponse))]
internal sealed partial class JsonRpcJsonContext : JsonSerializerContext
{
}
