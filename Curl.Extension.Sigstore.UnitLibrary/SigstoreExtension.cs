using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Sigstore;

/// <summary>
/// The <c>--sigstore-bundle</c> extension (ADR-1101): writes a downloaded file only once a Sigstore bundle shows it was
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

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "sigstore",
        "Verifies a download against a Sigstore bundle - digest, signature, Fulcio certificate chain and signer identity - before writing it.",
        [],
        [BundleOption, IdentityOption, IssuerOption, RootOption],
        new Uri("https://docs.sigstore.dev/about/bundle/"));

    /// <summary>
    /// Gives the extension's transfer hook when the option group gave any <c>--sigstore-*</c> option; a companion option
    /// without <c>--sigstore-bundle</c> makes the hook refuse the transfer with exit code 2 rather than download unverified.
    /// </summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when no <c>--sigstore-*</c> option was given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var bundlePath = values.ValueOf(BundleOption);
        var identity = values.ValueOf(IdentityOption);
        var issuer = values.ValueOf(IssuerOption);
        var root = values.ValueOf(RootOption);
        if (bundlePath is not null)
        {
            return new SigstoreTransferHook(new SigstoreExpectation(bundlePath, identity, issuer, root));
        }

        string companions = string.Join(", ", new[] { (identity, "--sigstore-identity"), (root, "--sigstore-root"), (issuer, "--sigstore-issuer") }.Where(option => option.Item1 is not null).Select(option => option.Item2));
        return companions.Length == 0 ? null : throw new ArgumentException($"{companions} needs --sigstore-bundle", nameof(values));
    }
}
