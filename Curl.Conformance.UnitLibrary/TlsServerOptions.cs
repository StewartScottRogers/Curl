using System.Security.Cryptography.X509Certificates;

namespace Curl.Conformance;

/// <summary>
/// How <see cref="TlsServerStream"/> serves TLS, in the terms upstream's test servers configure
/// stunnel with: the certificate it presents (<c>cert =</c>), the ALPN protocols it offers, and
/// whether it asks the client for a certificate (stunnel's <c>verify</c>). Like stunnel at
/// <c>verify = 0</c>, it accepts any client certificate it is sent, valid or not.
/// </summary>
/// <param name="Certificate">The server certificate, with its private key, loaded by the caller from PEM or PKCS#12.</param>
/// <param name="ApplicationProtocols">The ALPN protocol names offered, such as <c>h2</c> and <c>http/1.1</c>; empty for no ALPN, as stunnel does by default.</param>
/// <param name="RequestClientCertificate">Whether the server asks the client for a certificate.</param>
public sealed record TlsServerOptions(
    X509Certificate2 Certificate,
    IReadOnlyList<string> ApplicationProtocols,
    bool RequestClientCertificate);
