using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every event on to <paramref name="inner" /> and adds the <c>[HTTPS-CONNECT]</c> lines curl
/// 8.21.0 writes for the ALPN connect filter of a direct <c>https://</c> connect under
/// <c>--trace-config https-connect</c> or <c>all</c> (measured, BL-1192 Notes). The connector writes
/// <see cref="AddedLine" /> itself, before any <c>[DNS]</c> line; this writes <c>connect, init</c> and
/// <c>1st attempt uses &lt;version&gt; from wanted versions</c> before the setup filter's <c>happy
/// eyeballing</c> line (or the first <c>Trying</c> line when that is not traced), a
/// <c>connect -&gt; 0, done=0</c> and <c>adjust_pollset -&gt; 0, 1 socks</c> pair after the
/// <c>Trying</c> line, two more before a finished TLS handshake's lines (one before a failed one's),
/// <c>connect -&gt; 0, done=1</c> before <c>Established connection</c> and the filter's removal
/// after it. curl writes one pair per poll round, so the count is fixed to what a loopback TLS 1.2
/// handshake measured, and the handshake's pairs come before its <c>ALPN:</c> lines, which one
/// event carries, where curl writes them between those lines (ADR-0357's BL-1192 amendment).
/// </summary>
/// <param name="inner">The events below the ALPN connect filter: the DNS filter's, or the transfer's own.</param>
/// <param name="firstAttemptVersion">The HTTP version curl's first attempt uses: <c>h1</c>, <c>h2</c> or <c>h3</c>.</param>
internal sealed class HttpsConnectFilterTraceEvents(ITransferEvents inner, string firstAttemptVersion) : ITransferEvents
{
    /// <summary>The line curl writes as it adds the ALPN connect filter, before anything is resolved.</summary>
    public const string AddedLine = "[HTTPS-CONNECT] added";

    private const string TryingPrefix = "  Trying ";
    private const string HappyEyeballingPrefix = "[SETUP] happy eyeballing";
    private const string ConnectingLine = "[HTTPS-CONNECT] connect -> 0, done=0";
    private const string PollsetLine = "[HTTPS-CONNECT] adjust_pollset -> 0, 1 socks";

    private bool _connecting;

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        var trying = text.StartsWith(TryingPrefix, StringComparison.Ordinal);
        if (!_connecting && (trying || text.StartsWith(HappyEyeballingPrefix, StringComparison.Ordinal)))
        {
            _connecting = true;
            inner.ReportInfo("[HTTPS-CONNECT] connect, init");
            inner.ReportInfo($"[HTTPS-CONNECT] 1st attempt uses {firstAttemptVersion} from wanted versions");
        }

        inner.ReportInfo(text);
        if (trying)
        {
            WritePollRound();
        }
    }

    /// <summary>
    /// Writes curl's <c>connect, all attempts failed</c> and <c>connect -&gt; &lt;code&gt;, done=0</c>
    /// after a failed dial or handshake, once the filter started connecting; nothing before that.
    /// </summary>
    /// <param name="exitCode">The exit code the connect failed with.</param>
    public void ReportConnectFailed(CurlExitCode exitCode)
    {
        if (_connecting)
        {
            inner.ReportInfo("[HTTPS-CONNECT] connect, all attempts failed");
            inner.ReportInfo($"[HTTPS-CONNECT] connect -> {(int)exitCode}, done=0");
        }
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        inner.ReportInfo("[HTTPS-CONNECT] connect -> 0, done=1");
        inner.ReportConnectionOpened(opened);
        inner.ReportInfo("[HTTPS-CONNECT] removing connected setup filter");
        inner.ReportInfo("[HTTPS-CONNECT] destroy");
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
        WritePollRound();
        if (!handshake.Failed)
        {
            WritePollRound();
        }

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
    public void ReportTlsEarlyData(long bytes) => inner.ReportTlsEarlyData(bytes);

    /// <inheritdoc />
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes) => inner.ReportRequestHeader(bytes);

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);

    private void WritePollRound()
    {
        inner.ReportInfo(ConnectingLine);
        inner.ReportInfo(PollsetLine);
    }
}
