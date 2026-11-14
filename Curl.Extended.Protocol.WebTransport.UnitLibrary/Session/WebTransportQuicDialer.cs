using System.Net;
using System.Security.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Quic;
using Curl.Tls;

namespace Curl.Extended.Protocol.WebTransport.Session;

/// <summary>
/// Dials the QUIC connection a WebTransport session runs on (ADR-1089): ALPN <c>h3</c> and a non-zero
/// <c>max_datagram_frame_size</c> (draft-ietf-webtrans-http3-16 section 3.1, RFC 9221 section 3), with
/// curl's other transport parameters.
/// </summary>
/// <param name="connector">Opens the UDP channel, resolving the host as curl does.</param>
/// <param name="verifierFor">Gives the verifier that judges the certificate for a host.</param>
/// <param name="timeProvider">Times the handshake and the connection.</param>
internal sealed class WebTransportQuicDialer(IDatagramConnector connector, Func<string, IServerCertificateVerifier> verifierFor, TimeProvider timeProvider)
{
    /// <summary>The largest DATAGRAM frame the client accepts, advertised in <c>max_datagram_frame_size</c>.</summary>
    public const ulong MaxDatagramFrameSize = 65535;

    /// <summary>Gets the QUIC settings for <paramref name="serverName" />.</summary>
    /// <param name="serverName">The host the certificate must name.</param>
    /// <returns>The settings.</returns>
    public static QuicClientSettings CreateQuicSettings(string serverName) => new()
    {
        Tls = QuicClientSettings.CreateOpenSslTlsSettings(serverName) with { ApplicationProtocols = ["h3"] },
        TransportParameters = QuicTransportParameters.CurlClientDefaults with { MaxDatagramFrameSize = MaxDatagramFrameSize },
    };

    /// <summary>Opens the channel and runs the QUIC handshake, reporting nothing.</summary>
    /// <param name="request">The host and port to dial.</param>
    /// <param name="cancellationToken">Cancels the dial.</param>
    /// <returns>The connection, past its handshake.</returns>
    /// <exception cref="WebTransportException">The channel did not open or the handshake failed, with curl's exit code for it.</exception>
    public ValueTask<IMultiplexedConnection> DialAsync(WebTransportRequest request, CancellationToken cancellationToken) =>
        DialAsync(request, NoTransferEvents.Instance, cancellationToken);

    /// <summary>
    /// Opens the channel and runs the QUIC handshake, then reports the TLS handshake and the opened connection
    /// to <paramref name="events" />, which <c>-v</c> writes as the TLS backend's lines and the
    /// <c>Established connection</c> line (CurlSurface:verbose-webtransport-1).
    /// </summary>
    /// <param name="request">The host and port to dial.</param>
    /// <param name="events">Where the TLS handshake and the opened connection are reported.</param>
    /// <param name="cancellationToken">Cancels the dial.</param>
    /// <returns>The connection, past its handshake.</returns>
    /// <exception cref="WebTransportException">The channel did not open or the handshake failed, with curl's exit code for it.</exception>
    public async ValueTask<IMultiplexedConnection> DialAsync(WebTransportRequest request, ITransferEvents events, CancellationToken cancellationToken)
    {
        DatagramOpenResult opened = await connector.OpenAsync(request.Host, request.Port, cancellationToken).ConfigureAwait(false);
        if (opened.Channel is not { } channel)
        {
            throw new WebTransportException(opened.ExitCode, opened.ErrorMessage!);
        }

        QuicClientConnectionState state = new(CreateQuicSettings(request.Host), SystemTlsRandomSource.Instance, verifierFor(request.Host), timeProvider);
        if (await new QuicClientConnector(timeProvider).RunHandshakeAsync(state, channel, null, cancellationToken).ConfigureAwait(false) is { } failure)
        {
            state.Dispose();
            await channel.DisposeAsync().ConfigureAwait(false);
            throw new WebTransportException(failure.ExitCode, failure.Message);
        }

        events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.Tls13,
            CipherSuite = (System.Net.Security.TlsCipherSuite)state.Tls.CipherSuite!.Code,
            NegotiatedApplicationProtocol = state.Tls.ApplicationProtocol,
            OfferedApplicationProtocols = [],
            ServerCertificate = null,
            CertificateVerified = true,
            IsQuic = true,
        });
        events.ReportConnectionOpened(new ConnectionOpenedEvent
        {
            HostName = request.Host,
            RemoteEndPoint = channel.ServerEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, request.Port),
            LocalEndPoint = channel.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0),
            ConnectionNumber = 1,
        });
        return new QuicConnection(state, channel, timeProvider);
    }
}
