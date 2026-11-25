namespace Curl.Extension.Abstractions;

/// <summary>
/// The transfer's own TLS trust settings, for an extension that opens a TLS connection of its own (for example
/// <c>--dns-over-tls</c>) and verifies the server's certificate as <c>--doh-url</c> does: against
/// <c>--cacert</c>/<c>--capath</c> in place of the system store, or not at all under <c>-k</c>, and without the revocation check under <c>--ssl-no-revoke</c>.
/// </summary>
/// <param name="Insecure"><see langword="true" /> under <c>-k</c>/<c>--insecure</c>: the certificate is not verified.</param>
/// <param name="CaCertificateFile">The <c>--cacert</c> file, or <see langword="null" />.</param>
/// <param name="CaCertificateDirectory">The <c>--capath</c> directory, or <see langword="null" />.</param>
/// <param name="SkipRevocationCheck"><see langword="true" /> under <c>--ssl-no-revoke</c>: the Schannel build skips the revocation check.</param>
public sealed record ExtensionTlsTrust(bool Insecure = false, string? CaCertificateFile = null, string? CaCertificateDirectory = null, bool SkipRevocationCheck = false);
