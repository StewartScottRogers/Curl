namespace Curl.Extension.Abstractions.Certificates;

/// <summary>
/// Upgrades connections through curl's TLS provider and puts the chain of each handshake it accepted through
/// the activated certificate checks (ADR-1016): SMTP, IMAP and POP3 STARTTLS and FTP <c>AUTH TLS</c>. A check is given
/// the port the checking connector opened the plaintext connection to, or none when it did not open it (ADR-1131).
/// </summary>
/// <param name="inner">curl's TLS provider.</param>
/// <param name="hooks">The activated checks.</param>
internal sealed class CertificateCheckingTlsProvider(ITlsProvider inner, CertificateCheckHooks hooks) : ITlsProvider
{
    /// <inheritdoc />
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
        await hooks.CheckedAsync(await inner.AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken).ConfigureAwait(false), targetHost, hooks.PortOf(plaintext), handshook: true, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, CancellationToken cancellationToken) =>
        await hooks.CheckedAsync(await inner.AuthenticateAsClientAsync(plaintext, targetHost, events, cancellationToken).ConfigureAwait(false), targetHost, hooks.PortOf(plaintext), handshook: true, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, ITransferEvents events, IReadOnlyList<string> applicationProtocols, CancellationToken cancellationToken) =>
        await hooks.CheckedAsync(await inner.AuthenticateAsClientAsync(plaintext, targetHost, events, applicationProtocols, cancellationToken).ConfigureAwait(false), targetHost, hooks.PortOf(plaintext), handshook: true, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public ServerCertificateChainVerdict VerifyServerCertificateChain(IReadOnlyList<ReadOnlyMemory<byte>> certificates, string targetHost) =>
        inner.VerifyServerCertificateChain(certificates, targetHost);
}
