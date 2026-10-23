using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Tools;

/// <summary>
/// The source-generated JSON for <see cref="VersionMcpTool" />: the <see cref="McpVersionToolResult" /> it answers, in the
/// same camel case and string enums as the SDK-era JSON context, so the text is byte for byte the SDK tool's.
/// No reflection serialization (ADR-0621).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0575: the JSON source generator writes every member; the tests reach them through VersionMcpTool.")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(McpVersionToolResult))]
internal sealed partial class VersionMcpToolJson : JsonSerializerContext;
