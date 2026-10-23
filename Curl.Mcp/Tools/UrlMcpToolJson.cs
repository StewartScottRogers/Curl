using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Curl.Mcp.Tools;

/// <summary>
/// The source-generated JSON contract of the <c>parse_url</c> and <c>build_url</c> tools' results, set up exactly as
/// <see cref="CurlMcpJsonContext" /> sets it up so the text blocks are byte-identical to the SDK tools'.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0575: the JSON source generator writes every member; the tests reach them through ParseUrlMcpTool and BuildUrlMcpTool.")]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(McpParseUrlToolResult))]
[JsonSerializable(typeof(McpBuildUrlToolResult))]
internal sealed partial class UrlMcpToolJson : JsonSerializerContext;

/// <summary>The arguments the two URL tools read from a <see cref="JsonElement" />, and the schema text they share.</summary>
internal static class UrlMcpToolArguments
{
    /// <summary>The input-schema property of <c>flags</c>, the same for both tools.</summary>
    public const string FlagsSchemaProperty =
        """
        "flags": {
              "description": "libcurl URL flags by name (CURLU_* without the prefix, in CamelCase), e.g. NonSupportScheme, DefaultPort, UrlDecode, GuessScheme, PathAsIs; none when omitted.",
              "type": ["array", "null"],
              "items": { "type": ["string", "null"] },
              "default": null
            }
        """;

    /// <summary>Reads an optional string property: absent or null is <see langword="null" />, any other type fails.</summary>
    public static bool TryReadOptionalString(JsonElement arguments, string name, out string? value)
    {
        value = null;
        if (!arguments.TryGetProperty(name, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        value = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        return value is not null;
    }

    /// <summary>Reads the optional <c>flags</c> array: absent or null is <see langword="null" />, a non-array or a non-string item fails.</summary>
    public static bool TryReadFlags(JsonElement arguments, out string[]? flags)
    {
        flags = null;
        if (!arguments.TryGetProperty("flags", out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        List<string> items = [];
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            items.Add(item.GetString()!);
        }

        flags = [.. items];
        return true;
    }
}
