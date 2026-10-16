using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

/// <summary>
/// The MCP tool <c>version</c>: returns what <see cref="McpVersionTool" /> reports as JSON text
/// (ADR-0548).
/// </summary>
/// <param name="tool">The tool's logic.</param>
[McpServerToolType]
public sealed class CurlVersionTool(McpVersionTool tool)
{
    /// <summary>The description an agent reads for the <c>version</c> tool.</summary>
    public const string ToolDescription =
        "Reports what `curl -V` reports about Curl's transfer engine: the version, the protocols it supports, the features it has "
        + "built in and its TLS backend, each as its own field, and `curl -V`'s first line as `versionLine`. Takes no arguments.";

    /// <summary>Reports the version.</summary>
    /// <returns>The tool result.</returns>
    [McpServerTool(Name = "version", ReadOnly = true, Idempotent = true)]
    [Description(ToolDescription)]
    public CallToolResult Version() =>
        new()
        {
            IsError = false,
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(tool.Report(), CurlMcpJsonContext.Default.McpVersionToolResult) }],
        };
}
