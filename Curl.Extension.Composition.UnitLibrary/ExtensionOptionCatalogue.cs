using Curl.Extension.Abstractions;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's long command-line option, one entry per line (ADR-1006). Each extension adds
/// its own line and nothing else here; the file is marked <c>merge=union</c> in <c>.gitattributes</c>
/// and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding different options do not overlap.
/// </summary>
internal static class ExtensionOptionCatalogue
{
    /// <summary>Gets the options, in the order they are registered.</summary>
    internal static IReadOnlyList<ExtensionOption> Entries { get; } =
    [
        Curl.Extension.NamedPipe.NamedPipeExtension.Option,
        GraphQl.GraphQlOptions.Document,
        GraphQl.GraphQlOptions.Variables,
        GraphQl.GraphQlOptions.Operation,
        GraphQl.GraphQlOptions.Introspect,
    ];
}
