using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads the time a response's <c>Last-Modified</c> header gives, in Unix seconds: the source of
/// <see cref="TransferResult.SourceLastWriteUnixSeconds" /> for <c>-R</c> and the document time
/// <c>-z</c> is compared against.
/// </summary>
/// <remarks>
/// The value is read with <see cref="CurlDateParser" />, as curl 8.21.0 reads it with
/// <c>Curl_getdate_capped</c>: the three HTTP-date forms RFC 9110 section 5.6.7 obliges a
/// recipient to accept - IMF-fixdate (<c>Sun, 06 Nov 1994 08:49:37 GMT</c>), RFC 850
/// (<c>Sunday, 06-Nov-94 08:49:37 GMT</c>) and asctime (<c>Sun Nov  6 08:49:37 1994</c>) - and
/// any other date curl reads, with a year past 9999 kept as curl's 64-bit <c>time_t</c> keeps it
/// (ADR-0410). A value curl does not read is an unknown time, as curl 8.21.0 treats
/// <c>Last-Modified: garbage</c> (measured, BL-178 Notes, ADR-0044). The last
/// <c>Last-Modified</c> header wins, as in curl.
/// </remarks>
internal static class HttpLastModified
{
    private const string HeaderName = "Last-Modified";

    /// <summary>
    /// Finds the time the last <c>Last-Modified</c> header of <paramref name="head" /> gives.
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <returns>
    /// The time in seconds since the Unix epoch, or <see langword="null" /> when there is no
    /// <c>Last-Modified</c> header or curl would not read its value as a date.
    /// </returns>
    internal static long? Find(HttpResponseHead head)
    {
        string? value = head.Headers
            .LastOrDefault(header => string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase))?.Value;
        return value is not null && CurlDateParser.TryParse(value, out long unixSeconds) ? unixSeconds : null;
    }
}
