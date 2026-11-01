using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The MCP tool <c>build_url</c> on the hand-written server abstractions: forwards to <see cref="McpUrlTool" /> and
/// returns its result as JSON text, flagged as a tool error when the result is one. It offers the same name,
/// description, annotations, input schema and results as the SDK-attributed tool it replaced (BL-3388).
/// </summary>
/// <param name="tool">The tools' logic.</param>
public sealed class BuildUrlMcpTool(McpUrlTool tool) : IMcpTool
{
    /// <summary>The text of the answer to arguments that do not fit the input schema, as the SDK server gave it.</summary>
    public const string InvalidArgumentsMessage = "An error occurred invoking 'build_url'.";

    private static readonly string[] PartNames = ["scheme", "user", "password", "options", "host", "port", "path", "query", "fragment", "zoneId"];

    /// <inheritdoc />
    public string Name => "build_url";

    /// <inheritdoc />
    public string Description =>
        "Builds a URL from its parts with libcurl's URL API: sets only the parts supplied (curl_url_set), in libcurl's order, and "
        + "returns the whole URL (curl_url_get). The schemes of Curl's extended protocols (the version tool's extendedProtocols) build with no flag, as curl's own do. A part or URL the parser refuses, such as a bad port or no host, is a tool error "
        + "naming the part and the CURLUcode.";

    /// <inheritdoc />
    public string InputSchemaJson =>
        $$"""
        {
          "type": "object",
          "properties": {
            "scheme": { "description": "The scheme, e.g. https.", "type": ["string", "null"], "default": null },
            "user": { "description": "The user name.", "type": ["string", "null"], "default": null },
            "password": { "description": "The password.", "type": ["string", "null"], "default": null },
            "options": { "description": "The login options (IMAP, POP3 and SMTP).", "type": ["string", "null"], "default": null },
            "host": { "description": "The host name or address.", "type": ["string", "null"], "default": null },
            "port": { "description": "The port number, as text.", "type": ["string", "null"], "default": null },
            "path": { "description": "The path, e.g. /a/b.", "type": ["string", "null"], "default": null },
            "query": { "description": "The query, without its '?'.", "type": ["string", "null"], "default": null },
            "fragment": { "description": "The fragment, without its '#'.", "type": ["string", "null"], "default": null },
            "zoneId": { "description": "The IPv6 zone identifier.", "type": ["string", "null"], "default": null },
            {{UrlMcpToolArguments.FlagsSchemaProperty}}
          }
        }
        """;

    /// <inheritdoc />
    public McpToolAnnotations? Annotations { get; } = new(idempotentHint: true, readOnlyHint: true);

    /// <inheritdoc />
    public ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!TryReadParts(arguments, out McpUrlParts? parts)
            || !UrlMcpToolArguments.TryReadFlags(arguments, out string[]? flags))
        {
            return ValueTask.FromResult(McpToolCallResult.Error(InvalidArgumentsMessage));
        }

        McpBuildUrlToolResult result = tool.Build(parts, flags);
        string text = JsonSerializer.Serialize(result, UrlMcpToolJson.Default.McpBuildUrlToolResult);
        return ValueTask.FromResult(result.IsError ? McpToolCallResult.Error(text) : McpToolCallResult.Success(text));
    }

    private static bool TryReadParts(JsonElement arguments, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out McpUrlParts? parts)
    {
        parts = null;
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string?[] values = new string?[PartNames.Length];
        for (int index = 0; index < PartNames.Length; index++)
        {
            if (!UrlMcpToolArguments.TryReadOptionalString(arguments, PartNames[index], out values[index]))
            {
                return false;
            }
        }

        parts = new McpUrlParts(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9]);
        return true;
    }
}
