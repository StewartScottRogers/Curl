using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.Dpop;

/// <summary>
/// The <c>--dpop</c> extension (ADR-1145): OAuth 2.0 Demonstrating Proof of Possession (RFC 9449). It signs a DPoP proof
/// JWT for every HTTP request with the private key in a PEM file, sends an access token as <c>Authorization: DPoP</c>,
/// and retries once with the nonce a server demands. <c>Curl.Extension.Composition</c> registers its option, description
/// and transfer hook.
/// </summary>
public static class DpopExtension
{
    /// <summary>Gets the <c>--dpop &lt;key file&gt;</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "dpop",
        ExtensionOptionArgument.Value,
        "Sign a DPoP proof (RFC 9449) for each HTTP request with the EC or RSA private key in the PEM <key file>");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "dpop",
        "Binds OAuth 2.0 access tokens to a key: signs a DPoP proof JWT for every HTTP request, sends --oauth2-bearer as Authorization: DPoP and answers DPoP-Nonce challenges (RFC 9449).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9449"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--dpop</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook over the named key file, or <see langword="null" /> when <c>--dpop</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } keyFile ? new DpopTransferHook(keyFile) : null;
    }
}
