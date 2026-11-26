using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes the first DoH sub-transfer's events on to <paramref name="inner" /> and, after its first
/// <c>Trying</c> line, writes the lines the second sub-transfer writes while it waits under
/// <c>CURLOPT_PIPEWAIT</c> for that connection to say whether it can carry both queries (measured,
/// curl 8.18.0, ADR-1428).
/// </summary>
/// <param name="inner">Where every event goes.</param>
/// <param name="connectionNumber">The number <c>-v</c> gives that first connection.</param>
internal sealed class DohPipeWaitTraceEvents(ITransferEvents inner, long connectionNumber) : ITransferEvents
{
    private bool _hasWritten;

    public void ReportInfo(string text)
    {
        inner.ReportInfo(text);
        if (_hasWritten || !text.StartsWith("  Trying ", StringComparison.Ordinal))
        {
            return;
        }

        _hasWritten = true;
        inner.ReportInfo(FormattableString.Invariant($"Connection #{connectionNumber} is not open enough, cannot reuse"));
        inner.ReportInfo("Found pending candidate for reuse and CURLOPT_PIPEWAIT is set");
        inner.ReportInfo("Waiting on connection to negotiate possible multiplexing.");
    }

    public void ReportWarning(string text) => inner.ReportWarning(text);

    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => inner.ReportConnectionOpened(opened);

    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => inner.ReportTlsHandshake(handshake);

    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    public void ReportTlsMessage(TlsMessageEvent message) => inner.ReportTlsMessage(message);

    public void ReportTlsTrust(TlsTrustEvent trust) => inner.ReportTlsTrust(trust);

    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
