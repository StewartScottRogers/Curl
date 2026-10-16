using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;

namespace Curl.Mcp;

/// <summary>
/// The source-generated JSON contracts of the tools' parameters and results, so no tool serializes by
/// reflection (ADR-0548).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0575: the JSON source generator writes every member but ToolOptions; the tests reach them through CurlCommandTool and the tool schema.")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(McpCurlToolResult))]
[JsonSerializable(typeof(McpTransferHeader[]))]
[JsonSerializable(typeof(McpTransferBody))]
[JsonSerializable(typeof(McpTransferToolResult))]
internal sealed partial class CurlMcpJsonContext : JsonSerializerContext
{
    /// <summary>The options the tools are registered with: these contracts first, then the SDK's own protocol types.</summary>
    internal static JsonSerializerOptions ToolOptions { get; } = new(McpJsonUtilities.DefaultOptions)
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(Default, McpJsonUtilities.DefaultOptions.TypeInfoResolver),
    };
}
