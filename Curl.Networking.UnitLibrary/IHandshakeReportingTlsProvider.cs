using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITlsProvider" /> that also reports each handshake it completes, as a
/// <see cref="TlsHandshakeEvent" />, through the transfer's <see cref="ITransferEvents" />.
/// <see cref="TcpConnector" /> passes the target's <see cref="ConnectTarget.Events" /> to a
/// provider that implements it (BL-404).
/// </summary>
internal interface IHandshakeReportingTlsProvider : ITlsProvider
{
    /// <summary>
    /// Performs the client handshake as
    /// <see cref="ITlsProvider.AuthenticateAsClientAsync(IConnection, string, CancellationToken)" />
    /// does and, when it succeeds, reports what it negotiated to <paramref name="events" />.
    /// </summary>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the completed handshake is reported.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result the three-argument overload returns.</returns>
    ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken);
}
