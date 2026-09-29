using System.Text;
using Curl.Http2;

namespace Curl.Protocol.Http;

/// <summary>
/// Turns the HTTP/1.1 request head <see cref="HttpRequestHeadFormatter" /> formats into the
/// header list an HTTP/2 request sends, as curl 8.21.0 does (<c>Curl_http_req_to_h2</c>):
/// <c>:method</c>, <c>:scheme</c>, <c>:authority</c> from <c>Host</c> and <c>:path</c> from the
/// request target, then every other header in order with its name lower-cased, leaving out the
/// connection-specific ones HTTP/2 forbids (RFC 9113 section 8.2.2). Measured with curl.se's
/// nghttp2 build, whose HTTP/2 layer is libcurl's own (ADR-0141; BL-658 Notes).
/// </summary>
internal static class Http2RequestHeaders
{
    private static readonly string[] ConnectionSpecificNames =
        ["host", "connection", "keep-alive", "proxy-connection", "transfer-encoding", "upgrade"];

    private static readonly byte[] Http11LineEnd = " HTTP/1.1\r\n"u8.ToArray();

    private static readonly byte[] Http2LineEnd = " HTTP/2\r\n"u8.ToArray();

    /// <summary>
    /// Gives the header list for <paramref name="head" />. A <c>TE</c> header is sent only as
    /// <c>te: trailers</c>, and only when it lists <c>trailers</c>; a head with no <c>Host</c>
    /// sends no <c>:authority</c>.
    /// </summary>
    /// <param name="head">The request head: request line, header lines and empty line.</param>
    /// <param name="scheme">The URL's scheme, sent as <c>:scheme</c>.</param>
    /// <returns>The header list, in the order it is encoded.</returns>
    internal static List<HeaderField> Of(ReadOnlySpan<byte> head, string scheme)
    {
        string[] lines = Encoding.Latin1.GetString(head).Split("\r\n");
        string[] requestLine = lines[0].Split(' ');
        List<(string Name, string Value)> headers = [.. lines.Skip(1).TakeWhile(line => line.Length > 0).Select(Split)];
        List<HeaderField> fields = [new(":method", requestLine[0]), new(":scheme", scheme)];
        if (headers.FindIndex(header => header.Name == "host") is var hostIndex and >= 0)
        {
            fields.Add(new(":authority", headers[hostIndex].Value));
        }

        fields.Add(new(":path", requestLine[1]));
        fields.AddRange(headers.Select(Permitted).OfType<HeaderField>());
        return fields;
    }

    /// <summary>
    /// Gives <paramref name="head" /> with its request line's <c>HTTP/1.1</c> made
    /// <c>HTTP/2</c>, the head curl 8.21.0 reports sending over HTTP/2 (<c>&gt; GET / HTTP/2</c>).
    /// </summary>
    /// <param name="head">The HTTP/1.1 request head.</param>
    /// <returns>The same head naming HTTP/2.</returns>
    internal static byte[] WithHttp2RequestLine(byte[] head)
    {
        int lineEnd = head.AsSpan().IndexOf(Http11LineEnd);
        return [.. head.AsSpan(0, lineEnd), .. Http2LineEnd, .. head.AsSpan(lineEnd + Http11LineEnd.Length)];
    }

    private static (string Name, string Value) Split(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        return (line[..colon].ToLowerInvariant(), line[(colon + 1)..].Trim(' ', '\t'));
    }

    private static HeaderField? Permitted((string Name, string Value) header)
    {
        if (header.Name == "te")
        {
            return ListsTrailers(header.Value) ? new HeaderField("te", "trailers") : null;
        }

        return ConnectionSpecificNames.Contains(header.Name) ? null : new HeaderField(header.Name, header.Value);
    }

    private static bool ListsTrailers(string value) =>
        value.Split(',').Any(token => token.Trim(' ', '\t').Equals("trailers", StringComparison.OrdinalIgnoreCase));
}
