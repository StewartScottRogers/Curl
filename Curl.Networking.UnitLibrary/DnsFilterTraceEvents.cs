using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and adds the <c>[DNS]</c> lines curl 8.21.0
/// writes for its DNS connection filter under <c>-v --trace-config dns</c> (or <c>doh</c> or
/// <c>all</c>) around a connect (measured, BL-1102 Notes): <see cref="Start" /> writes the filter's
/// creation for a host (a proxy's, for a connect through one), <see cref="StartOverUnixSocket" />
/// for a Unix socket, <c>[DNS] Curl_conn_connect(block=0) -&gt; 0, done=0</c> follows each
/// <c>Trying</c> line of a host's dial, the chain's connecting brackets <c>Established connection</c>,
/// and a dial that fails ends with the exit 7 it returns. A name the resolver answered nothing for
/// brackets <c>Could not resolve: &lt;host&gt;:&lt;port&gt;</c> with the negative cache entry before
/// it and the exit 6 after it, up to <c>[DNS] [1] shutdown async</c> (measured, BL-1157 Notes). A
/// negative DNS cache entry is preceded by <c>[DNS] cache entry does not have type=&lt;type&gt;
/// addresses</c> and its last <c>Could not resolve: &lt;host&gt;</c> followed by the exit 6 alone;
/// a name the resolver answered writes <c>[DNS] resolve complete for &lt;host&gt;:&lt;port&gt;</c>
/// before <c>Host &lt;host&gt;:&lt;port&gt; was resolved.</c> and <c>[DNS] [1] shutdown async</c>
/// after a refused dial's exit 7 (measured, BL-1181 Notes).
/// </summary>
/// <param name="inner">The transfer's own events.</param>
/// <param name="queriedTypes">
/// The record types the filter asks for, as curl names them: <c>A+AAAA</c>, or <c>A</c> under
/// <c>-4</c> and <c>AAAA</c> under <c>-6</c>.
/// </param>
/// <param name="host">The host the filter resolves, or empty for a Unix socket.</param>
/// <param name="port">The port it resolves the host for.</param>
/// <param name="writesDialProgress">
/// Whether a <c>Trying</c> line is followed by the unfinished connect's line: it is for a host's
/// dial, not for a Unix socket's immediate connect.
/// </param>
internal sealed class DnsFilterTraceEvents(
    ITransferEvents inner,
    string queriedTypes = "A+AAAA",
    string host = "",
    int port = 0,
    bool writesDialProgress = true) : ITransferEvents
{
    private const string TryingPrefix = "  Trying ";
    private const string FailedToConnectPrefix = "Failed to connect to ";
    private const string CouldNotResolvePrefix = "Could not resolve: ";
    private const string NegativeEntryLine = "Negative DNS entry";

    private bool _foundInCache;
    private bool _negativeEntry;
    private bool _resolvedAsynchronously;

    /// <summary>
    /// Gives the <c>-v</c> line curl writes after <c>Could not resolve host:</c> once a name its
    /// resolver was asked for resolved to nothing.
    /// </summary>
    /// <param name="host">The host that did not resolve.</param>
    /// <param name="port">The port it was resolved for.</param>
    /// <returns><c>Could not resolve: &lt;host&gt;:&lt;port&gt;</c>.</returns>
    public static string CouldNotResolveLine(string host, int port) => $"{CouldNotResolvePrefix}{host}:{port}";

    /// <summary>
    /// Gives the last <c>-v</c> line curl writes for a name its DNS cache holds a negative entry for.
    /// </summary>
    /// <param name="host">The host that did not resolve.</param>
    /// <returns><c>Could not resolve: &lt;host&gt;</c>.</returns>
    public static string CouldNotResolveLine(string host) => $"{CouldNotResolvePrefix}{host}";

    /// <summary>
    /// Writes the lines curl writes as it creates the filter for <paramref name="host" /> and
    /// <paramref name="port" />, and gives the events that write the rest of them.
    /// </summary>
    /// <param name="inner">The transfer's own events.</param>
    /// <param name="host">The host the connection resolves, after any <c>--connect-to</c> mapping.</param>
    /// <param name="port">The port it dials.</param>
    /// <param name="addressFamily">
    /// The <c>-4</c> or <c>-6</c> family, the only one curl's filter then asks for:
    /// <c>queries=1</c> and <c>type=A</c> under <c>-4</c>, <c>queries=2</c> and <c>type=AAAA</c> under
    /// <c>-6</c> (measured, BL-1157 Notes); any other value asks for both, <c>queries=3</c>.
    /// </param>
    /// <returns>The events to connect with.</returns>
    public static DnsFilterTraceEvents Start(ITransferEvents inner, string host, int port, AddressFamily addressFamily = AddressFamily.Unspecified)
    {
        var (queries, queriedTypes) = addressFamily switch
        {
            AddressFamily.InterNetwork => (1, "A"),
            AddressFamily.InterNetworkV6 => (2, "AAAA"),
            _ => (3, "A+AAAA"),
        };
        inner.ReportInfo($"[DNS] created DNS filter for {host}:{port}, transport=3, queries={queries}");
        inner.ReportInfo("[DNS] added");
        inner.ReportInfo($"[DNS] cf_dns_start host {host}:{port}");
        return new DnsFilterTraceEvents(inner, queriedTypes, host, port);
    }

    /// <summary>
    /// Writes the lines curl writes as it creates the filter for a connect over the Unix socket at
    /// <paramref name="path" />, transport 6 on port 0 (measured, BL-1181 Notes), and gives the
    /// events that write the rest of them.
    /// </summary>
    /// <param name="inner">The transfer's own events.</param>
    /// <param name="path">The socket's path as <c>--unix-socket</c> gave it.</param>
    /// <returns>The events to connect with.</returns>
    public static DnsFilterTraceEvents StartOverUnixSocket(ITransferEvents inner, string path)
    {
        inner.ReportInfo($"[DNS] created DNS filter for {path}:0, transport=6, queries=3");
        inner.ReportInfo("[DNS] added");
        inner.ReportInfo($"[DNS] cf_dns_start unix-domain-socket {path}:0");
        return new DnsFilterTraceEvents(inner, writesDialProgress: false);
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        if (text.StartsWith(CouldNotResolvePrefix, StringComparison.Ordinal))
        {
            ReportCouldNotResolve(text);
            return;
        }

        ReportBefore(text);
        inner.ReportInfo(text);
        ReportAfter(text);
    }

    // The lines curl writes before the given one: the cache entry's missing type before "Negative DNS
    // entry", and an asynchronous resolve's completion before the name's "Host ... was resolved.";
    // curl answers localhost itself, with no asynchronous resolve (measured, BL-1181 Notes).
    private void ReportBefore(string text)
    {
        if (text == NegativeEntryLine)
        {
            _negativeEntry = true;
            inner.ReportInfo($"[DNS] cache entry does not have type={queriedTypes} addresses");
        }
        else if (!_foundInCache && text == $"Host {host}:{port} was resolved." && !TcpConnector.IsLocalhost(host))
        {
            _resolvedAsynchronously = true;
            inner.ReportInfo($"[DNS] resolve complete for {host}:{port}");
        }
    }

    // The lines curl writes after the given one. A name found in the DNS cache was not resolved
    // asynchronously; one that was is shut down after the filter's exit 7 and destroyed after
    // "closing connection #N" (AsyncResolveTeardownTraceEvents).
    private void ReportAfter(string text)
    {
        if (text == $"Hostname {host} was found in DNS cache")
        {
            _foundInCache = true;
        }
        else if (writesDialProgress && text.StartsWith(TryingPrefix, StringComparison.Ordinal))
        {
            inner.ReportInfo("[DNS] Curl_conn_connect(block=0) -> 0, done=0");
        }
        else if (text.StartsWith(FailedToConnectPrefix, StringComparison.Ordinal))
        {
            inner.ReportInfo("[DNS] Curl_conn_connect(block=0) -> 7, done=0");
            inner.ReportInfo("[DNS] Curl_conn_connect(), filter returned 7");
            ReportShutdownIfResolvedAsynchronously();
        }
    }

    private void ReportShutdownIfResolvedAsynchronously()
    {
        if (_resolvedAsynchronously)
        {
            inner.ReportInfo(AsyncResolveTeardownTraceEvents.ShutdownLine);
        }
    }

    // A negative DNS cache entry's "Could not resolve" lines pass on, and its last, naming the host
    // alone, ends with the filter's exit 6 and no asynchronous resolve to shut down (measured,
    // BL-1181 Notes); any other is a failed resolve's.
    private void ReportCouldNotResolve(string text)
    {
        if (!_negativeEntry)
        {
            ReportResolveFailed(text);
            return;
        }

        inner.ReportInfo(text);
        if (text == CouldNotResolveLine(host))
        {
            ReportResolveError();
        }
    }

    // curl caches the name as negative, writes its own line, and its filter returns exit 6 and
    // shuts its asynchronous resolve down; the resolve is destroyed after "closing connection #N"
    // (AsyncResolveTeardownTraceEvents).
    private void ReportResolveFailed(string text)
    {
        inner.ReportInfo($"[DNS] cache negative name resolve for {text[CouldNotResolvePrefix.Length..]} type={queriedTypes}");
        inner.ReportInfo(text);
        ReportResolveError();
        inner.ReportInfo(AsyncResolveTeardownTraceEvents.ShutdownLine);
    }

    private void ReportResolveError()
    {
        inner.ReportInfo("[DNS] error resolving: 6");
        inner.ReportInfo("[DNS] Curl_conn_connect(block=0) -> 6, done=0");
        inner.ReportInfo("[DNS] Curl_conn_connect(), filter returned 6");
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
