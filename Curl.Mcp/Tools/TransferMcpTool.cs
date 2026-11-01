using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The <c>transfer</c> tool on the hand-written server abstractions: the same name, description, input schema and
/// results as the SDK-attributed tool it replaced (BL-3388), reading its <c>arguments</c> itself. A wrong argument shape is refused
/// first with <see cref="McpTransferRequestCheck.CheckArgumentShapes" />, as the SDK's call-tool filter refused it
/// (ADR-0771); an omitted or <c>null</c> field takes the default the SDK method's signature gave it.
/// </summary>
/// <param name="tool">Runs the transfer the arguments describe.</param>
public sealed class TransferMcpTool(McpTransferTool tool) : IMcpTool
{
    /// <inheritdoc />
    public string Name => "transfer";

    /// <inheritdoc />
    public string Description =>
        "Makes one request, described by fields rather than a command line, through Curl's transfer engine, and returns a structured result: "
        + "the status code, the response headers, the body (text, or base64 when it is not UTF-8) and timing and size information. "
        + "Prefer it to the `curl` tool for a single HTTP request whose response you want to read; use `curl` for anything else, "
        + "such as files, other protocols or any curl option this tool has no field for. A URL of one of Curl's extended protocols (the version tool's extendedProtocols) runs through its extension, as curl's own schemes run through theirs. A failed transfer is a tool error carrying "
        + "libcurl's error code.";

    /// <inheritdoc />
    public string InputSchemaJson => """
        {
          "type": "object",
          "properties": {
            "url": { "description": "The absolute URL to transfer, e.g. https://example.com/; file: URLs are refused.", "type": "string" },
            "method": { "description": "The request method, sent as given; GET when omitted.", "type": "string", "default": "GET" },
            "headers": {
              "description": "Extra request headers, in the order sent; an empty value sends the header empty.",
              "type": ["array", "null"],
              "items": {
                "type": ["object", "null"],
                "properties": { "name": { "type": ["string", "null"] }, "value": { "type": ["string", "null"] } },
                "required": ["name", "value"]
              },
              "default": null
            },
            "body": {
              "description": "The request body: inline text sent as UTF-8, or base64 when isBase64 is true. Never a file.",
              "type": ["object", "null"],
              "properties": { "content": { "type": ["string", "null"] }, "isBase64": { "type": "boolean" } },
              "required": ["content", "isBase64"],
              "default": null
            },
            "followRedirects": { "description": "Whether redirects are followed, as curl -L follows them.", "type": "boolean", "default": false },
            "timeoutSeconds": { "description": "The longest the whole transfer may take, in seconds; omit or 0 for no limit.", "type": ["number", "null"], "default": null },
            "insecure": { "description": "Whether the server's certificate goes unverified, as curl -k leaves it.", "type": "boolean", "default": false },
            "proxy": { "description": "The proxy to use, as curl -x takes it; the environment's when omitted.", "type": ["string", "null"], "default": null },
            "userAgent": { "description": "The User-Agent header; none is sent when omitted.", "type": ["string", "null"], "default": null }
          },
          "required": ["url"]
        }
        """;

    /// <inheritdoc />
    public McpToolAnnotations? Annotations => null;

    /// <inheritdoc />
    public async ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        Dictionary<string, JsonElement> fields = ReadFields(arguments);
        McpTransferToolResult result = McpTransferRequestCheck.CheckArgumentShapes(fields) is McpToolFailure failure
            ? McpTransferToolResult.Failed(failure)
            : await tool.RunAsync(ReadRequest(fields), cancellationToken).ConfigureAwait(false);
        return new McpToolCallResult(
            [new McpTextContent(JsonSerializer.Serialize(result, TransferMcpToolJson.Default.McpTransferToolResult))],
            result.IsError);
    }

    private static Dictionary<string, JsonElement> ReadFields(JsonElement arguments)
    {
        Dictionary<string, JsonElement> fields = new(StringComparer.Ordinal);
        if (arguments.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in arguments.EnumerateObject())
            {
                fields[property.Name] = property.Value;
            }
        }

        return fields;
    }

    private static McpTransferRequest ReadRequest(Dictionary<string, JsonElement> fields) =>
        new(Text(fields, "url") ?? string.Empty)
        {
            Method = Text(fields, "method") ?? "GET",
            Headers = Headers(fields),
            Body = Body(fields),
            FollowRedirects = Flag(fields, "followRedirects"),
            TimeoutSeconds = Number(fields, "timeoutSeconds"),
            Insecure = Flag(fields, "insecure"),
            Proxy = Text(fields, "proxy"),
            UserAgent = Text(fields, "userAgent"),
        };

    private static string? Text(Dictionary<string, JsonElement> fields, string name) =>
        Field(fields, name) is JsonElement value ? value.GetString() : null;

    private static bool Flag(Dictionary<string, JsonElement> fields, string name) =>
        Field(fields, name) is JsonElement value && value.GetBoolean();

    private static double? Number(Dictionary<string, JsonElement> fields, string name) =>
        Field(fields, name) is JsonElement value ? value.GetDouble() : null;

    private static McpTransferHeader[] Headers(Dictionary<string, JsonElement> fields) =>
        Field(fields, "headers") is JsonElement value ? value.Deserialize(TransferMcpToolJson.Default.McpTransferHeaderArray)! : [];

    private static McpTransferBody? Body(Dictionary<string, JsonElement> fields) =>
        Field(fields, "body") is JsonElement value ? value.Deserialize(TransferMcpToolJson.Default.McpTransferBody) : null;

    private static JsonElement? Field(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out JsonElement value) && value.ValueKind != JsonValueKind.Null ? value : null;
}
