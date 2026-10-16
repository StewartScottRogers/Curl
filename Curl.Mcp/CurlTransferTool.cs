using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

/// <summary>
/// The MCP tool <c>transfer</c>: forwards its fields as one <see cref="McpTransferRequest" /> to
/// <see cref="McpTransferTool" /> and returns its result as JSON text, flagged as a tool error when the
/// result is one (ADR-0548, ADR-0593).
/// </summary>
/// <param name="tool">The tool's logic.</param>
[McpServerToolType]
public sealed class CurlTransferTool(McpTransferTool tool)
{
    /// <summary>The description an agent reads for the <c>transfer</c> tool.</summary>
    public const string ToolDescription =
        "Makes one request, described by fields rather than a command line, through Curl's transfer engine, and returns a structured result: "
        + "the status code, the response headers, the body (text, or base64 when it is not UTF-8) and timing and size information. "
        + "Prefer it to the `curl` tool for a single HTTP request whose response you want to read; use `curl` for anything else, "
        + "such as files, other protocols or any curl option this tool has no field for. A failed transfer is a tool error carrying "
        + "libcurl's error code.";

    /// <summary>Runs one transfer.</summary>
    /// <param name="url">The URL.</param>
    /// <param name="method">The request method.</param>
    /// <param name="headers">Extra request headers, in the order sent; none when omitted.</param>
    /// <param name="body">The request body; none when omitted.</param>
    /// <param name="followRedirects">Whether redirects are followed.</param>
    /// <param name="timeoutSeconds">The longest the whole transfer may take, in seconds.</param>
    /// <param name="insecure">Whether the server's certificate goes unverified.</param>
    /// <param name="proxy">The proxy to use.</param>
    /// <param name="userAgent">The <c>User-Agent</c> header.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token.</param>
    /// <returns>The tool result.</returns>
    [McpServerTool(Name = "transfer")]
    [Description(ToolDescription)]
    public async Task<CallToolResult> TransferAsync(
        [Description("The absolute URL to transfer, e.g. https://example.com/; file: URLs are refused.")] string url,
        [Description("The request method, sent as given; GET when omitted.")] string method = "GET",
        [Description("Extra request headers, in the order sent; an empty value sends the header empty.")] McpTransferHeader[]? headers = null,
        [Description("The request body: inline text sent as UTF-8, or base64 when isBase64 is true. Never a file.")] McpTransferBody? body = null,
        [Description("Whether redirects are followed, as curl -L follows them.")] bool followRedirects = false,
        [Description("The longest the whole transfer may take, in seconds; omit or 0 for no limit.")] double? timeoutSeconds = null,
        [Description("Whether the server's certificate goes unverified, as curl -k leaves it.")] bool insecure = false,
        [Description("The proxy to use, as curl -x takes it; the environment's when omitted.")] string? proxy = null,
        [Description("The User-Agent header; none is sent when omitted.")] string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        McpTransferRequest request = new(url)
        {
            Method = method,
            Headers = headers ?? [],
            Body = body,
            FollowRedirects = followRedirects,
            TimeoutSeconds = timeoutSeconds,
            Insecure = insecure,
            Proxy = proxy,
            UserAgent = userAgent,
        };
        McpTransferToolResult result = await tool.RunAsync(request, cancellationToken).ConfigureAwait(false);
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, CurlMcpJsonContext.Default.McpTransferToolResult) }],
        };
    }
}
