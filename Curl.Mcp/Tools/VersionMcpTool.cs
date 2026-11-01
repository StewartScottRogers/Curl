using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The <c>version</c> tool on the hand-written server abstractions: the same name, description, annotations and
/// result as the SDK-attributed tool it replaced (BL-3388). It takes no arguments, and any supplied are ignored.
/// </summary>
/// <param name="tool">The tool's logic.</param>
public sealed class VersionMcpTool(McpVersionTool tool) : IMcpTool
{
    /// <inheritdoc />
    public string Name => "version";

    /// <inheritdoc />
    public string Description =>
        "Reports what `curl -V` reports about Curl's transfer engine: the version, the protocols it supports, the features it has "
        + "built in and its TLS backend, each as its own field, the schemes of Curl's extended protocols as `extendedProtocols` (empty when CURL_EXTENSIONS is 0), and `curl -V`'s first line as `versionLine`. Takes no arguments.";

    /// <inheritdoc />
    public string InputSchemaJson => """{"type":"object","properties":{}}""";

    /// <inheritdoc />
    public McpToolAnnotations? Annotations { get; } = new(idempotentHint: true, readOnlyHint: true);

    /// <inheritdoc />
    public ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        ValueTask.FromResult(McpToolCallResult.Success(JsonSerializer.Serialize(tool.Report(), VersionMcpToolJson.Default.McpVersionToolResult)));
}
