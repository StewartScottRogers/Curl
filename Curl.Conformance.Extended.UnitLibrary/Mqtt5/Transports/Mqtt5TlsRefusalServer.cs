using System.Security.Cryptography.X509Certificates;
using Curl.Conformance;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.Mqtt5.Transports;

/// <summary>
/// The TLS front end for an <c>mqtt5s://</c> case whose certificate curl must refuse (BL-4486, MQTT 5.0 chapter 5):
/// on a plain TCP route it serves the TLS handshake with <paramref name="certificate" /> and records the failure
/// curl's refusal causes (Schannel ends the stream, OpenSSL alerts; either fails the accept), so the case still sees curl's exit code. (A TLS route's own front end would fail the run
/// instead.) A handshake curl completes is a failed case.
/// </summary>
/// <param name="certificate">The server certificate with its private key.</param>
public sealed class Mqtt5TlsRefusalServer(X509Certificate2 certificate) : IReferenceServer
{
    /// <summary>Gets the handshake failure curl's refusal caused; null until it has.</summary>
    public Exception? HandshakeFailure { get; private set; }

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        DelegatingDuplexStream wire = new(connection.ReadAsync, connection.WriteAsync);
        try
        {
            await using Stream tls = await TlsServerStream.AcceptAsync(wire, new TlsServerOptions(certificate, [], RequestClientCertificate: false), TlsServerStream.PlatformServesTls13, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            HandshakeFailure = failure;
            return;
        }

        throw new InvalidOperationException("curl completed the TLS handshake with a certificate it must refuse.");
    }
}
