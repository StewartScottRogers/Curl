using System.Globalization;
using System.Text;

using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

// The same POST over HTTP/2, for a DoH server whose TLS handshake picked h2 (ADR-1418).
public sealed partial class DohDnsResolver
{
    // Sends the query on stream 1 of a new HTTP/2 connection and reads the answer; null when the
    // connection or the stream fails or the response is not read whole, as ExchangeAsync returns it.
    private async ValueTask<DohResponseReader.Response?> ExchangeOverHttp2Async(IConnection connection, byte[] query, ITransferEvents events, CancellationToken cancellationToken)
    {
        try
        {
            var http2 = new Http2Connection(new ConnectionStream(connection));
            await http2.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
            var streamId = http2.OpenStream();
            var fields = BuildRequestHeaderFields(query.Length);
            await http2.WriteHeadersAsync(streamId, new HpackEncoder().Encode(fields), isEndStream: false, cancellationToken).ConfigureAwait(false);
            // A query is at most 272 bytes, far inside the stream's initial send window, so one write sends it all.
            await http2.WriteDataAsync(streamId, query, isEndStream: true, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
            ReportHttp2Request(streamId, fields, query, events);
            return await ReadHttp2ResponseAsync(http2, events, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsHttp2ExchangeFailure(exception))
        {
            return null;
        }
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

    // Reads the stream to its end. Null when the peer closes first, ends the stream with no final
    // status, or sends a body longer than curl's buffer.
    private static async ValueTask<DohResponseReader.Response?> ReadHttp2ResponseAsync(Http2Connection http2, ITransferEvents events, CancellationToken cancellationToken)
    {
        var decoder = new HpackDecoder();
        var statusCode = 0;
        var body = new List<byte>();
        Http2StreamFrame? frame;
        do
        {
            frame = await http2.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false);
            if (frame is null)
            {
                return null;
            }

            if (frame.Type == Http2FrameType.Headers)
            {
                statusCode = TakeHeaderBlock(decoder.Decode(frame.Content.Span), statusCode, events);
            }
            else
            {
                body.AddRange(frame.Content.Span);
            }
        }
        while (!frame.IsEndStream && body.Count <= DohResponseReader.MaximumBodyLength);

        return FinishedHttp2Response(statusCode, [.. body], events);
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
