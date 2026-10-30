using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Connection;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's connection hook activator, one entry per line (ADR-1010): each gives the extension's
/// <see cref="IConnectionHook" /> when an option group gave its option, otherwise <see langword="null" />. Each
/// extension task adds its own line and nothing else here; the file is marked <c>merge=union</c> in
/// <c>.gitattributes</c> and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions
/// do not overlap. A host passes them to <see cref="ConnectionHooks.ActivatedBy" /> for each option group. Plain
/// method references, no reflection, so native AOT sees every type.
/// </summary>
public static class ExtensionConnectionHookCatalogue
{
    /// <summary>Gets the activators; at most one may activate for an option group.</summary>
    public static IReadOnlyList<Func<ExtensionOptionValues, IConnectionHook?>> Entries { get; } =
    [
        Curl.Extension.NamedPipe.NamedPipeExtension.Activate,
        Curl.Extension.VirtualMachineSocket.VirtualMachineSocketExtension.Activate,
    ];
}
