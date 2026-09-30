using System.Globalization;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reports the <c>-v</c> lines curl 8.21.0's HTTP/2 and HTTP/3 layers write when a request
/// opens a stream, before the request head's <c>&gt;</c> lines: <c>[HTTP/2] [1] OPENED stream for</c>
/// and the URL as <c>%{url_effective}</c> prints it, then one <c>[HTTP/2] [1] [name: value]</c>
/// line for each header the stream sends, pseudo-headers first (measured with curl.se's
/// nghttp2 and ngtcp2 build, BL-660 Notes).
/// </summary>
/// <param name="events">The transfer's events, which the lines are reported through.</param>
/// <param name="url">The URL the lines name.</param>
internal sealed class HttpStreamOpenedLines(ITransferEvents events, string url)
{
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
    }
}
