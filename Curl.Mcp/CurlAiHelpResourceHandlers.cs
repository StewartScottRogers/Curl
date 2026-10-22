using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Curl.Mcp;

/// <summary>
/// The MCP <c>resources/list</c> and <c>resources/read</c> handlers over <see cref="McpAiHelpResourceCatalogue" />:
/// curl's <c>--ai-help</c> Markdown as resources (ADR-0548, ADR-0589).
/// </summary>
internal static class CurlAiHelpResourceHandlers
{
    /// <summary>Lists every resource of the catalogue.</summary>
    /// <param name="request">The <c>resources/list</c> request; its cursor is ignored, as the list fits one page.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token.</param>
    /// <returns>The resources, each with its URI, name, description and MIME type.</returns>
    internal static ValueTask<ListResourcesResult> ListAsync(RequestContext<ListResourcesRequestParams> request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ListResourcesResult
        {
            Resources = [.. McpAiHelpResourceCatalogue.Resources.Select(resource => new Resource
            {
                Uri = resource.Uri,
                Name = resource.Name,
                Description = resource.Description,
                MimeType = McpAiHelpResource.MimeType,
            })],
        });

    /// <summary>Reads the resource the request names.</summary>
    /// <param name="request">The <c>resources/read</c> request.</param>
    /// <param name="cancellationToken">The MCP request's cancellation token.</param>
    /// <returns>The resource's Markdown as one text content.</returns>
    /// <exception cref="McpProtocolException">No resource has the URI; its code is <see cref="McpErrorCode.ResourceNotFound" />.</exception>
    internal static ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken)
    {
        // A request without parameters names no resource, so it falls through to the not-found error.
        string uri = request.Params?.Uri ?? string.Empty;
        McpAiHelpResource resource = McpAiHelpResourceCatalogue.Find(uri)
            ?? throw new McpProtocolException($"Resource not found: '{uri}'.", McpErrorCode.ResourceNotFound);
        return ValueTask.FromResult(new ReadResourceResult
        {
            Contents = [new TextResourceContents { Uri = resource.Uri, MimeType = McpAiHelpResource.MimeType, Text = resource.ReadMarkdown() }],
        });
    }
}
