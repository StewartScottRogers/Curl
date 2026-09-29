using System.Globalization;

namespace Curl.Protocol.Ftp;

/// <summary>
/// The HTTP-style header lines curl 8.21.0 writes for <c>-I</c> on an <c>ftp://</c> file,
/// each ending in CRLF, as measured with <c>Record-CurlExchange.ps1 -Ftp</c> (BL-438).
/// </summary>
internal static class FtpHeadHeaderLines
{
    /// <summary>The line written when <c>REST 0</c> is answered with <c>350</c>.</summary>
    internal const string AcceptRanges = "Accept-ranges: bytes\r\n";

    /// <summary>The line written for a <c>213</c> reply to <c>SIZE</c>.</summary>
    /// <param name="size">The <c>SIZE</c> count.</param>
    /// <returns>The header line.</returns>
    internal static string ContentLength(long size) =>
        string.Create(CultureInfo.InvariantCulture, $"Content-Length: {size}\r\n");

    /// <summary>
    /// The line written for the time a reply to <c>MDTM</c> named
    /// (<see cref="FtpModificationTime" />).
    /// </summary>
    /// <param name="modifiedUtc">The time, or <see langword="null" /> when the reply named none.</param>
    /// <returns>
    /// The header line, or <see langword="null" /> when the time is unknown, as curl then
    /// writes none.
    /// </returns>
    internal static string? LastModified(DateTimeOffset? modifiedUtc) =>
        modifiedUtc?.ToString("'Last-Modified: 'ddd, dd MMM yyyy HH:mm:ss' GMT'\r\n", CultureInfo.InvariantCulture);
}
