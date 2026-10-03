using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes the <c>[HTTP/3]</c> lines curl 8.21.0's ngtcp2/nghttp3 layer writes about a request
/// stream under <c>-v --trace-config http/3</c> (BL-1168, ADR-0375):
/// <c>[&lt;stream&gt;] end_headers, status=&lt;code&gt;</c> for each response head,
/// <c>[&lt;stream&gt;] DATA len=&lt;n&gt;</c> and <c>[&lt;stream&gt;] ACK &lt;n&gt;/&lt;n&gt; bytes of DATA</c>
/// for each piece of body, and <c>[&lt;stream&gt;] CLOSED</c> and
/// <c>[&lt;stream&gt;] quic close(app_error=256) -&gt; 0</c> once the response has ended. Measured
/// with curl 8.18.0's ngtcp2 build against cloudflare-quic.com; see BL-1168's Notes. It also
/// writes, as they happen, the <c>status:</c> / <c>header:</c> echo of each response head line,
/// the peer's idle timeout as the connection's first stream opens, and the lines that end a
/// transfer (BL-1208, ADR-0388).
/// </summary>
/// <param name="events">The transfer's events, which the lines are reported through as info lines, or <see langword="null" /> to write none.</param>
/// <remarks>
/// curl writes each of these lines after it has handed what it describes to the transfer - the
/// head's <c>&lt;</c> lines, the body's <c>{ [n bytes data]</c> - so the lines are held until the
/// stream is next read (<see cref="Flush" />). curl's lines about its own I/O loop (<c>ingress</c>,
/// <c>egress</c>, <c>vquic_*</c>, <c>cf_send</c>, <c>cf_recv</c>, <c>read_stream</c>), whose count
/// varies from run to run, are not written.
/// </remarks>
internal sealed class Http3StreamTrace(ITransferEvents? events)
{
    /// <summary>The application error a stream closed cleanly carries: H3_NO_ERROR, 0x100.</summary>
    private const long NoErrorCode = 0x100;

    private readonly List<string> pending = [];

    /// <summary>Holds <c>[&lt;stream&gt;] end_headers, status=&lt;code&gt;</c> for a response head.</summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="statusCode">The head's status code.</param>
    internal void HeadReceived(long streamId, int statusCode) => Hold(streamId, FormattableString.Invariant($"end_headers, status={statusCode}"));

    /// <summary>Holds <c>DATA len=&lt;n&gt;</c> and <c>ACK &lt;n&gt;/&lt;n&gt; bytes of DATA</c> for body bytes received.</summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="length">How many body bytes arrived.</param>
    internal void DataReceived(long streamId, int length)
    {
        Hold(streamId, FormattableString.Invariant($"DATA len={length}"));
        Hold(streamId, FormattableString.Invariant($"ACK {length}/{length} bytes of DATA"));
    }

    /// <summary>Holds <c>CLOSED</c> and <c>quic close(app_error=256) -&gt; 0</c> once the response has ended.</summary>
    /// <param name="streamId">The stream.</param>
    internal void StreamClosed(long streamId)
    {
        Hold(streamId, "CLOSED");
        Hold(streamId, FormattableString.Invariant($"quic close(app_error={NoErrorCode}) -> 0"));
    }

    /// <summary>
    /// Reports <c>[HTTP/3] peer idle timeout is &lt;n&gt;ms, set keep-alive to &lt;n/2&gt; ms.</c> as the
    /// connection's first request stream opens (BL-1208), or nothing when the peer declared no idle timeout.
    /// </summary>
    /// <param name="peerIdleTimeout">The peer's <c>max_idle_timeout</c>, or <see langword="null" /> for none.</param>
    internal void ConnectionReady(TimeSpan? peerIdleTimeout)
    {
        if (peerIdleTimeout is { } idle)
        {
            long milliseconds = (long)idle.TotalMilliseconds;
            Report(FormattableString.Invariant($"[HTTP/3] peer idle timeout is {milliseconds}ms, set keep-alive to {milliseconds / 2} ms."));
        }
    }

    /// <summary>
    /// Reports <c>[&lt;stream&gt;] header: &lt;name&gt;: &lt;value&gt;</c> right before a response header
    /// line is reported, as curl's <c>cb_h3_recv_header</c> traces each field before it writes it (BL-1208);
    /// the status line and the head's empty line have no echo here.
    /// </summary>
    /// <param name="streamId">The stream the head arrived on.</param>
    /// <param name="line">The line about to be reported, its line end included.</param>
    internal void ResponseLineReporting(long streamId, ReadOnlySpan<byte> line)
    {
        string text = Encoding.Latin1.GetString(line).TrimEnd('\r', '\n');
        if (text.Length > 0 && !IsStatusLine(text))
        {
            Report(streamId, "header: " + text);
        }
    }

    /// <summary>
    /// Reports <c>[&lt;stream&gt;] status: HTTP/3 &lt;code&gt; </c> right after the status line is
    /// reported, its line end included, as curl traces the status line it has just written, so
    /// <c>-v</c> shows an empty line after it (measured, BL-1208 Notes); other lines have no echo here.
    /// </summary>
    /// <param name="streamId">The stream the head arrived on.</param>
    /// <param name="line">The line just reported, its line end included.</param>
    internal void ResponseLineReported(long streamId, ReadOnlySpan<byte> line)
    {
        string text = Encoding.Latin1.GetString(line);
        if (IsStatusLine(text))
        {
            Report(streamId, "status: " + text);
        }
    }

    /// <summary>
    /// Reports what curl writes once a transfer on the stream is done (BL-1208):
    /// <c>[&lt;stream&gt;] easy handle is done</c>, <c>no active streams, unset keep-alive</c> when no
    /// other transfer is on the connection, and <c>query conn[&lt;n&gt;]: MAX_CONCURRENT -&gt; &lt;left&gt; (&lt;in use&gt; in use)</c>
    /// when the connection knows how many more streams the peer allows.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    /// <param name="connectionNumber">The connection's number, as in <c>Connection #&lt;n&gt;</c>.</param>
    /// <param name="streamsLeft">How many more request streams the peer allows, or <see langword="null" /> when unknown.</param>
    /// <param name="streamsInUse">How many other request streams are still in use.</param>
    internal void TransferDone(long streamId, long connectionNumber, long? streamsLeft, int streamsInUse)
    {
        Report(streamId, "easy handle is done");
        if (streamsInUse == 0)
        {
            Report("[HTTP/3] no active streams, unset keep-alive");
        }

        if (streamsLeft is { } left)
        {
            Report(FormattableString.Invariant($"[HTTP/3] query conn[{connectionNumber}]: MAX_CONCURRENT -> {left} ({streamsInUse} in use)"));
        }
    }

    /// <summary>Reports the lines held since the last flush, in the order they were held.</summary>
    internal void Flush()
    {
        foreach (string line in pending)
        {
            events!.ReportInfo(line);
        }

        pending.Clear();
    }

    private static bool IsStatusLine(string text) => text.StartsWith("HTTP/3 ", StringComparison.Ordinal);

    private void Report(long streamId, string text) => Report(string.Create(CultureInfo.InvariantCulture, $"[HTTP/3] [{streamId}] {text}"));

    private void Report(string line) => events?.ReportInfo(line);

    private void Hold(long streamId, string text)
    {
        if (events is null)
        {
            return;
        }

        pending.Add(string.Create(CultureInfo.InvariantCulture, $"[HTTP/3] [{streamId}] {text}"));
    }
}
