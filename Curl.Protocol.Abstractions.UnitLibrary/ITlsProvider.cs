namespace Curl.Protocol.Abstractions;

/// <summary>
/// Upgrades a plaintext connection to TLS.
/// </summary>
/// <remarks>
/// TLS lives behind this one interface because nine of curl's URL schemes differ from
/// another only by transport security. Implementing the handshake per protocol would
/// mean nine copies of the most security-sensitive code in the solution; instead every
/// protocol receives an already-secured <see cref="IConnection" /> and never knows the
/// difference. Production has two providers: <c>SslStreamTlsProvider</c> hands the
/// handshake to the .NET <c>SslStream</c>, and <c>HandBuiltTlsProvider</c> runs the
/// hand-built TLS client in <c>Curl.Tls.UnitLibrary</c>; the engine picks one per
/// transfer (ADR-0140).
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

    /// <summary>
    /// Performs the client handshake as
    /// <see cref="AuthenticateAsClientAsync(IConnection, string, ITransferEvents, CancellationToken)" /> does,
    /// offering <paramref name="applicationProtocols" /> through ALPN as curl's own origin handshake offers them
    /// (none under <c>--no-alpn</c>), so a connector outside <c>Curl.Networking</c> - an extension's, such as
    /// <c>--npipe</c>'s - can run an <c>https://</c> transfer that negotiates HTTP/2 (ADR-1066).
    /// </summary>
    /// <remarks>
    /// By default nothing is offered and the handshake is the four-argument overload's; a provider that offers
    /// ALPN overrides this.
    /// </remarks>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the handshake is reported.</param>
    /// <param name="applicationProtocols">The protocols to offer through ALPN, in preference order.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>
    /// The same result
    /// <see cref="AuthenticateAsClientAsync(IConnection, string, CancellationToken)" /> returns.
    /// </returns>
    ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, events, cancellationToken);

    /// <summary>
    /// Verifies a server certificate chain for <paramref name="targetHost" /> with the
    /// transfer's trust settings (<c>-k</c>, <c>--cacert</c>, <c>--capath</c>,
    /// <c>--crlfile</c>, <c>--pinnedpubkey</c>) without opening a TLS stream, so a protocol
    /// handler that runs its own handshake (DTLS for <c>coaps://</c>) judges the server's
    /// certificates as curl's own TLS would (ADR-1052).
    /// </summary>
    /// <remarks>
    /// By default the chain is rejected with <see cref="CurlExitCode.PeerFailedVerification" />
    /// (60), so a provider that cannot verify apart from a handshake never accepts a chain it
    /// has not checked; the engine's providers override this with the checks their handshakes run.
    /// </remarks>
    /// <param name="certificates">The DER certificates in the order the server sent them, its own first.</param>
    /// <param name="targetHost">The host name the certificate is checked against, IPv6 brackets and all.</param>
    /// <returns>
    /// <see cref="ServerCertificateChainVerdict.Accepted" />, or a rejection with the exit code and
    /// message the handshake would fail with.
    /// </returns>
    ServerCertificateChainVerdict VerifyServerCertificateChain(
        IReadOnlyList<ReadOnlyMemory<byte>> certificates,
        string targetHost) =>
        ServerCertificateChainVerdict.Rejected(
            CurlExitCode.PeerFailedVerification,
            "This TLS provider cannot verify a certificate chain apart from a handshake");
}
