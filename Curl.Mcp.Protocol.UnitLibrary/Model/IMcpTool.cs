using System.Text.Json;

namespace Curl.Mcp.Protocol.Model;

/// <summary>
/// One tool the MCP server offers: what <c>tools/list</c> describes and what <c>tools/call</c> runs. The host adds
/// each tool to <see cref="McpServerRegistry" />; nothing is found by reflection.
/// </summary>
public interface IMcpTool
{
    /// <summary>Gets the tool's name, unique within the server, e.g. <c>parse_url</c>.</summary>
    string Name { get; }

    /// <summary>Gets the tool's description as <c>tools/list</c> shows it.</summary>
    string Description { get; }

    /// <summary>
    /// Gets the tool's input schema as raw JSON text: a JSON object with <c>"type": "object"</c>, written by hand,
    /// parsed once when the tool is registered.
    /// </summary>
    string InputSchemaJson { get; }

    /// <summary>Gets the hints that describe the tool's behaviour, or <see langword="null" /> for none.</summary>
    McpToolAnnotations? Annotations { get; }

    /// <summary>Runs the tool.</summary>
    /// <param name="arguments">The <c>arguments</c> object of the <c>tools/call</c> request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The result, with <see cref="McpToolCallResult.IsError" /> set when the tool failed.</returns>
    ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken);
}
