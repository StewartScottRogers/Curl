using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Passes every event on to <paramref name="inner" /> unchanged and writes each CONNECT reply
/// head a tunnelling proxy sends to <paramref name="headerOutput" />, the transfer's
/// <c>-i</c>, <c>-I</c> or <c>-D</c> output, as curl 8.21.0 does without
/// <c>--suppress-connect-headers</c> (measured 2026-09-30, BL-613 Notes).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="headerOutput">The transfer's header output; it belongs to the caller and is neither flushed nor closed here.</param>
internal sealed class ConnectReplyHeadWritingEvents(ITransferEvents inner, Stream headerOutput) : IConnectReplyHeadWritingEvents
{
    /// <inheritdoc />
    public ValueTask WriteConnectReplyHeadAsync(ReadOnlyMemory<byte> head, CancellationToken cancellationToken) =>
        headerOutput.WriteAsync(head, cancellationToken);

    /// <inheritdoc />
    public void ReportInfo(string text) => inner.ReportInfo(text);

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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);
}
