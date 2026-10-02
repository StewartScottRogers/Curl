using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Writes the <c>[HTTP/3]</c> lines curl 8.21.0's ngtcp2/nghttp3 layer writes about a request
/// stream under <c>-v --trace-config http/3</c> (BL-1168, ADR-0375):
/// <c>[&lt;stream&gt;] end_headers, status=&lt;code&gt;</c> for each response head,
/// <c>[&lt;stream&gt;] DATA len=&lt;n&gt;</c> and <c>[&lt;stream&gt;] ACK &lt;n&gt;/&lt;n&gt; bytes of DATA</c>
/// for each piece of body, and <c>[&lt;stream&gt;] CLOSED</c> and
/// <c>[&lt;stream&gt;] quic close(app_error=256) -&gt; 0</c> once the response has ended. Measured
/// with curl 8.18.0's ngtcp2 build against cloudflare-quic.com; see BL-1168's Notes.
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

    /// <summary>Reports the lines held since the last flush, in the order they were held.</summary>
    internal void Flush()
    {
        foreach (string line in pending)
        {
            events!.ReportInfo(line);
        }

        pending.Clear();
    }

    private void Hold(long streamId, string text)
    {
        if (events is null)
        {
            return;
        }

        pending.Add(string.Create(CultureInfo.InvariantCulture, $"[HTTP/3] [{streamId}] {text}"));
    }
}
