
namespace Curl.Protocol.Abstractions;

/// <summary>
/// Passes every event on to <paramref name="transferEvents" /> until <see cref="StopInfoLines" />,
/// after which info lines are dropped and every other event is still passed on. The FTP handler
/// gives it to its control connection's connector and stops it before <c>QUIT</c>: curl 8.21.0
/// sends <c>QUIT</c> as it closes the connection, where its trace writes no <c>[TCP] send</c> or
/// <c>recv</c> line for the command or its reply (measured, BL-1259 Notes).
/// </summary>
/// <param name="transferEvents">The transfer's own events.</param>
public sealed class InfoLineStoppingTransferEvents(ITransferEvents transferEvents) : ITransferEvents
{
    private bool writesInfoLines = true;

    /// <summary>Drops every info line reported from now on.</summary>
    public void StopInfoLines() => writesInfoLines = false;

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (writesInfoLines)
        {
            transferEvents.ReportInfo(text);
        }
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => transferEvents.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => transferEvents.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => transferEvents.ReportTlsHandshake(handshake);

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => transferEvents.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message) => transferEvents.ReportTlsMessage(message);

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => transferEvents.ReportTlsTrust(trust);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => transferEvents.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => transferEvents.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => transferEvents.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => transferEvents.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => transferEvents.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => transferEvents.ReportDataReceived(bytes);
}
