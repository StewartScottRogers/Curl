namespace Curl.Extension.Abstractions.Certificates;

/// <summary>
/// Opens connections through curl's connector and puts the chain of each TLS connection it opened through
/// the activated certificate checks (ADR-1016). A QUIC connection's chain is not reported to it, so a
/// multiplexed connect passes through unchecked. Each connection it opened keeps its target's port, so an in-place
/// upgrade of it later is checked with that port (ADR-1141).
/// </summary>
/// <param name="inner">curl's connector.</param>
/// <param name="hooks">The activated checks.</param>
internal sealed class CertificateCheckingConnector(IConnector inner, CertificateCheckHooks hooks) : IConnector
{
    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        await hooks.CheckedConnectAsync(await inner.ConnectAsync(target, cancellationToken).ConfigureAwait(false), target, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public ValueTask<MultiplexedConnectResult> ConnectMultiplexedAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        inner.ConnectMultiplexedAsync(target, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<ConnectResult> ConnectMultiplexedSessionAsync(ConnectTarget target, Func<IMultiplexedConnection, IConnection> openSession, CancellationToken cancellationToken) =>
        await hooks.CheckedConnectAsync(await inner.ConnectMultiplexedSessionAsync(target, openSession, cancellationToken).ConfigureAwait(false), target, cancellationToken).ConfigureAwait(false);
}
