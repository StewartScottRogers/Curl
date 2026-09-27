using System.Globalization;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads the time a response's <c>Last-Modified</c> header gives: the source of
/// <see cref="Abstractions.TransferResult.SourceLastWriteTimeUtc" /> for <c>-R</c> and the
/// document time <c>-z</c> is compared against.
/// </summary>
/// <remarks>
/// The value is read in the three HTTP-date forms RFC 9110 section 5.6.7 obliges a recipient to
/// accept: IMF-fixdate (<c>Sun, 06 Nov 1994 08:49:37 GMT</c>), RFC 850
/// (<c>Sunday, 06-Nov-94 08:49:37 GMT</c>) and asctime (<c>Sun Nov  6 08:49:37 1994</c>). A value
/// in none of them is an unknown time, as curl 8.21.0 treats <c>Last-Modified: garbage</c>
/// (measured, BL-178 Notes, ADR-0041). The last <c>Last-Modified</c> header wins, as in curl.
/// </remarks>
internal static class HttpLastModified
{
    private const string HeaderName = "Last-Modified";

    private static readonly string[] Formats =
    [
        "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
        "dddd, dd'-'MMM'-'yy HH':'mm':'ss 'GMT'",
        "ddd MMM d HH':'mm':'ss yyyy",
    ];

    /// <summary>
    /// Finds the time the last <c>Last-Modified</c> header of <paramref name="head" /> gives.
    /// </summary>
    /// <param name="head">The final response's head.</param>
    /// <returns>
    /// The time in UTC, or <see langword="null" /> when there is no <c>Last-Modified</c>
    /// header or its value is not an HTTP-date.
    /// </returns>
    internal static DateTimeOffset? Find(HttpResponseHead head)
    {
        string? value = head.Headers
            .LastOrDefault(header => string.Equals(header.Name, HeaderName, StringComparison.OrdinalIgnoreCase))?.Value;
        return DateTimeOffset.TryParseExact(
            value,
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset time)
            ? time
            : null;
    }
}
