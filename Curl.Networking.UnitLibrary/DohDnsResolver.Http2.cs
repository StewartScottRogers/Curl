using System.Globalization;
using System.Text;

using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// The same POST over HTTP/2, for a DoH server whose TLS handshake picked h2 (ADR-1424), each query
// the connection carries on a stream of its own (ADR-1428).
public sealed partial class DohDnsResolver
{
    // One query of an HTTP/2 connection: its place among the queries, the stream carrying it (0 until
    // opened), and what has arrived of its response.
    private sealed class Http2Query(int index)
    {
        public int Index { get; } = index;

        public int StreamId { get; set; }

        public int StatusCode { get; set; }

        public List<byte> Body { get; } = [];
    }

    // Sends each query as a POST on a stream of its own (1, then 3) of one new HTTP/2 connection and
    // hands answered each query's response as its stream ends: null when the stream, or the
    // connection under it, fails or the response is not read whole, as ExchangeAsync returns it.
    private async ValueTask ExchangeOverHttp2Async(IConnection connection, byte[][] queries, ITransferEvents events, Action<int, DohResponseReader.Response?> answered, CancellationToken cancellationToken)
    {
        var unanswered = Enumerable.Range(0, queries.Length).Select(index => new Http2Query(index)).ToList();
        try
        {
            var http2 = new Http2Connection(new ConnectionStream(connection));
            await http2.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
            var encoder = new HpackEncoder();
            foreach (var query in unanswered)
            {
                if (query.Index > 0)
                {
                    ReportHttp2Reuse(events);
                }

                await SendHttp2RequestAsync(http2, encoder, query, queries[query.Index], events, cancellationToken).ConfigureAwait(false);
                await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await ReadHttp2ResponsesAsync(http2, unanswered, events, answered, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsHttp2ExchangeFailure(exception))
        {
            // Every query still unanswered fails with the connection, below.
        }

        foreach (var query in unanswered.ToArray())
        {
            Answer(unanswered, query, null, answered);
        }
    }

    private async ValueTask SendHttp2RequestAsync(Http2Connection http2, HpackEncoder encoder, Http2Query query, byte[] body, ITransferEvents events, CancellationToken cancellationToken)
    {
        query.StreamId = http2.OpenStream();
        var fields = BuildRequestHeaderFields(body.Length);
        await http2.WriteHeadersAsync(query.StreamId, encoder.Encode(fields), isEndStream: false, cancellationToken).ConfigureAwait(false);
        // A query is at most 272 bytes, far inside the stream's initial send window, so one write sends it all.
        await http2.WriteDataAsync(query.StreamId, body, isEndStream: true, cancellationToken).ConfigureAwait(false);
        ReportHttp2Request(query.StreamId, fields, body, events);
    }

    // The -v lines curl writes as its second sub-transfer takes the first one's connection (ADR-1428).
    private void ReportHttp2Reuse(ITransferEvents events)
    {
        events.ReportInfo("Multiplexed connection found");
        events.ReportInfo($"Reusing existing {_dohUrl.Scheme}: connection with host {_dohServer.Host}");
    }

    private static void Answer(List<Http2Query> unanswered, Http2Query query, DohResponseReader.Response? response, Action<int, DohResponseReader.Response?> answered)
    {
        unanswered.Remove(query);
        answered(query.Index, response);
    }

    private static bool IsHttp2ExchangeFailure(Exception exception) =>
        exception is IOException or HpackDecodingException or Http2ProtocolException or Http2StreamResetException or Http2GoAwayException;

    // The header fields of the POST, pseudo-headers first, carrying what the HTTP/1.1 head carries.
    private List<HeaderField> BuildRequestHeaderFields(int queryLength)
    {
        List<HeaderField> fields =
        [
            new(":method", "POST"),
            new(":scheme", _dohUrl.Scheme),
            new(":authority", HostHeaderValue),
            new(":path", _dohUrl.PathAndQuery),
        ];
        if (BasicAuthorizationValue(_dohUrl.UserInfo) is { } authorization)
        {
            fields.Add(new("authorization", authorization));
        }

        fields.Add(new("accept", "*/*"));
        fields.Add(new("content-type", "application/dns-message"));
        fields.Add(new("content-length", queryLength.ToString(CultureInfo.InvariantCulture)));
        return fields;
    }

    // The -v lines of the sub-transfer's request, in the shape the HTTP handler gives an HTTP/2 one.
    private void ReportHttp2Request(int streamId, List<HeaderField> fields, byte[] query, ITransferEvents events)
    {
        var prefix = FormattableString.Invariant($"[HTTP/2] [{streamId}] ");
        events.ReportInfo($"{prefix}OPENED stream for {_dohUrl.Scheme}://{HostHeaderValue}{_dohUrl.PathAndQuery}");
        foreach (var field in fields)
        {
            events.ReportInfo($"{prefix}[{field.Name}: {field.Value}]");
        }

        events.ReportRequestHeader(BuildRequestHead(query, "HTTP/2"));
        events.ReportDataSent(query);
        events.ReportInfo($"upload completely sent off: {query.Length} bytes");
    }

    // Reads frames until every query is answered or the peer closes. A query is answered when its
    // stream ends or is reset, or its body is longer than curl's buffer.
    private static async ValueTask ReadHttp2ResponsesAsync(Http2Connection http2, List<Http2Query> unanswered, ITransferEvents events, Action<int, DohResponseReader.Response?> answered, CancellationToken cancellationToken)
    {
        var decoder = new HpackDecoder();
        while (unanswered.Count > 0)
        {
            Http2StreamFrame? frame;
            try
            {
                frame = await http2.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Http2StreamResetException reset)
            {
                // A stream given up on for its long body may be reset after it was answered.
                if (unanswered.Find(query => query.StreamId == reset.StreamId) is { } resetQuery)
                {
                    Answer(unanswered, resetQuery, null, answered);
                }

                continue;
            }

            if (frame is null)
            {
                return;
            }

            TakeFrame(frame, decoder, unanswered, events, answered);
        }
    }

    // A header block is decoded whichever stream it is on, since the HPACK decoder must see every one;
    // a frame for a query already answered is otherwise dropped.
    private static void TakeFrame(Http2StreamFrame frame, HpackDecoder decoder, List<Http2Query> unanswered, ITransferEvents events, Action<int, DohResponseReader.Response?> answered)
    {
        var fields = frame.Type == Http2FrameType.Headers ? decoder.Decode(frame.Content.Span) : null;
        if (unanswered.Find(query => query.StreamId == frame.StreamId) is not { } query)
        {
            return;
        }

        if (fields is not null)
        {
            query.StatusCode = TakeHeaderBlock(fields, query.StatusCode, events);
        }
        else
        {
            query.Body.AddRange(frame.Content.Span);
        }

        if (frame.IsEndStream || query.Body.Count > DohResponseReader.MaximumBodyLength)
        {
            Answer(unanswered, query, FinishedHttp2Response(query.StatusCode, [.. query.Body], events), answered);
        }
    }

    private static DohResponseReader.Response? FinishedHttp2Response(int statusCode, byte[] body, ITransferEvents events)
    {
        if (statusCode < 200 || body.Length > DohResponseReader.MaximumBodyLength)
        {
            return null;
        }

        if (body.Length > 0)
        {
            events.ReportDataReceived(body);
        }

        return new DohResponseReader.Response(statusCode, body);
    }

    // Reports one header block's lines as -v shows them, and returns the response's status so far:
    // the block's own when it has one (an interim 1xx is replaced), else the earlier one (trailers).
    private static int TakeHeaderBlock(IReadOnlyList<HeaderField> fields, int statusCode, ITransferEvents events)
    {
        foreach (var field in fields)
        {
            if (field.Name == ":status")
            {
                statusCode = int.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ? code : 0;
                events.ReportResponseHeader(Encoding.Latin1.GetBytes($"HTTP/2 {field.Value} \r\n"));
            }
            else
            {
                events.ReportResponseHeader(Encoding.Latin1.GetBytes($"{field.Name}: {field.Value}\r\n"));
            }
        }

        events.ReportResponseHeader("\r\n"u8.ToArray());
        return statusCode;
    }
}
