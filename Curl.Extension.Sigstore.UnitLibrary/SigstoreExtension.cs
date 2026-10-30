using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Sigstore;

/// <summary>
/// The <c>--sigstore-bundle</c> extension (ADR-1093): writes a downloaded file only once a Sigstore bundle shows it was
/// signed by the expected identity, as <c>cosign verify-blob</c> checks it. <c>Curl.Extension.Composition</c>
/// registers its options, description and transfer hook.
/// </summary>
public static class SigstoreExtension
{
    /// <summary>Gets the <c>--sigstore-bundle &lt;file&gt;</c> option.</summary>
    public static ExtensionOption BundleOption { get; } = new(
        "sigstore-bundle",
        ExtensionOptionArgument.Value,
        "Write the download only if this Sigstore bundle verifies it");

    /// <summary>Gets the <c>--sigstore-identity &lt;identity&gt;</c> option.</summary>
    public static ExtensionOption IdentityOption { get; } = new(
        "sigstore-identity",
        ExtensionOptionArgument.Value,
        "The e-mail address or URI the signing certificate must name (needed with --sigstore-bundle)");

    /// <summary>Gets the <c>--sigstore-issuer &lt;url&gt;</c> option.</summary>
    public static ExtensionOption IssuerOption { get; } = new(
        "sigstore-issuer",
        ExtensionOptionArgument.Value,
        "The OIDC issuer the signing certificate must name (default: any)");

    /// <summary>Gets the <c>--sigstore-root &lt;pem-file&gt;</c> option.</summary>
    public static ExtensionOption RootOption { get; } = new(
        "sigstore-root",
        ExtensionOptionArgument.Value,
        "PEM file of the Fulcio CA certificates to trust (needed with --sigstore-bundle)");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "sigstore",
        "Verifies a download against a Sigstore bundle - digest, signature, Fulcio certificate chain and signer identity - before writing it.",
        [],
        [BundleOption, IdentityOption, IssuerOption, RootOption],
        new Uri("https://docs.sigstore.dev/about/bundle/"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--sigstore-bundle</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--sigstore-bundle</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(BundleOption) is { } bundlePath
            ? new SigstoreTransferHook(new SigstoreExpectation(bundlePath, values.ValueOf(IdentityOption), values.ValueOf(IssuerOption), values.ValueOf(RootOption)))
            : null;
    }
}
