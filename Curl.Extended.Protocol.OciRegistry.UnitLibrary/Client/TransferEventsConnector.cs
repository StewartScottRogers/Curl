using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.OciRegistry.Client;

/// <summary>
/// Opens every connection of one <c>oci://</c> transfer (registry, token endpoint, redirect target) with the transfer's
/// <see cref="ITransferContext.Events"/> and <see cref="ITransferContext.DiagnosticLog"/> on its <see cref="ConnectTarget"/>,
/// so <c>-v</c> tells each connection and its TLS handshake as curl does (ADR-0046, ADR-0222).
/// </summary>
/// <param name="inner">Opens the connection.</param>
/// <param name="context">The transfer whose events and diagnostic log each connection reports to.</param>
internal sealed class TransferEventsConnector(IConnector inner, ITransferContext context) : IConnector
{
    /// <inheritdoc />
    public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
        inner.ConnectAsync(target with { Events = context.Events, DiagnosticLog = context.DiagnosticLog }, cancellationToken);
}
