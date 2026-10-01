namespace Curl.Protocol.Abstractions;

/// <summary>
/// Upgrades a plaintext connection to TLS.
/// </summary>
/// <remarks>
/// TLS lives behind this one interface because nine of curl's URL schemes differ from
/// another only by transport security. Implementing the handshake per protocol would
/// mean nine copies of the most security-sensitive code in the solution; instead every
/// protocol receives an already-secured <see cref="IConnection" /> and never knows the
/// difference. The production implementation delegates to the .NET
/// <c>SslStream</c> rather than implementing TLS.
/// </remarks>
public interface ITlsProvider
{
    /// <summary>
    /// Performs a client-side TLS handshake over an existing plaintext connection.
    /// </summary>
    /// <remarks>
    /// A failed handshake is returned, never thrown, so it reaches the protocol handler
    /// as a curl exit code like every other connect failure: the provider alone sees the
    /// certificate validation callback, so it alone can tell a verification failure
    /// (<see cref="CurlExitCode.PeerFailedVerification" />, 60) from any other handshake
    /// failure (<see cref="CurlExitCode.SslConnectError" />, 35). Only an
    /// <see cref="OperationCanceledException" /> escapes.
    /// </remarks>
    /// <param name="plaintext">
    /// The connection to upgrade. Ownership transfers to the provider: on success it is
    /// owned by the returned connection, and on failure the provider has disposed it.
    /// </param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>
    /// <see cref="ConnectResult.Connected(IConnection)" /> with a connection whose
    /// <see cref="IConnection.IsSecure" /> is <see langword="true" />, or
    /// <see cref="ConnectResult.Failed(CurlExitCode, string)" /> with the curl exit code
    /// and message for a failed handshake, after <paramref name="plaintext" /> has been
    /// disposed.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was cancelled during the handshake.
    /// </exception>
    ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken);

    /// <summary>
    /// Performs the client handshake as
    /// <see cref="AuthenticateAsClientAsync(IConnection, string, CancellationToken)" /> does,
    /// reporting to <paramref name="events" /> what a provider that reports its handshake
    /// reports, so a protocol handler's in-place upgrade (SMTP and IMAP <c>STARTTLS</c>, POP3
    /// <c>STLS</c>, FTP <c>AUTH TLS</c>) writes curl's <c>-v</c> TLS lines (BL-1058).
    /// </summary>
    /// <remarks>
    /// By default nothing is reported and the handshake is the three-argument overload's;
    /// a provider that reports its handshake overrides this.
    /// </remarks>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the handshake is reported.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>
    /// The same result
    /// <see cref="AuthenticateAsClientAsync(IConnection, string, CancellationToken)" /> returns.
    /// </returns>
    ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken);
}
