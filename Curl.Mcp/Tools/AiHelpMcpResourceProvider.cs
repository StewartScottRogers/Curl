using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Tools;

/// <summary>
/// The MCP resources of curl's <c>--ai-help</c> Markdown on the hand-written server abstractions: lists and reads
/// <see cref="McpAiHelpResourceCatalogue.ResourcesWithExtensions" /> with the same entries and contents the SDK-era
/// handlers served (BL-3388), and the extensions resource the host's <c>--ai-help extensions</c> answers (BL-4450).
/// </summary>
/// <param name="readExtensionsMarkdown">Reads what <c>curl --ai-help extensions</c> prints in the host's environment.</param>
public sealed class AiHelpMcpResourceProvider(Func<string> readExtensionsMarkdown) : IMcpResourceProvider
{
    private readonly IReadOnlyList<McpAiHelpResource> _resources = McpAiHelpResourceCatalogue.ResourcesWithExtensions(readExtensionsMarkdown);

    /// <inheritdoc />
    public IReadOnlyList<McpResource> ListResources() =>
        [.. _resources.Select(resource =>
            new McpResource(resource.Name, resource.Uri, resource.Description, McpAiHelpResource.MimeType))];

    /// <inheritdoc />
    public ValueTask<McpResourceContents?> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        McpAiHelpResource? resource = McpAiHelpResourceCatalogue.Find(_resources, uri);
        return ValueTask.FromResult<McpResourceContents?>(
            resource is null ? null : new McpResourceContents(resource.Uri, McpAiHelpResource.MimeType, resource.ReadMarkdown()));
    }
}
