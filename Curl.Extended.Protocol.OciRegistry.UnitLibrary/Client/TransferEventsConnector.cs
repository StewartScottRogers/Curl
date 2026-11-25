using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>
/// Opens every connection of one <c>oci://</c> transfer (registry, token endpoint, redirect target) with the transfer's
/// <see cref="ITransferContext.Proxy"/> (ADR-0056), <see cref="ITransferContext.Events"/> and <see cref="ITransferContext.DiagnosticLog"/> on its <see cref="ConnectTarget"/>,
/// so <c>-v</c> tells each connection and its TLS handshake as curl does (ADR-0046, ADR-0222). A hop whose host
/// the transfer's <see cref="ITransferContext.NoProxy" /> list exempts is opened without the proxy (ADR-1290).
/// </summary>
/// <param name="inner">Opens the connection.</param>
/// <param name="context">The transfer whose proxy, no-proxy list, events and diagnostic log each connection takes.</param>
internal sealed class TransferEventsConnector(IConnector inner, ITransferContext context) : IConnector
{
    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)
    {
        ProxyEndpoint? proxy = HopNoProxyMatcher.Matches(target.Host, context.NoProxy) ? null : context.Proxy;
        return inner.ConnectAsync(target with { Proxy = proxy, Events = context.Events, DiagnosticLog = context.DiagnosticLog }, cancellationToken);
    }
}
