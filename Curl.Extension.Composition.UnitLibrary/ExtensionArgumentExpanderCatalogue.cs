using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's argument expander, one entry per line (ADR-1040): each replaces its option and value with curl
/// command-line arguments before the host parses the command line (<see cref="ArgumentExpansion.Expand" />). Each
/// extension task adds its own line and nothing else here; the file is marked <c>merge=union</c> in
/// <c>.gitattributes</c> and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions do not
/// overlap. An expander's option is also registered in <see cref="ExtensionOptionCatalogue" />, so
/// <c>--extensions</c> lists it. Plain instances, no reflection, so native AOT sees every type.
/// </summary>
public static class ExtensionArgumentExpanderCatalogue
{
    /// <summary>Gets the expanders; no two expand the same option.</summary>
    public static IReadOnlyList<IArgumentExpander> Entries { get; } =
    [
        new HttpFile.HttpFileArgumentExpander(),
        new OpenApi.OpenApiArgumentExpander(),
    ];
}
