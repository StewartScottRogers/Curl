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
[JsonSerializable(typeof(McpVersionToolResult))]
[JsonSerializable(typeof(McpParseUrlToolResult))]
[JsonSerializable(typeof(McpBuildUrlToolResult))]
internal sealed partial class CurlMcpJsonContext : JsonSerializerContext
{
    /// <summary>
    /// The options the tools are registered with: these contracts first, then the SDK's own protocol types.
    /// Built on first use, never by a static initializer: the generator's <c>Default</c> is set by the static
    /// initializer of its own partial file, which may run after this one's, and
    /// <c>JsonTypeInfoResolver.Combine</c> silently drops a <see langword="null" /> resolver - which left
    /// every parameter type of these contracts unresolved under native AOT (ADR-0621). The build is locked,
    /// so threads reading it first at once all get the one instance (BL-2490).
    /// </summary>
    internal static JsonSerializerOptions ToolOptions => toolOptions.Value;

    /// <summary>The lazily built, thread-safe holder behind <see cref="ToolOptions" />.</summary>
    private static readonly Lazy<JsonSerializerOptions> toolOptions = new(() => new(McpJsonUtilities.DefaultOptions)
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(Default, McpJsonUtilities.DefaultOptions.TypeInfoResolver),
    });
}
