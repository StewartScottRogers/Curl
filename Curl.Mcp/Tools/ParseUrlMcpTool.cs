using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The MCP tool <c>parse_url</c> on the hand-written server abstractions: forwards to <see cref="McpUrlTool" /> and
/// returns its result as JSON text, flagged as a tool error when the result is one. It offers the same name,
/// description, annotations, input schema and results as the SDK-attributed tool it replaced (BL-3388).
/// </summary>
/// <param name="tool">The tools' logic.</param>
public sealed class ParseUrlMcpTool(McpUrlTool tool) : IMcpTool
{
    /// <summary>The text of the answer to arguments that do not fit the input schema, as the SDK server gave it.</summary>
    public const string InvalidArgumentsMessage = "An error occurred invoking 'parse_url'.";

    /// <inheritdoc />
    public string Name => "parse_url";

    /// <inheritdoc />
    public string Description =>
        "Parses a URL with libcurl's URL API (curl_url_set and curl_url_get) and returns the URL as the parser rebuilt it and its "
        + "parts: scheme, user, password, options, host, port, path, query, fragment and zoneId. A part the URL does not have is "
        + "null, never an empty string. The schemes of Curl's extended protocols (the version tool's extendedProtocols) parse with no flag, as curl's own do. A URL the parser refuses is a tool error naming the CURLUcode.";

    /// <inheritdoc />
    public string InputSchemaJson =>
        $$"""
        {
          "type": "object",
          "properties": {
            "url": {
              "description": "The URL to parse, e.g. https://user:pw@example.com:8443/a?x=1#f.",
              "type": "string"
            },
            {{UrlMcpToolArguments.FlagsSchemaProperty}}
          },
          "required": ["url"]
        }
        """;

    /// <inheritdoc />
    public McpToolAnnotations? Annotations { get; } = new(idempotentHint: true, readOnlyHint: true);

    /// <inheritdoc />
    public ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("url", out JsonElement url)
            || url.ValueKind != JsonValueKind.String
            || !UrlMcpToolArguments.TryReadFlags(arguments, out string[]? flags))
        {
            return ValueTask.FromResult(McpToolCallResult.Error(InvalidArgumentsMessage));
        }

        McpParseUrlToolResult result = tool.Parse(url.GetString()!, flags);
        string text = JsonSerializer.Serialize(result, UrlMcpToolJson.Default.McpParseUrlToolResult);
        return ValueTask.FromResult(result.IsError ? McpToolCallResult.Error(text) : McpToolCallResult.Success(text));
    }
}
