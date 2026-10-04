using System.Globalization;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reports the <c>-v</c> lines curl 8.21.0's HTTP/2 and HTTP/3 layers write when a request
/// opens a stream, before the request head's <c>&gt;</c> lines: <c>[HTTP/2] [1] OPENED stream for</c>
/// and the URL as <c>%{url_effective}</c> prints it, then one <c>[HTTP/2] [1] [name: value]</c>
/// line for each header the stream sends, pseudo-headers first (measured with curl.se's
/// nghttp2 and ngtcp2 build, BL-660 Notes), then, for an HTTP/2 stream whose headers total
/// more than <see cref="MaximumHttp2HeaderBytes" /> bytes, <see cref="Http2HeadersTooLongWarning" />.
/// </summary>
/// <param name="events">The transfer's events, which the lines are reported through.</param>
/// <param name="url">The URL the lines name.</param>
internal sealed class HttpStreamOpenedLines(ITransferEvents events, string url)
{
    /// <summary>
    /// The total of an HTTP/2 stream's header name and value lengths in bytes, pseudo-headers
    /// included, that curl 8.21.0 sends without a warning (<c>MAX_ACC</c> in <c>lib/http2.c</c>).
    /// HPACK writes each character as one Latin-1 byte, so a string's length is its byte count.
    /// </summary>
    internal const int MaximumHttp2HeaderBytes = 60000;

    /// <summary>
    /// The <c>-v</c> line curl 8.21.0 writes after the header lines of an HTTP/2 stream whose
    /// headers total more than <see cref="MaximumHttp2HeaderBytes" /> bytes; HTTP/3 has none (BL-1431).
    /// </summary>
    internal const string Http2HeadersTooLongWarning =
        "[HTTP/2] Warning: The cumulative length of all headers exceeds 60000 bytes and that could cause the stream to be rejected.";

    /// <summary>
    /// Reports the lines for the stream <paramref name="streamId" /> that sent
    /// <paramref name="fields" />.
    /// </summary>
    /// <param name="versionName">The version the stream speaks: <c>HTTP/2</c> or <c>HTTP/3</c>.</param>
    /// <param name="streamId">The stream's identifier.</param>
    /// <param name="fields">The header list the stream sent, in order.</param>
    internal void Report(string versionName, long streamId, IReadOnlyList<HeaderField> fields)
    {
        string prefix = string.Create(CultureInfo.InvariantCulture, $"[{versionName}] [{streamId}] ");
        events.ReportInfo(prefix + "OPENED stream for " + url);
        foreach (HeaderField field in fields)
        {
            events.ReportInfo($"{prefix}[{field.Name}: {field.Value}]");
        }

        if (versionName == "HTTP/2" && fields.Sum(field => field.Name.Length + field.Value.Length) > MaximumHttp2HeaderBytes)
        {
            events.ReportInfo(Http2HeadersTooLongWarning);
        }
    }
}
