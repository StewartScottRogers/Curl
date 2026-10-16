using Curl.Cli;

namespace Curl.Mcp;

/// <summary>
/// One MCP resource that serves a subject of <c>curl --ai-help</c> as Markdown (ADR-0548, ADR-0589).
/// </summary>
/// <param name="Uri">The resource's URI: <c>curl://ai-help</c>, <c>curl://ai-help/all</c> or <c>curl://ai-help/&lt;category&gt;</c>.</param>
/// <param name="Name">The resource's name, unique in the catalogue.</param>
/// <param name="Description">What the resource holds, for an agent choosing one.</param>
/// <param name="Subject">The <c>--ai-help</c> subject it serves: empty for the index, <c>all</c> or a category name.</param>
public sealed record McpAiHelpResource(string Uri, string Name, string Description, string Subject)
{
    /// <summary>The MIME type of every <c>--ai-help</c> resource.</summary>
    public const string MimeType = "text/markdown";

    /// <summary>Reads the resource: what <c>curl --ai-help</c> prints for <see cref="Subject" />.</summary>
    /// <returns>The Markdown, every line ending in <c>\n</c>.</returns>
    public string ReadMarkdown()
    {
        _ = CurlAiHelpText.TryGetMarkdown(Subject, out string markdown);
        return markdown;
    }
}
