using Curl.Cli;

namespace Curl.Mcp;

/// <summary>
/// The MCP resources that serve <c>curl --ai-help</c>: the index at <c>curl://ai-help</c>, the full reference at
/// <c>curl://ai-help/all</c>, one resource per category of <see cref="CurlHelpText.Categories" /> at
/// <c>curl://ai-help/&lt;category&gt;</c>, and the extensions at <c>curl://ai-help/extensions</c>, whose Markdown
/// the host supplies. The list is built from the category table, so a new category appears without an edit here
/// (ADR-0548, ADR-0589, BL-4450).
/// </summary>
public static class McpAiHelpResourceCatalogue
{
    /// <summary>The URI of the index; every other resource's URI is this, a slash and its subject.</summary>
    public const string IndexUri = "curl://ai-help";

    /// <summary>The <c>--ai-help</c> subject of Curl's extensions.</summary>
    public const string ExtensionsSubject = "extensions";

    /// <summary>The URI of the extensions resource.</summary>
    public const string ExtensionsUri = IndexUri + "/" + ExtensionsSubject;

    private const string AllSubject = "all";

    /// <summary>
    /// The resources <see cref="CurlAiHelpText" /> answers, the index first, then <c>all</c>, then the categories in
    /// <c>--help category</c> order.
    /// </summary>
    public static IReadOnlyList<McpAiHelpResource> Resources { get; } =
    [
        CurlAiHelpResource(IndexUri, "ai-help", "Index of curl's options for AI agents: the categories and the most-used options (curl --ai-help).", string.Empty),
        CurlAiHelpResource($"{IndexUri}/{AllSubject}", "ai-help-all", "Every curl option by category, then the exit codes and the --write-out variables (curl --ai-help all).", AllSubject),
        .. CurlHelpText.Categories.Select(category => CurlAiHelpResource(
            $"{IndexUri}/{category.Name}",
            $"ai-help-{category.Name}",
            $"{category.Description}: every option in curl's {category.Name} category (curl --ai-help {category.Name}).",
            category.Name)),
    ];

    /// <summary>
    /// <see cref="Resources" /> followed by the extensions resource, which reads <paramref name="readExtensionsMarkdown" />.
    /// </summary>
    /// <param name="readExtensionsMarkdown">Reads what <c>curl --ai-help extensions</c> prints in the host's environment.</param>
    /// <returns>Every resource the MCP server serves, in <c>resources/list</c> order.</returns>
    public static IReadOnlyList<McpAiHelpResource> ResourcesWithExtensions(Func<string> readExtensionsMarkdown) =>
    [
        .. Resources,
        new(ExtensionsUri, "ai-help-extensions", "Curl's extensions: what each adds beyond curl and how to turn them off, or that they are off (curl --ai-help extensions).", ExtensionsSubject, readExtensionsMarkdown),
    ];

    /// <summary>Finds the resource in <paramref name="resources" /> whose URI is <paramref name="uri" />, compared exactly.</summary>
    /// <param name="resources">The resources to search.</param>
    /// <param name="uri">The URI a client asked to read.</param>
    /// <returns>The resource, or <see langword="null" /> when no resource has that URI.</returns>
    public static McpAiHelpResource? Find(IReadOnlyList<McpAiHelpResource> resources, string uri) =>
        resources.FirstOrDefault(resource => string.Equals(resource.Uri, uri, StringComparison.Ordinal));

    /// <summary>Finds the resource of <see cref="Resources" /> whose URI is <paramref name="uri" />, compared exactly.</summary>
    /// <param name="uri">The URI a client asked to read.</param>
    /// <returns>The resource, or <see langword="null" /> when no resource has that URI.</returns>
    public static McpAiHelpResource? Find(string uri) => Find(Resources, uri);

    private static McpAiHelpResource CurlAiHelpResource(string uri, string name, string description, string subject) =>
        new(uri, name, description, subject, () =>
        {
            _ = CurlAiHelpText.TryGetMarkdown(subject, out string markdown);
            return markdown;
        });
}
