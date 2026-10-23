using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Tools;

/// <summary>
/// The source-generated JSON for <see cref="TransferMcpTool" />: the <c>headers</c> and <c>body</c> arguments it reads
/// and the <see cref="McpTransferToolResult" /> it answers, in the same camel case and string enums as
/// <see cref="CurlMcpJsonContext" />, so the text is byte for byte the SDK tool's. No reflection serialization (ADR-0621).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0575: the JSON source generator writes every member; the tests reach them through TransferMcpTool.")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(McpTransferHeader[]))]
[JsonSerializable(typeof(McpTransferBody))]
[JsonSerializable(typeof(McpTransferToolResult))]
internal sealed partial class TransferMcpToolJson : JsonSerializerContext;
