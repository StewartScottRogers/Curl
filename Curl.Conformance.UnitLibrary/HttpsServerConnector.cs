using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// An <see cref="IConnector"/> that stands in for upstream's HTTPS test server, <c>sws</c> behind
/// <c>stunnel</c>, on <see cref="HttpsPort"/> (<c>%HTTPSPORT</c>): a connection to that port
/// speaks TLS with the certificate it is given, and the decrypted requests reach the sws
/// emulation, which records them in its <see cref="SwsHttpServerConnector.ReceivedBytes"/> for
/// <c>&lt;verify&gt;&lt;protocol&gt;</c>. Every other connection reaches the connector it wraps.
/// No socket is opened.
/// </summary>
/// <remarks>
/// Like stunnel as upstream's <c>servers.pm</c> starts it, the server offers no ALPN protocol,
/// so curl speaks HTTP/1.1, the only version sws reads, and asks for no client certificate.
/// </remarks>
/// <param name="sws">The sws emulation behind the TLS layer.</param>
/// <param name="certificate">The server certificate, with its private key; see <see cref="LoadCertificate"/>.</param>
/// <param name="backend">The server every connection not to <see cref="HttpsPort"/> reaches.</param>
public sealed class HttpsServerConnector(SwsHttpServerConnector sws, X509Certificate2 certificate, IConnector backend) : IConnector
{
    /// <summary>The port of the HTTPS server, <c>%HTTPSPORT</c>.</summary>
    public const int HttpsPort = 8989;

    /// <summary>
    /// The certificate file upstream's stunnel presents when a case's <c>&lt;server&gt;</c> line
    /// names none: <c>https</c> alone.
    /// </summary>
    public const string DefaultCertificateFile = "test-localhost.pem";

    private readonly TlsServerOptions options = new(certificate, [], false);

    /// <summary>
    /// Loads a server certificate and its key from a PEM file such as the
    /// <c>%CERTDIR/certs/test-localhost.pem</c> <see cref="UpstreamTestCertificateGenerator"/>
    /// writes, reloaded through PKCS#12, since Windows Schannel and macOS reject a server
    /// certificate whose key is ephemeral.
    /// </summary>
    /// <param name="pemPath">The PEM file holding the certificate and its private key.</param>
    /// <returns>The certificate, with its private key.</returns>
    public static X509Certificate2 LoadCertificate(string pemPath)
    {
        string pem = File.ReadAllText(pemPath);
        using X509Certificate2 ephemeral = X509Certificate2.CreateFromPem(pem, pem);
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), password: null);
    }

    /// <summary>Opens a TLS connection to the sws emulation on <see cref="HttpsPort"/>; any other on the wrapped connector.</summary>
    /// <param name="target">The host and port to connect to.</param>
    /// <param name="cancellationToken">Passed to the server connected to.</param>
    /// <returns>The connected result.</returns>
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Port != HttpsPort)
        {
            return await backend.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        }

        ConnectResult connected = await sws.ConnectAsync(target, cancellationToken).ConfigureAwait(false);
        return ConnectResult.Connected(new TlsServerConnection((SwsHttpServerConnection)connected.Connection!, options));
    }
}
