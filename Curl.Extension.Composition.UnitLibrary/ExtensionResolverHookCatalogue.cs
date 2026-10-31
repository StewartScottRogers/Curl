using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's resolver hook activator, one entry per line (ADR-1012): each gives the extension's
/// <see cref="IResolverHook" /> when an option group gave its option, otherwise <see langword="null" />. Each
/// extension task adds its own line and nothing else here; the file is marked <c>merge=union</c> in
/// <c>.gitattributes</c> and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions
/// do not overlap. A host passes them to <see cref="ResolverHooks.ActivatedBy" /> for each option group. Plain
/// method references, no reflection, so native AOT sees every type.
/// </summary>
public static class ExtensionResolverHookCatalogue
{
    /// <summary>Gets the activators; at most one may activate for an option group, and none beside <c>--doh-url</c>.</summary>
    public static IReadOnlyList<Func<ExtensionOptionValues, IResolverHook?>> Entries { get; } =
    [
        Curl.Extension.EncryptedDns.EncryptedDnsExtension.ActivateDnsOverTls,
        Curl.Extension.EncryptedDns.EncryptedDnsExtension.ActivateDnsOverQuic,
        Curl.Extension.ObliviousDoh.ObliviousDohExtension.ActivateResolver,
        Curl.Extension.WireGuard.WireGuardExtension.ActivateResolver,
    ];
}
