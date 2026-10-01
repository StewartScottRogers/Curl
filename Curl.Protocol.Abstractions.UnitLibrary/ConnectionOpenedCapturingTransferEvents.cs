namespace Curl.Protocol.Abstractions;

/// <summary>
/// Passes every event on to the transfer's own events unchanged and keeps the last
/// <see cref="ConnectionOpenedEvent" /> reported for a transfer's first connection, so a
/// protocol handler that upgrades its connection to TLS in place (<c>STARTTLS</c>) can report
/// it again once the handshake is done, as curl 8.21.0 writes the connect's
/// <c>Established connection</c> line a second time after the upgrade (measured, BL-1058).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
public sealed class ConnectionOpenedCapturingTransferEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>
    /// Gets the last first-connection <see cref="ConnectionOpenedEvent" /> reported, or
    /// <see langword="null" /> when none was. A second connection, such as FTP's data
    /// connection, is passed on but not kept.
    /// </summary>
    public ConnectionOpenedEvent? Opened { get; private set; }

    /// <inheritdoc />
    public void ReportInfo(string text) => inner.ReportInfo(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        if (!opened.IsSecondConnection)
        {
            Opened = opened;
        }

        inner.ReportConnectionOpened(opened);
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => inner.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => inner.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => inner.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) =>
        inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
