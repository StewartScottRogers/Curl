using System.Net.Security;

namespace Curl.Conformance;

/// <summary>
/// Puts the server side of TLS on an in-memory server stream (such as one end of an
/// <see cref="InMemoryDuplexStream"/> pair), so a TLS stand-in for upstream's stunnel-fronted
/// servers can speak TLS without a socket. It adds no protocol: the caller reads and writes the
/// returned stream as the plain-text server would.
/// </summary>
public static class TlsServerStream
{
    /// <summary>Runs the server's TLS handshake on <paramref name="innerStream"/>.</summary>
    /// <param name="innerStream">The server's end of the connection; disposed with the returned stream, or at once if the handshake fails.</param>
    /// <param name="options">The certificate, ALPN protocols and client-certificate request.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The authenticated stream; its <see cref="SslStream.NegotiatedApplicationProtocol"/> and <see cref="SslStream.RemoteCertificate"/> say what the client agreed and sent.</returns>
    public static async Task<SslStream> AuthenticateAsync(Stream innerStream, TlsServerOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(innerStream);
        ArgumentNullException.ThrowIfNull(options);
        var sslStream = new SslStream(innerStream, leaveInnerStreamOpen: false);
        try
        {
            await sslStream.AuthenticateAsServerAsync(CreateAuthenticationOptions(options), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            sslStream.Dispose();
            throw;
        }

        return sslStream;
    }

    private static SslServerAuthenticationOptions CreateAuthenticationOptions(TlsServerOptions options) => new()
    {
        ServerCertificate = options.Certificate,
        ClientCertificateRequired = options.RequestClientCertificate,
        RemoteCertificateValidationCallback = static (_, _, _, _) => true,
        ApplicationProtocols = options.ApplicationProtocols.Count == 0
            ? null
            : [.. options.ApplicationProtocols.Select(protocol => new SslApplicationProtocol(protocol))],
    };
}
