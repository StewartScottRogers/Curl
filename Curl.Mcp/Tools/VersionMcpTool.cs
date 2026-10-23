using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The <c>version</c> tool on the hand-written server abstractions: the same name, description, annotations and
/// result as <see cref="CurlVersionTool" />. It takes no arguments, and any supplied are ignored.
/// </summary>
/// <param name="tool">The tool's logic.</param>
public sealed class VersionMcpTool(McpVersionTool tool) : IMcpTool
{
    /// <inheritdoc />
    public string Name => "version";

    /// <inheritdoc />
    public string Description => CurlVersionTool.ToolDescription;

    /// <inheritdoc />
    public string InputSchemaJson => """{"type":"object","properties":{}}""";

    /// <inheritdoc />
    public McpToolAnnotations? Annotations { get; } = new(idempotentHint: true, readOnlyHint: true);

    /// <inheritdoc />
    public ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        ValueTask.FromResult(McpToolCallResult.Success(JsonSerializer.Serialize(tool.Report(), VersionMcpToolJson.Default.McpVersionToolResult)));
}
