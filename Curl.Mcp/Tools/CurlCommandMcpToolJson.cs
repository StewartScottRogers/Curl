using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Tools;

/// <summary>
/// The source-generated JSON contract of the <c>curl</c> tool's result, set up exactly as
/// <see cref="CurlMcpJsonContext" /> sets it up so the text block is byte-identical to the SDK tool's.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0575: the JSON source generator writes every member; the tests reach them through CurlCommandMcpTool.")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(McpCurlToolResult))]
internal sealed partial class CurlCommandMcpToolJson : JsonSerializerContext;
