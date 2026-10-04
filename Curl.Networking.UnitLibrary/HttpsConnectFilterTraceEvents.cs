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
/// Through a proxy or over a Unix socket the same lines go around the proxy's own (measured, BL-1254
/// Notes): one more pair after each CONNECT request head, and two before <c>Opened SOCKS
/// connection</c>, the rounds a loopback SOCKS5 handshake measured (ADR-0357's BL-1254 amendment).
/// Over QUIC (measured with curl.se's curl 8.22.0 ngtcp2 build, BL-1284 Notes) <c>--http3</c> adds
/// <c>2nd attempt uses h2 from wanted versions</c> after the first attempt's line, a finished QUIC
/// handshake has one poll round, not two, and the TCP attempt that follows a QUIC one goes on with
/// the filter (<see cref="ContinueAfterFirstAttempt" />) rather than adding it again (ADR-0357's
/// BL-1284 amendment).
/// </summary>
/// <param name="inner">The events below the ALPN connect filter: the DNS filter's, or the transfer's own.</param>
/// <param name="firstAttemptVersion">The HTTP version curl's first attempt uses: <c>h1</c>, <c>h2</c> or <c>h3</c>.</param>
/// <param name="secondAttemptVersion">The version of the attempt curl may start after the first, if any: <c>h2</c> under <c>--http3</c>, <c>h3</c> after a preferred TCP attempt.</param>
/// <param name="firstAttemptIsPreferred"><see langword="true" /> when an <c>--alt-svc</c> entry chose the first attempt's version, which curl names <c>from preferred version</c> (BL-1320 Notes).</param>
internal sealed class HttpsConnectFilterTraceEvents(ITransferEvents inner, string firstAttemptVersion, string? secondAttemptVersion = null, bool firstAttemptIsPreferred = false) : ITransferEvents
{
    /// <summary>The line curl writes as it adds the ALPN connect filter, before anything is resolved.</summary>
    public const string AddedLine = "[HTTPS-CONNECT] added";

    private const string TryingPrefix = "  Trying ";
    private const string SocksOpenedPrefix = "Opened SOCKS connection ";
    private const string HappyEyeballingPrefix = "[SETUP] happy eyeballing";
    private const string ConnectingLine = "[HTTPS-CONNECT] connect -> 0, done=0";
    private bool _connecting;
    private int _sockets = 1;
    private CurlExitCode? _firstAttemptFailure;
    private bool _destroysFirstAttemptSetup;

    /// <summary>
    /// Goes on, for the attempt curl starts after the first one of a race, from where that attempt's
    /// filter stood (measured, BL-1284 and BL-1320 Notes): <c>&lt;first&gt; baller failed, starting
    /// &lt;second&gt;</c> when the first attempt failed with <paramref name="firstFailure" />, which a
    /// failed second attempt then reports as the connect's code; otherwise two more poll rounds of the
    /// first attempt and <c>&lt;first&gt; inconclusive after &lt;ms&gt;, starting &lt;second&gt;</c>,
    /// the second attempt's rounds counting both sockets. Once connected, the first attempt's setup
    /// filter's <c>[SETUP] destroy</c> follows the filter's own when <paramref name="tracesSetup" />.
    /// </summary>
    /// <param name="firstVersion">The first attempt's HTTP version: <c>h3</c> for QUIC, <c>h2</c> or <c>h1</c> for TCP.</param>
    /// <param name="secondVersion">The second attempt's HTTP version.</param>
    /// <param name="firstFailure">The first attempt's exit code, or <see langword="null" /> while it is still connecting.</param>
    /// <param name="happyEyeballsTimeout">How long curl let the first attempt run alone.</param>
    /// <param name="tracesSetup">Whether the setup filter is traced.</param>
    public void ContinueAfterFirstAttempt(string firstVersion, string secondVersion, CurlExitCode? firstFailure, TimeSpan happyEyeballsTimeout, bool tracesSetup)
    {
        _connecting = true;
        _firstAttemptFailure = firstFailure;
        _destroysFirstAttemptSetup = tracesSetup;
        if (firstFailure is not null)
        {
            inner.ReportInfo($"[HTTPS-CONNECT] {firstVersion} baller failed, starting {secondVersion}");
            return;
        }

        WritePollRound();
        WritePollRound();
        inner.ReportInfo($"[HTTPS-CONNECT] {firstVersion} inconclusive after {(long)happyEyeballsTimeout.TotalMilliseconds}, starting {secondVersion}");
        _sockets = 2;
    }

    /// <inheritdoc />
    public void ReportInfo(string text)
    {
        var trying = text.StartsWith(TryingPrefix, StringComparison.Ordinal);
        if (!_connecting && (trying || text.StartsWith(HappyEyeballingPrefix, StringComparison.Ordinal)))
        {
            WriteInitLines();
        }

        if (text.StartsWith(SocksOpenedPrefix, StringComparison.Ordinal))
        {
            WritePollRound();
            WritePollRound();
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
            inner.ReportInfo($"[HTTPS-CONNECT] connect -> {(int)(_firstAttemptFailure ?? exitCode)}, done=0");
        }
    }

    /// <inheritdoc />
    public void ReportConnectionOpened(ConnectionOpenedEvent opened)
    {
        inner.ReportInfo("[HTTPS-CONNECT] connect -> 0, done=1");
        inner.ReportConnectionOpened(opened);
        inner.ReportInfo("[HTTPS-CONNECT] removing connected setup filter");
        inner.ReportInfo("[HTTPS-CONNECT] destroy");
        if (_destroysFirstAttemptSetup)
        {
            inner.ReportInfo("[SETUP] destroy");
        }
    }

    /// <inheritdoc />
    public void ReportConnectionReused(ConnectionReusedEvent reused) => inner.ReportConnectionReused(reused);

    /// <inheritdoc />
    public void ReportTlsHandshake(TlsHandshakeEvent handshake)
    {
        WritePollRound();
        if (!handshake.Failed && !handshake.IsQuic)
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
    public void ReportRequestHeader(ReadOnlySpan<byte> bytes)
    {
        inner.ReportRequestHeader(bytes);
        WritePollRound();
    }

    /// <inheritdoc />
    public void ReportResponseHeader(ReadOnlySpan<byte> bytes) => inner.ReportResponseHeader(bytes);

    /// <inheritdoc />
    public void ReportDataSent(ReadOnlySpan<byte> bytes) => inner.ReportDataSent(bytes);

    /// <inheritdoc />
    public void ReportDataReceived(ReadOnlySpan<byte> bytes) => inner.ReportDataReceived(bytes);

    private void WriteInitLines()
    {
        _connecting = true;
        inner.ReportInfo("[HTTPS-CONNECT] connect, init");
        inner.ReportInfo($"[HTTPS-CONNECT] 1st attempt uses {firstAttemptVersion} from {(firstAttemptIsPreferred ? "preferred version" : "wanted versions")}");
        if (secondAttemptVersion is not null)
        {
            inner.ReportInfo($"[HTTPS-CONNECT] 2nd attempt uses {secondAttemptVersion} from wanted versions");
        }
    }

    private void WritePollRound()
    {
        inner.ReportInfo(ConnectingLine);
        inner.ReportInfo($"[HTTPS-CONNECT] adjust_pollset -> 0, {_sockets} socks");
    }
}
