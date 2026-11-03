using System.Security.Cryptography.X509Certificates;
using Curl.Conformance;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.Mqtt5.Transports;

namespace Curl.Conformance.Extended.DnsQuery.Encrypted;

/// <summary>
/// The TLS front end for a <c>dns://</c> DNS over TLS or HTTPS case whose certificate curl must refuse (BL-4681; RFC 8310 section 5, RFC 8484 section 5):
/// on a plain TCP route it serves the TLS handshake with <paramref name="certificate" /> and records curl's refusal,
/// so the case still sees curl's exit code. (A TLS route's own front end would fail the run instead.) curl refuses
/// either by failing the handshake (Schannel ends the stream, OpenSSL alerts; either fails the accept) or, where the
/// platform's TLS checks the certificate only once the handshake is done (OpenSSL under .NET on Linux and macOS,
/// BL-4596), by closing the connection before it sends a byte of DNS or HTTP. A byte of either is a failed case.
/// </summary>
/// <param name="certificate">The server certificate with its private key.</param>
public sealed class DnsTlsRefusalServer(X509Certificate2 certificate) : IReferenceServer
{
    /// <summary>Gets a value indicating whether curl refused the certificate: it failed the handshake or closed the connection before sending any DNS or HTTP.</summary>
    public bool CurlRefusedTheCertificate { get; private set; }

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        DelegatingDuplexStream wire = new(connection.ReadAsync, connection.WriteAsync);
        int applicationBytes;
        try
        {
            await using Stream tls = await TlsServerStream.AcceptAsync(wire, new TlsServerOptions(certificate, [], RequestClientCertificate: false), TlsServerStream.PlatformServesTls13, cancellationToken)
                .ConfigureAwait(false);
            applicationBytes = await tls.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            CurlRefusedTheCertificate = true;
            return;
        }

        CurlRefusedTheCertificate = applicationBytes == 0;
        if (!CurlRefusedTheCertificate)
        {
            throw new InvalidOperationException("curl sent a query over a TLS connection whose certificate it must refuse.");
        }
    }
}
