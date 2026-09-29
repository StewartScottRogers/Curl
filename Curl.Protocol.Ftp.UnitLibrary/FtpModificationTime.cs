using System.Globalization;

namespace Curl.Protocol.Ftp;

/// <summary>
/// Reads the modification time a reply to <c>MDTM</c> carries, as curl 8.21.0 reads it for
/// <c>-z</c>, <c>-R</c> and <c>-I</c> (BL-438, BL-637).
/// </summary>
internal static class FtpModificationTime
{
    /// <summary>Where the timestamp starts: after the code and one space.</summary>
    private const int TimestampStart = 4;

    private const int TimestampDigits = 14;

    /// <summary>
    /// The UTC time a <c>213</c> reply whose text starts with a <c>YYYYMMDDHHMMSS</c>
    /// timestamp names; any fraction of a second after it is ignored.
    /// </summary>
    /// <param name="reply">The reply to <c>MDTM</c>, such as <c>213 20260927123456</c>.</param>
    /// <returns>
    /// The time, or <see langword="null" /> for any other reply, a short timestamp or one that
    /// names no real time, all of which curl treats as unknown.
    /// </returns>
    internal static DateTimeOffset? Of(FtpReply reply) =>
        reply.Code == 213
            && reply.LastLine.Length >= TimestampStart + TimestampDigits
            && DateTime.TryParseExact(
                reply.LastLine.AsSpan(TimestampStart, TimestampDigits),
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime modified)
            ? new DateTimeOffset(modified, TimeSpan.Zero)
            : null;
}
