using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

/// <summary>
/// The MCP tool <c>curl</c>: forwards to <see cref="McpCurlTool" /> and returns its result as JSON text,
/// flagged as a tool error when the result is one (ADR-0548, ADR-0575).
/// </summary>
/// <param name="tool">The tool's logic.</param>
[McpServerToolType]
public sealed class CurlCommandTool(McpCurlTool tool)
{
    /// <summary>The description an agent reads for the <c>curl</c> tool.</summary>
    public const string ToolDescription =
        "Runs curl in-process, exactly as the curl command line would. `args` are curl's arguments without the program name, "
        + "for example [\"-s\", \"https://example.com\"]. Every file curl reads or writes must lie inside the folder CURL_MCP_ROOT names; "
        + "a path outside it is refused before curl runs. Run [\"--ai-help\"] for an index of every option and [\"--ai-help\", \"all\"] "
        + "for the full reference. Returns curl's standard output, standard error and exit code; a nonzero exit is a tool error.";

    /// <summary>Runs curl with <paramref name="args" />.</summary>
    /// <param name="args">curl's arguments without the program name.</param>
    /// <param name="stdin">The text curl reads as standard input; none when omitted.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token.</param>
    /// <returns>The tool result.</returns>
    [McpServerTool(Name = "curl")]
    [Description(ToolDescription)]
    public async Task<CallToolResult> RunCurlAsync(
        [Description("curl's command-line arguments, without the program name.")] string[] args,
        [Description("The text curl reads as standard input, as UTF-8; omit for none.")] string? stdin = null,
        CancellationToken cancellationToken = default)
    {
        McpCurlToolResult result = await tool.RunAsync(args, stdin, cancellationToken).ConfigureAwait(false);
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, CurlMcpJsonContext.Default.McpCurlToolResult) }],
        };
    }
}
