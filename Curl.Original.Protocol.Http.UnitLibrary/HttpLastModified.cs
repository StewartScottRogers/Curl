using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Reads the time a response's <c>Last-Modified</c> header gives, in Unix seconds: the document
/// time <c>-z</c> is compared against, and the source of
/// <see cref="TransferResult.SourceLastWriteUnixSeconds" /> for <c>-R</c>.
/// </summary>
/// <remarks>
/// The value is read with <see cref="CurlDateParser.ParseOrEpoch" />, as curl 8.22.0 reads it with
/// <c>Curl_getdate_capped</c>: the three HTTP-date forms RFC 9110 section 5.6.7 obliges a
/// recipient to accept - IMF-fixdate (<c>Sun, 06 Nov 1994 08:49:37 GMT</c>), RFC 850
/// (<c>Sunday, 06-Nov-94 08:49:37 GMT</c>) and asctime (<c>Sun Nov  6 08:49:37 1994</c>) - and
/// any other date curl reads, with a year past 9999 kept as curl's 64-bit <c>time_t</c> keeps it
/// (ADR-0410). A value curl does not read is the epoch, 0, and the instant one second before the
/// epoch is -1, which curl's file time takes as no time (measured on curl 8.22.0, BL-3576,
/// ADR-0821). The last <c>Last-Modified</c> header wins, as in curl.
/// </remarks>
internal static class HttpLastModified
{
    private const string HeaderName = "Last-Modified";

    private const long NoFileTime = -1;

    /// <summary>
    /// Finds the document time the last <c>Last-Modified</c> header of <paramref name="head" />
    /// gives, as curl compares it with <c>-z</c>.
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <returns>
    /// The time in seconds since the Unix epoch, 0 when curl would not read the value as a date,
    /// or <see langword="null" /> when there is no <c>Last-Modified</c> header.
    /// </returns>
    internal static long? Find(HttpResponseHead head)
    {
        string? value = head.Headers
            .LastOrDefault(header => string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase))?.Value;
        return value is null ? null : CurlDateParser.ParseOrEpoch(value);
    }

    /// <summary>
    /// Finds the file time <c>-R</c> gives the downloaded file: <see cref="Find" />'s time, except
    /// that -1 is no time, as curl's <c>CURLINFO_FILETIME</c> takes it.
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <returns>
    /// The time in seconds since the Unix epoch, or <see langword="null" /> when there is no
    /// <c>Last-Modified</c> header or it gives the instant one second before the epoch.
    /// </returns>
    internal static long? FindFileTime(HttpResponseHead head) =>
        Find(head) is { } time && time != NoFileTime ? time : null;
}
