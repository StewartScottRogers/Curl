using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and writes <paramref name="waitLines" />, the
/// <c>[TIMER]</c> lines curl 8.21.0 writes under <c>-v --trace-config timer</c> as it waits for the
/// response, after each <c>Request completely sent off</c> line (measured, BL-1258 Notes, ADR-0401).
/// Used only when the multi is not traced: then <see cref="MultiStateTraceEvents" /> writes them among its
/// poll lines.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="waitLines">The lines, from <see cref="TransferTimers.WaitLines" />.</param>
internal sealed class ResponseWaitTimerTraceEvents(ITransferEvents inner, IReadOnlyList<string> waitLines) : ITransferEvents
{
    /// <summary>The line after which curl waits for the response.</summary>
    public const string RequestSentLine = "Request completely sent off";

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        inner.ReportInfo(text);
        if (text == RequestSentLine)
        {
            foreach (var line in waitLines)
            {
                inner.ReportInfo(line);
            }
        }
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => inner.ReportConnectionOpened(opened);

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
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
