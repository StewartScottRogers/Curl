using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and writes the last <c>[DNS]</c> line of a
/// failed resolve under <c>-v --trace-config dns</c> (or <c>doh</c> or <c>all</c>): once
/// <see cref="DnsFilterTraceEvents" /> has written <see cref="ShutdownLine" />, curl 8.21.0 writes
/// <c>[DNS] [1] destroy async</c> after the transfer's <c>closing connection #N</c> (measured,
/// BL-1157 Notes). The protocol writes that line after the connect has returned, so these events
/// wrap the whole transfer's, not only the connect's.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
public sealed class AsyncResolveTeardownTraceEvents(ITransferEvents inner) : ITransferEvents
{
    /// <summary>The line curl writes as its filter shuts a failed asynchronous resolve down.</summary>
    public const string ShutdownLine = "[DNS] [1] shutdown async";

    private const string DestroyLine = "[DNS] [1] destroy async";
    private const string ClosingConnectionPrefix = "closing connection #";

    private bool _shutDown;

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        inner.ReportInfo(text);
        if (text == ShutdownLine)
        {
            _shutDown = true;
        }
        else if (_shutDown && text.StartsWith(ClosingConnectionPrefix, StringComparison.Ordinal))
        {
            _shutDown = false;
            inner.ReportInfo(DestroyLine);
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
