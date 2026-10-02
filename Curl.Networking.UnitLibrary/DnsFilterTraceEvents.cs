using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and adds the <c>[DNS]</c> lines curl 8.21.0
/// writes for its DNS connection filter under <c>-v --trace-config dns</c> (or <c>doh</c> or
/// <c>all</c>) around a direct connect (measured, BL-1102 Notes): <see cref="Start" /> writes the
/// filter's creation, <c>[DNS] Curl_conn_connect(block=0) -&gt; 0, done=0</c> follows each
/// <c>Trying</c> line, the chain's connecting brackets <c>Established connection</c>, and a dial that
/// fails ends with the exit 7 it returns.
/// </summary>
/// <param name="inner">The transfer's own events.</param>
internal sealed class DnsFilterTraceEvents(ITransferEvents inner) : ITransferEvents
{
    private const string TryingPrefix = "  Trying ";
    private const string FailedToConnectPrefix = "Failed to connect to ";

    /// <summary>
    /// Writes the lines curl writes as it creates the filter for <paramref name="host" /> and
    /// <paramref name="port" />, and gives the events that write the rest of them.
    /// </summary>
    /// <param name="inner">The transfer's own events.</param>
    /// <param name="host">The host the connection resolves, after any <c>--connect-to</c> mapping.</param>
    /// <param name="port">The port it dials.</param>
    /// <returns>The events to connect with.</returns>
    public static DnsFilterTraceEvents Start(ITransferEvents inner, string host, int port)
    {
        inner.ReportInfo($"[DNS] created DNS filter for {host}:{port}, transport=3, queries=3");
        inner.ReportInfo("[DNS] added");
        inner.ReportInfo($"[DNS] cf_dns_start host {host}:{port}");
        return new DnsFilterTraceEvents(inner);
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        inner.ReportInfo(text);
        if (text.StartsWith(TryingPrefix, StringComparison.Ordinal))
        {
            inner.ReportInfo("[DNS] Curl_conn_connect(block=0) -> 0, done=0");
        }
        else if (text.StartsWith(FailedToConnectPrefix, StringComparison.Ordinal))
        {
            inner.ReportInfo("[DNS] Curl_conn_connect(block=0) -> 7, done=0");
            inner.ReportInfo("[DNS] Curl_conn_connect(), filter returned 7");
        }
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        inner.ReportInfo("[DNS] connected filter chain below");
        inner.ReportInfo("[DNS] Curl_conn_connect(block=0) -> 0, done=1");
        inner.ReportConnectionOpened(opened);
        inner.ReportInfo("[DNS] removing connected setup filter");
        inner.ReportInfo("[DNS] destroy");
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
