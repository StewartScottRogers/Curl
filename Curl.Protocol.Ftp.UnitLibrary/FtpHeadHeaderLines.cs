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

    private const int TimestampDigits = 14;

    /// <summary>The line written for a <c>213</c> reply to <c>SIZE</c>.</summary>
    /// <param name="size">The <c>SIZE</c> count.</param>
    /// <returns>The header line.</returns>
    internal static string ContentLength(long size) =>
        string.Create(CultureInfo.InvariantCulture, $"Content-Length: {size}\r\n");

    /// <summary>
    /// The line written for a <c>213</c> reply to <c>MDTM</c> whose text starts with a
    /// <c>YYYYMMDDHHMMSS</c> timestamp that names a real UTC time.
    /// </summary>
    /// <param name="reply">The reply's last line, such as <c>213 20260927123456</c>.</param>
    /// <returns>
    /// The header line, or <see langword="null" /> when the reply carries no timestamp, as
    /// curl then writes none.
    /// </returns>
    internal static string? LastModified(FtpReply reply)
    {
        string text = reply.LastLine[4..];
        if (reply.Code != 213
            || text.Length < TimestampDigits
            || !DateTime.TryParseExact(text[..TimestampDigits], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime modified))
        {
            return null;
        }

        return modified.ToString("'Last-Modified: 'ddd, dd MMM yyyy HH:mm:ss' GMT'", CultureInfo.InvariantCulture) + "\r\n";
    }
}
