using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Scheme;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's scheme hook activator, one entry per line (ADR-1048): each gives the extension's
/// <see cref="ISchemeHook" /> when an option group gave its option, otherwise <see langword="null" />. Each
/// extension task adds its own line and nothing else here; the file is listed in <c>Tasks/SharedListFiles.txt</c>,
/// so lanes adding different extensions do not overlap. A host passes them to <see cref="SchemeHooks.ActivatedBy" />
/// for each option group. Plain method references, no reflection, so native AOT sees every type.
/// </summary>
public static class ExtensionSchemeHookCatalogue
{
    /// <summary>Gets the activators; at most one may activate for an option group.</summary>
    public static IReadOnlyList<Func<ExtensionOptionValues, ISchemeHook?>> Entries { get; } =
    [
    ];
}
