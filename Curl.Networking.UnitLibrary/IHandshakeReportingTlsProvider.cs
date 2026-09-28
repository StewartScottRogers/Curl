using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An <see cref="ITlsProvider" /> that also reports, through the transfer's
/// <see cref="ITransferEvents" />, the trust each handshake is set up with and each handshake
/// it completes. <see cref="TcpConnector" /> passes the target's <see cref="ConnectTarget.Events" />
/// to a provider that implements it, for the handshake with an HTTPS proxy as well as with the
/// origin (BL-404, BL-452).
/// </summary>
internal interface IHandshakeReportingTlsProvider : ITlsProvider
{
    /// <summary>
    /// Performs the client handshake as
    /// <see cref="ITlsProvider.AuthenticateAsClientAsync(IConnection, string, CancellationToken)" />
    /// does, reporting the trust it is set up with to <paramref name="events" /> and, when it
    /// succeeds, what it negotiated, offering <paramref name="applicationProtocols" /> through ALPN.
    /// </summary>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the trust and the completed handshake are reported.</param>
    /// <param name="isProxy">
    /// <see langword="true" /> when the handshake is with an HTTPS proxy rather than the origin.
    /// </param>
    /// <param name="applicationProtocols">
    /// The protocols the connection can speak, in preference order, for the handshake to offer
    /// through ALPN unless the provider was told not to (<c>--no-alpn</c>); empty offers none.
    /// </param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result the three-argument overload returns.</returns>
    ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        bool isProxy,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken);
}
