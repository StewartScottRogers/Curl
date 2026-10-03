using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Passes a DoH sub-transfer's events on to the transfer's own <paramref name="inner" /> events
/// with curl 8.21.0's <c>[DNS] </c> prefix on every info line, as curl writes a DoH sub-transfer's
/// <c>-v</c> lines under <c>--trace-config dns</c>, <c>doh</c> or <c>all</c> (measured, BL-1157 Notes;
/// ADR-0380): the structured events - a connection opened or reused, a TLS handshake, message and
/// trust - are worded here as <see cref="TransferEventInfoText" /> words them for
/// <paramref name="tlsBackend" /> and reported as prefixed info lines, so a DNS filter line such as
/// <c>[DNS] added</c> becomes <c>[DNS] [DNS] added</c>. The header and data lines (<c>&gt;</c>,
/// <c>&lt;</c>, <c>}</c>, <c>{</c>) and the TLS bytes pass on unprefixed, as curl writes them.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="tlsBackend">The curl build whose wording the TLS events get (ADR-0085).</param>
/// <remarks>
/// The one exception is the DNS filter's creation line, <c>[DNS] created DNS filter for ...</c>,
/// which curl writes through its DNS trace feature rather than the filter, so it keeps its one
/// prefix.
/// </remarks>
public sealed class DohSubTransferEvents(ITransferEvents inner, TlsBackend tlsBackend) : ITransferEvents
{
    /// <summary>The prefix curl puts on a DoH sub-transfer's info lines.</summary>
    public const string Prefix = "[DNS] ";

    private const string FilterCreatedLineStart = "[DNS] created DNS filter for ";

    /// <inheritdoc />
    public void ReportInfo(string text) =>
        inner.ReportInfo(text.StartsWith(FilterCreatedLineStart, StringComparison.Ordinal) ? text : Prefix + text);

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => ReportInfo(TransferEventInfoText.ConnectionOpened(opened));

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => ReportInfo(TransferEventInfoText.ConnectionReused(reused));

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => ReportLines(TransferEventInfoText.TlsHandshake(handshake, tlsBackend));

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => inner.ReportTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message)
    {
        ReportLines(TransferEventInfoText.TlsMessage(message, tlsBackend));
        inner.ReportTlsData(message.Bytes.Span, message.Sent);
    }

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => ReportLines(TransferEventInfoText.TlsTrust(trust, tlsBackend));

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => inner.ReportCertificateVerifyResult(verifyResult, isProxy);

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

    private void ReportLines(IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            ReportInfo(line);
        }
    }
}
