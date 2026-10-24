using System.Security.Authentication;

namespace Curl.Conformance.Differential.Tls;

/// <summary>
/// One loopback TLS server the tester's <c>runner.py</c> started from <c>tls_server.py</c>: the
/// placeholder its cases name it by, its port, the <c>pki</c> certificate it presents, the TLS
/// versions it accepts and whether it requires a client certificate signed by <c>ca.pem</c>.
/// </summary>
/// <param name="Placeholder">The placeholder name in a case's arguments, such as <c>T12</c> for <c>{T12}</c>.</param>
/// <param name="Port">The loopback port the server listens on.</param>
/// <param name="CertificateName">The <c>pki</c> file name, without <c>.pem</c> or <c>.key</c>, of the certificate it presents.</param>
/// <param name="EnabledProtocols">The TLS versions the server accepts.</param>
/// <param name="RequiresClientCertificate">Whether the server requires a client certificate signed by <c>ca.pem</c>.</param>
public sealed record DifferentialTlsEndpoint(
    string Placeholder,
    int Port,
    string CertificateName,
    SslProtocols EnabledProtocols,
    bool RequiresClientCertificate)
{
    /// <summary>Gets the <c>host:port</c> text that replaces the placeholder, as <c>runner.py</c> wrote it.</summary>
    public string Authority => $"127.0.0.1:{Port}";
}
