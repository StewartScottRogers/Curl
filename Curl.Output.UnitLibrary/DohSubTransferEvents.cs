using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Passes a DoH sub-transfer's events on to the transfer's own <paramref name="inner" /> events
/// with curl 8.22.0's <c>[DoH] </c> prefix on every info line, as curl writes a DoH sub-transfer's
/// <c>-v</c> lines under <c>--trace-config dns</c>, <c>doh</c> or <c>all</c> (measured, BL-1157 Notes,
/// BL-2973; ADR-0380): the structured events - a connection opened or reused, a TLS handshake, message
/// and trust - are worded here as <see cref="TransferEventInfoText" /> words them for
/// <paramref name="tlsBackend" /> and reported as prefixed info lines, so a DNS filter line such as
/// <c>[DNS] added</c> becomes <c>[DoH] [DNS] added</c>. The header and data lines (<c>&gt;</c>,
/// <c>&lt;</c>, <c>}</c>, <c>{</c>) and the TLS bytes pass on unprefixed, as curl writes them.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="tlsBackend">The curl build whose wording the TLS events get (ADR-0085).</param>
/// <param name="writesDnsFilterLines">Whether the <c>[DNS]</c> filter lines are written: curl writes them under the <c>dns</c> component, not <c>doh</c> alone (BL-3814).</param>
/// <param name="writesSubTransferLines">Whether the sub-transfer's own lines, headers and TLS bytes are written: curl writes them under the <c>doh</c> component, not <c>dns</c> alone (BL-3814).</param>
/// <remarks>
/// The one exception is the DNS filter's creation line, <c>[DNS] created DNS filter for ...</c>,
/// which curl writes through its DNS trace feature rather than the filter, so it keeps its one
/// prefix.
/// </remarks>
public sealed class DohSubTransferEvents(ITransferEvents inner, TlsBackend tlsBackend, bool writesDnsFilterLines = true, bool writesSubTransferLines = true) : ITransferEvents
{
    /// <summary>The prefix curl puts on a DoH sub-transfer's info lines.</summary>
    public const string Prefix = "[DoH] ";

    private const string DnsLinePrefix = "[DNS] ";

    private const string FilterCreatedLineStart = "[DNS] created DNS filter for ";

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        var isDnsLine = text.StartsWith(DnsLinePrefix, StringComparison.Ordinal);
        if (isDnsLine ? !writesDnsFilterLines : !writesSubTransferLines)
        {
            return;
        }

        inner.ReportInfo(text.StartsWith(FilterCreatedLineStart, StringComparison.Ordinal) ? text : Prefix + text);
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened) => ReportInfo(TransferEventInfoText.ConnectionOpened(opened));

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => ReportInfo(TransferEventInfoText.ConnectionReused(reused));

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake) => ReportLines(TransferEventInfoText.TlsHandshake(handshake, tlsBackend));

    /// <inheritdoc />
    public void ReportTlsData(ReadOnlySpan<byte> bytes, bool sent) => ForwardTlsData(bytes, sent);

    /// <inheritdoc />
    public void ReportTlsMessage(TlsMessageEvent message)
    {
        ReportLines(TransferEventInfoText.TlsMessage(message, tlsBackend));
        inner.ReportTlsData(message.Bytes.Span, message.Sent);
    }

    /// <inheritdoc />
    public void ReportTlsTrust(TlsTrustEvent trust) => ReportLines(TransferEventInfoText.TlsTrust(trust, tlsBackend));

    /// <inheritdoc />
    public void ReportWarning(string text) => inner.ReportWarning(text);

    /// <inheritdoc />
    public void ReportCertificateVerifyResult(long verifyResult, bool isProxy) => inner.ReportCertificateVerifyResult(verifyResult, isProxy);

    /// <inheritdoc />
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
        if (writesSubTransferLines)
        {
            inner.ReportRequestHeader(bytes);
        }
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes)
    {
        if (writesSubTransferLines)
        {
            inner.ReportResponseHeader(bytes);
        }
    }

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes)
    {
        if (writesSubTransferLines)
        {
            inner.ReportDataSent(bytes);
        }
    }

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes)
    {
        if (writesSubTransferLines)
        {
            inner.ReportDataReceived(bytes);
        }
    }

    private void ForwardTlsData(ReadOnlySpan<byte> bytes, bool sent)
    {
        if (writesSubTransferLines)
        {
            inner.ReportTlsData(bytes, sent);
        }
    }

    private void ReportLines(IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            ReportInfo(line);
        }
    }
}
