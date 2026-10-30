using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.MlDsaCertificates;

/// <summary>
/// The <c>--mldsa-verify</c> extension (ADR-1121): ML-DSA signed certificates (FIPS 204, RFC 9881). After curl's own
/// verification accepted a server's chain, it requires the server's certificate to be signed with ML-DSA and verifies
/// every ML-DSA signature in the chain against its issuer's ML-DSA key. <c>Curl.Extension.Composition</c> registers
/// its option, description and certificate check hook.
/// </summary>
public static class MlDsaCertificatesExtension
{
    /// <summary>Gets the <c>--mldsa-verify</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "mldsa-verify",
        ExtensionOptionArgument.None,
        "Require the server certificate to be ML-DSA signed and verify every ML-DSA signature in its chain");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "mldsa-verify",
        "Requires the server's certificate to be signed with ML-DSA-44, ML-DSA-65 or ML-DSA-87 and verifies every ML-DSA certificate signature in the chain against its issuer's key, failing with exit 60 otherwise (FIPS 204, RFC 9881).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc9881"));

    /// <summary>Gives the extension's certificate check hook when the option group gave <c>--mldsa-verify</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--mldsa-verify</c> was not given.</returns>
    public static ICertificateCheckHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.IsGiven(Option) ? new MlDsaCertificateCheckHook() : null;
    }
}
