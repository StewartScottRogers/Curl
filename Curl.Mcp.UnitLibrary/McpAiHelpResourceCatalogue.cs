using Curl.Cli;

namespace Curl.Mcp;

/// <summary>
/// The MCP resources that serve <c>curl --ai-help</c>: the index at <c>curl://ai-help</c>, the full reference at
/// <c>curl://ai-help/all</c> and one resource per category of <see cref="CurlHelpText.Categories" /> at
/// <c>curl://ai-help/&lt;category&gt;</c>. The list is built from the category table, so a new category
/// appears without an edit here (ADR-0548, ADR-0589).
/// </summary>
public static class McpAiHelpResourceCatalogue
{
    /// <summary>The URI of the index; every other resource's URI is this, a slash and its subject.</summary>
    public const string IndexUri = "curl://ai-help";

    private const string AllSubject = "all";

    /// <summary>The resources, the index first, then <c>all</c>, then the categories in <c>--help category</c> order.</summary>
    public static IReadOnlyList<McpAiHelpResource> Resources { get; } =
    [
        new(IndexUri, "ai-help", "Index of curl's options for AI agents: the categories and the most-used options (curl --ai-help).", string.Empty),
        new($"{IndexUri}/{AllSubject}", "ai-help-all", "Every curl option by category, then the exit codes and the --write-out variables (curl --ai-help all).", AllSubject),
        .. CurlHelpText.Categories.Select(category => new McpAiHelpResource(
            $"{IndexUri}/{category.Name}",
            $"ai-help-{category.Name}",
            $"{category.Description}: every option in curl's {category.Name} category (curl --ai-help {category.Name}).",
            category.Name)),
    ];

    /// <summary>Finds the resource whose URI is <paramref name="uri" />, compared exactly.</summary>
    /// <param name="uri">The URI a client asked to read.</param>
    /// <returns>The resource, or <see langword="null" /> when no resource has that URI.</returns>
    public static McpAiHelpResource? Find(string uri) =>
        Resources.FirstOrDefault(resource => string.Equals(resource.Uri, uri, StringComparison.Ordinal));
}
