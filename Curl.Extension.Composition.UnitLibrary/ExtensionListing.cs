using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.Composition;

/// <summary>
/// What <c>curl --extensions-help</c> and <c>curl --ai-help extensions</c> print, built from every registered
/// extension's <see cref="ExtensionDescription" /> (ADR-1008). With
/// <see cref="ExtensionProtocolHandlers.SwitchVariable" /> set to <c>0</c> both say that extensions are off.
/// </summary>
public static class ExtensionListing
{
    /// <summary>The line <c>--extensions-help</c> prints when extensions are off.</summary>
    public const string OffLine = "Extensions are off: CURL_EXTENSIONS is 0.";

    /// <summary>
    /// The lines <c>--extensions-help</c> prints: one per registered extension, or <see cref="OffLine" /> alone
    /// when extensions are off.
    /// </summary>
    /// <param name="readEnvironmentVariable">Reads the run's environment.</param>
    /// <returns>The lines, without terminators.</returns>
    public static IReadOnlyList<string> Lines(Func<string, string?> readEnvironmentVariable) =>
        AreOff(readEnvironmentVariable) ? [OffLine] : LinesOf(ExtensionDescriptionCatalogue.Entries);

    /// <summary>
    /// The Markdown <c>--ai-help extensions</c> prints: a heading, what extensions are, and one section per
    /// registered extension, or a line saying they are off. Every line ends in <c>\n</c> alone.
    /// </summary>
    /// <param name="readEnvironmentVariable">Reads the run's environment.</param>
    /// <returns>The Markdown.</returns>
    public static string Markdown(Func<string, string?> readEnvironmentVariable) =>
        MarkdownOf(ExtensionDescriptionCatalogue.Entries, AreOff(readEnvironmentVariable));

    /// <summary>One line per description: its name, summary, schemes and options.</summary>
    /// <param name="descriptions">The extensions' descriptions.</param>
    /// <returns>The lines, in order.</returns>
    internal static IReadOnlyList<string> LinesOf(IEnumerable<ExtensionDescription> descriptions) =>
        [.. descriptions.Select(description =>
            $"{description.Name}: {description.Summary} Schemes: {ListOf(description.Schemes, scheme => scheme)}. Options: {ListOf(description.Options, option => "--" + option.Name)}.")];

    /// <summary>Builds the <c>--ai-help extensions</c> Markdown for <paramref name="descriptions" />.</summary>
    /// <param name="descriptions">The extensions' descriptions.</param>
    /// <param name="off">Whether extensions are off, which replaces the sections with one line saying so.</param>
    /// <returns>The Markdown.</returns>
    internal static string MarkdownOf(IEnumerable<ExtensionDescription> descriptions, bool off)
    {
        List<string> lines =
        [
            "# extensions",
            string.Empty,
            "Curl's own additions beyond upstream curl: each adds URL schemes or long options curl does not have and never changes what curl's own options do. Set `CURL_EXTENSIONS=0` to turn every one off. `curl --extensions-help` lists them one per line.",
            string.Empty,
        ];
        lines.AddRange(off ? ["Extensions are off: `CURL_EXTENSIONS` is `0`."] : descriptions.SelectMany(SectionLines));
        return string.Concat(lines.Select(line => line + "\n"));
    }

    private static IEnumerable<string> SectionLines(ExtensionDescription description) =>
    [
        $"## {description.Name}",
        string.Empty,
        description.Summary,
        string.Empty,
        $"- Schemes: {ListOf(description.Schemes, scheme => $"`{scheme}://`")}",
        $"- Options: {ListOf(description.Options, OptionText)}",
        $"- Specification: <{description.Specification}>",
        string.Empty,
    ];

    private static string OptionText(ExtensionOption option) =>
        option.Argument == ExtensionOptionArgument.Value
            ? $"`--{option.Name} <value>` ({option.Help})"
            : $"`--{option.Name}` ({option.Help})";

    private static string ListOf<T>(IReadOnlyList<T> items, Func<T, string> format) =>
        items.Count == 0 ? "none" : string.Join(", ", items.Select(format));

    private static bool AreOff(Func<string, string?> readEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        return readEnvironmentVariable(ExtensionProtocolHandlers.SwitchVariable) == "0";
    }
}
