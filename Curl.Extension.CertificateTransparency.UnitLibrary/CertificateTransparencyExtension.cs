using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.CertificateTransparency;

/// <summary>
/// The <c>--require-sct &lt;log list&gt;</c> extension (ADR-1119): Certificate Transparency (RFC 6962, RFC 9162). After curl
/// verified a server's chain, it refuses the connection with exit 60 unless the server's certificate carries an embedded
/// SCT from a log in the given log list that verifies. <c>Curl.Extension.Composition</c> registers its option, description
/// and certificate check hook.
/// </summary>
public static class CertificateTransparencyExtension
{
    /// <summary>Gets the <c>--require-sct</c> option.</summary>
    public static ExtensionOption Option { get; } = new(
        "require-sct",
        ExtensionOptionArgument.Value,
        "Require a valid Certificate Transparency SCT from a log in this v3 JSON log list on the server certificate");

    /// <summary>Gets what <c>curl --extensions</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "certificate-transparency",
        "Refuses a server certificate (exit 60) unless it carries a valid embedded Signed Certificate Timestamp from a log in a v3 JSON log list (RFC 6962, RFC 9162).",
        [],
        [Option],
        new Uri("https://www.rfc-editor.org/rfc/rfc6962"));

    /// <summary>Gives the extension's certificate check hook when the option group gave <c>--require-sct</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--require-sct</c> was not given.</returns>
    public static ICertificateCheckHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } logListPath ? new CertificateTransparencyCheckHook(logListPath, File.ReadAllBytesAsync, TimeProvider.System) : null;
    }
}
