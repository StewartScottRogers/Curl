using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The MCP resources of curl's <c>--ai-help</c> Markdown on the hand-written server abstractions: lists and reads
/// <see cref="McpAiHelpResourceCatalogue" /> with the same entries and contents as <see cref="CurlAiHelpResourceHandlers" />.
/// </summary>
public sealed class AiHelpMcpResourceProvider : IMcpResourceProvider
{
    /// <inheritdoc />
    public IReadOnlyList<McpResource> ListResources() =>
        [.. McpAiHelpResourceCatalogue.Resources.Select(resource =>
            new McpResource(resource.Name, resource.Uri, resource.Description, McpAiHelpResource.MimeType))];

    /// <inheritdoc />
    public ValueTask<McpResourceContents?> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        McpAiHelpResource? resource = McpAiHelpResourceCatalogue.Find(uri);
        return ValueTask.FromResult<McpResourceContents?>(
            resource is null ? null : new McpResourceContents(resource.Uri, McpAiHelpResource.MimeType, resource.ReadMarkdown()));
    }
}
