using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The MCP tool <c>curl</c> on the hand-written server abstractions: forwards to <see cref="McpCurlTool" /> and
/// returns its result as JSON text, flagged as a tool error when the result is one. It offers the same name,
/// description, input schema and results as <see cref="CurlCommandTool" />.
/// </summary>
/// <param name="tool">The tool's logic.</param>
public sealed class CurlCommandMcpTool(McpCurlTool tool) : IMcpTool
{
    /// <summary>The text of the answer to arguments that do not fit the input schema, as the SDK server gave it.</summary>
    public const string InvalidArgumentsMessage = "An error occurred invoking 'curl'.";

    /// <inheritdoc />
    public string Name => "curl";

    /// <inheritdoc />
    public string Description => CurlCommandTool.ToolDescription;

    /// <inheritdoc />
    public string InputSchemaJson =>
        """
        {
          "type": "object",
          "properties": {
            "args": {
              "description": "curl's command-line arguments, without the program name.",
              "type": "array",
              "items": { "type": "string" }
            },
            "stdin": {
              "description": "The text curl reads as standard input, as UTF-8; omit for none.",
              "type": ["string", "null"],
              "default": null
            }
          },
          "required": ["args"]
        }
        """;

    /// <inheritdoc />
    public McpToolAnnotations? Annotations => null;

    /// <inheritdoc />
    public async ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!TryReadArguments(arguments, out string[] args, out string? stdin))
        {
            return McpToolCallResult.Error(InvalidArgumentsMessage);
        }

        McpCurlToolResult result = await tool.RunAsync(args, stdin, cancellationToken).ConfigureAwait(false);
        string text = JsonSerializer.Serialize(result, CurlCommandMcpToolJson.Default.McpCurlToolResult);
        return result.IsError ? McpToolCallResult.Error(text) : McpToolCallResult.Success(text);
    }

    private static bool TryReadArguments(JsonElement arguments, out string[] args, out string? stdin)
    {
        args = [];
        stdin = null;
        return arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("args", out JsonElement argsElement)
            && TryReadStrings(argsElement, out args)
            && TryReadOptionalString(arguments, "stdin", out stdin);
    }

    private static bool TryReadStrings(JsonElement element, out string[] strings)
    {
        strings = [];
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

        strings = [.. items];
        return true;
    }

    private static bool TryReadOptionalString(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return true;
    }
}
