using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and keeps the last
/// <see cref="TlsHandshakeEvent" />, so <see cref="TcpConnector" /> can write the handshake's
/// version, cipher suite and certificates to the diagnostic log (BL-920).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class HandshakeCapturingTransferEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>Gets the last handshake reported, or <see langword="null" /> when none was.</summary>
    public TlsHandshakeEvent? Handshake { get; private set; }

    /// <inheritdoc />
    public void ReportInfo(string text) => inner.ReportInfo(text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => inner.ReportConnectionOpened(opened);

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
        Handshake = handshake;
        inner.ReportTlsHandshake(handshake);
    }

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
