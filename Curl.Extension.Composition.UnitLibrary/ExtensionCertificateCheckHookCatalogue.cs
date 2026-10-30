using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;

namespace Curl.Extension.Composition;

/// <summary>
/// Every extension's certificate check hook activator, one entry per line (ADR-1016): each gives the extension's
/// <see cref="ICertificateCheckHook" /> when an option group gave its option, otherwise <see langword="null" />. Each
/// extension task adds its own line and nothing else here; the file is marked <c>merge=union</c> in
/// <c>.gitattributes</c> and listed in <c>Tasks/SharedListFiles.txt</c>, so lanes adding different extensions
/// do not overlap. A host passes them to <see cref="CertificateCheckHooks.ActivatedBy" /> for each option group.
/// Plain method references, no reflection, so native AOT sees every type.
/// </summary>
public static class ExtensionCertificateCheckHookCatalogue
{
    /// <summary>Gets the activators; every one that activates for an option group runs its check, in this order.</summary>
    public static IReadOnlyList<Func<ExtensionOptionValues, ICertificateCheckHook?>> Entries { get; } =
    [
        Curl.Extension.MlDsaCertificates.MlDsaCertificatesExtension.HookFor,
    ];
}
