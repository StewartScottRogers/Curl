using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The transfer's events while a passive data connection is dialled: every event passes to
/// <paramref name="transferEvents" />, with the connector's <c>-v</c> failure line rewritten by
/// <paramref name="failure" /> so it names the control connection as curl 8.21.0's does (BL-904),
/// and the connection opened marked the transfer's second one, named by
/// <paramref name="urlHost" />, so <c>-v</c> says <c>Established 2nd connection to &lt;URL host&gt;</c>
/// as curl 8.21.0's does whichever address the data connection dialled (measured, BL-944).
/// </summary>
/// <param name="transferEvents">The transfer's own events.</param>
/// <param name="failure">The rewrite for the data connection's failure line.</param>
/// <param name="urlHost">The URL's host, as given.</param>
internal sealed class FtpDataConnectEvents(ITransferEvents transferEvents, FtpDataConnectFailure failure, string urlHost) : ITransferEvents
{
    /// <inheritdoc />
    public void ReportInfo(string text) => transferEvents.ReportInfo(failure.Rewrite(text));

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) =>
        transferEvents.ReportConnectionOpened(opened with { HostName = urlHost, IsSecondConnection = true });

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => transferEvents.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => transferEvents.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => transferEvents.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => transferEvents.ReportDataReceived(bytes);
}
