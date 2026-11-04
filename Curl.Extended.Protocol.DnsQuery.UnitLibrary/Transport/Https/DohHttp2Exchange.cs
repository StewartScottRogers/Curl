using System.Globalization;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.Transport.Https;

/// <summary>
/// Sends a DNS query over HTTP/2 once ALPN selected <see cref="Protocol" /> (RFC 8484 section 5.2, BL-4690, ADR-1231):
/// the client preface, one stream carrying a POST to <see cref="DohRequest.Path" /> with the query as its only DATA
/// frame (a DNS query is far below the 16384-octet default frame size and the 65535-octet window), and the answer read
/// from the stream: informational responses skipped, the final one checked as a 200 <c>application/dns-message</c>,
/// its DATA frames joined into the answer and any trailers ignored.
/// </summary>
internal static class DohHttp2Exchange
{
    /// <summary>The ALPN identifier of HTTP/2 over TLS (RFC 9113 section 3.2).</summary>
    public const string Protocol = "h2";

    /// <summary>Sends the query and reads the answer.</summary>
    /// <param name="connection">The TLS connection, ALPN having selected <see cref="Protocol" />; the caller owns it.</param>
    /// <param name="host">The server's host, an IPv6 address without brackets.</param>
    /// <param name="port">The server's port; <c>:authority</c> names it unless it is 443.</param>
    /// <param name="query">The encoded DNS query.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The answer's octets.</returns>
    /// <exception cref="DnsQueryException">
    /// The status is not 200 (exit 22), the response is malformed or carries no DNS message (exit 8), or the stream
    /// ended or the connection closed before the whole response arrived, including a reset or GOAWAY (exit 56); an HTTP/2 or HPACK error, or an answer over 65535 octets, is exit 8.
    /// </exception>
    public static async ValueTask<byte[]> ExchangeAsync(IConnection connection, string host, int port, byte[] query, CancellationToken cancellationToken)
    {
        try
        {
            return await ExchangeCoreAsync(connection, host, port, query, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is Http2StreamResetException or Http2GoAwayException or EndOfStreamException)
        {
            throw new DnsQueryException(CurlExitCode.RecvError, $"The DoH server's HTTP/2 connection failed before its whole answer arrived: {failure.Message}");
        }
        catch (Exception failure) when (failure is Http2ProtocolException or HpackDecodingException)
        {
            throw DohResponseHead.Malformed($"The DoH server's HTTP/2 response is malformed: {failure.Message}");
        }
    }

    private static async ValueTask<byte[]> ExchangeCoreAsync(IConnection connection, string host, int port, byte[] query, CancellationToken cancellationToken)
    {
        Http2Connection http2 = new(new DohConnectionStream(connection));
        await http2.SendPrefaceAsync(cancellationToken).ConfigureAwait(false);
        int streamId = http2.OpenStream();
        HeaderField[] headers =
        [
            new(":method", "POST"),
            new(":scheme", "https"),
            new(":authority", DohRequest.AuthorityOf(host, port)),
            new(":path", DohRequest.Path),
            new("accept", DohRequest.MediaType),
            new("content-type", DohRequest.MediaType),
            new("content-length", query.Length.ToString(CultureInfo.InvariantCulture)),
        ];
        await http2.WriteHeadersAsync(streamId, new HpackEncoder().Encode(headers), isEndStream: false, cancellationToken).ConfigureAwait(false);
        await http2.WriteDataAsync(streamId, query, isEndStream: true, cancellationToken).ConfigureAwait(false);
        return await ReadAnswerAsync(http2, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<byte[]> ReadAnswerAsync(Http2Connection http2, CancellationToken cancellationToken)
    {
        HpackDecoder decoder = new();
        DohResponseHead? head = null;
        List<byte> body = [];
        for (Http2StreamFrame? frame = await http2.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false); frame is not null; frame = await http2.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false))
        {
            if (head is null)
            {
                head = FinalHeadOf(decoder.Decode(frame.Content.Span));
            }
            else if (frame.Type == Http2FrameType.Data)
            {
                AppendWithinLimit(body, frame.Content.Span);
            }

            if (frame.IsEndStream)
            {
                return head is null ? throw Closed() : [.. body];
            }
        }

        throw Closed();
    }

    /// <summary>Appends DATA to the answer, refusing (exit 8) before the answer would pass a DNS message's 65535 octets.</summary>
    private static void AppendWithinLimit(List<byte> body, ReadOnlySpan<byte> data)
    {
        if (body.Count + data.Length > DohResponseHead.MaxMessageLength)
        {
            throw DohResponseHead.Malformed($"The DoH server's answer is longer than a DNS message's {DohResponseHead.MaxMessageLength} octets");
        }

        body.AddRange(data);
    }

    /// <summary>Reads a response head's fields; gives <see langword="null" /> for an informational response.</summary>
    private static DohResponseHead? FinalHeadOf(IReadOnlyList<HeaderField> fields)
    {
        string? status = null;
        string? contentType = null;
        foreach (HeaderField field in fields)
        {
            status = field.Name == ":status" ? field.Value : status;
            contentType = field.Name == "content-type" ? field.Value : contentType;
        }

        DohResponseHead head = int.TryParse(status, NumberStyles.None, CultureInfo.InvariantCulture, out int code)
            ? new DohResponseHead(code, contentType, null, false)
            : throw DohResponseHead.Malformed($"The DoH server's HTTP/2 response head has no valid :status, but \"{status}\"");
        if (head.IsInterim)
        {
            return null;
        }

        head.EnsureDnsMessage();
        return head;
    }

    private static DnsQueryException Closed() =>
        new(CurlExitCode.RecvError, "The DoH server ended the HTTP/2 stream before its whole answer arrived");
}
