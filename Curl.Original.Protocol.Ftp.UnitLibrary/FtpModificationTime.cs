namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Reads the modification time a reply to <c>MDTM</c> carries, as curl 8.22.0 reads it for
/// <c>-z</c>, <c>-R</c> and <c>-I</c> (BL-438, BL-637, BL-2904, ADR-0725).
/// </summary>
internal static class FtpModificationTime
{
    /// <summary>Where the timestamp starts: after the code and one space.</summary>
    private const int TimestampStart = 4;

    private const int TimestampDigits = 14;

    /// <summary>The first year curl's date parser accepts: the Gregorian calendar began in 1582.</summary>
    private const int FirstGregorianYear = 1583;

    /// <summary>
    /// The UTC time a <c>213</c> reply whose text starts with a <c>YYYYMMDDHHMMSS</c>
    /// timestamp names; any bytes after the fourteen digits are ignored.
    /// </summary>
    /// <param name="reply">The reply to <c>MDTM</c>, such as <c>213 20260927123456</c>.</param>
    /// <returns>
    /// The time, or <see langword="null" /> for any other reply, a timestamp
    /// <see cref="TryReadDate" /> refuses, month 00, a year before 1583, or a time past
    /// <see cref="DateTimeOffset.MaxValue" />, all of which curl treats as unknown. A day the
    /// month lacks (31 February, day 00) or second 60 rolls over into the next field, as curl's
    /// <c>time2epoch</c> does.
    /// </returns>
    internal static DateTimeOffset? Of(FtpReply reply) =>
        reply.Code == 213
            && reply.LastLine.Length >= TimestampStart
            && TryReadDate(reply.LastLine.AsSpan(TimestampStart), out FtpMdtmDate date)
            ? ToUtc(date)
            : null;

    /// <summary>
    /// Reads the six fields of a <c>YYYYMMDDHHMMSS</c> timestamp as curl's <c>ftp_213_date</c>
    /// does: the first fourteen characters must be ASCII digits, the month at most 12, the day
    /// at most 31, the hour at most 23, the minute at most 59 and the second at most 60;
    /// month 00, day 00, 31 February and anything after the fourteen digits are accepted.
    /// </summary>
    /// <param name="text">The reply text after <c>213 </c>.</param>
    /// <param name="date">The six fields when the text is accepted; otherwise default.</param>
    /// <returns><see langword="true" /> when <c>ftp_213_date</c> returns <c>TRUE</c>.</returns>
    internal static bool TryReadDate(ReadOnlySpan<char> text, out FtpMdtmDate date)
    {
        date = default;
        if (text.Length < TimestampDigits || text[..TimestampDigits].ContainsAnyExceptInRange('0', '9'))
        {
            return false;
        }

        var read = new FtpMdtmDate(
            (TwoDigits(text, 0) * 100) + TwoDigits(text, 2),
            TwoDigits(text, 4),
            TwoDigits(text, 6),
            TwoDigits(text, 8),
            TwoDigits(text, 10),
            TwoDigits(text, 12));
        if (!IsWithinFtp213DateLimits(read))
        {
            return false;
        }

        date = read;
        return true;
    }

    private static bool IsWithinFtp213DateLimits(FtpMdtmDate date) =>
        date.Month <= 12 && date.Day <= 31 && date.Hour <= 23 && date.Minute <= 59 && date.Second <= 60;

    private static int TwoDigits(ReadOnlySpan<char> text, int start) =>
        ((text[start] - '0') * 10) + (text[start + 1] - '0');

    /// <summary>
    /// The UTC time curl's <c>Curl_getdate_capped</c> makes of the fields, or
    /// <see langword="null" /> where it fails or the time is past what
    /// <see cref="DateTimeOffset" /> holds.
    /// </summary>
    private static DateTimeOffset? ToUtc(FtpMdtmDate date)
    {
        if (date.Year < FirstGregorianYear || date.Month == 0)
        {
            return null;
        }

        var monthStart = new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var intoMonth = new TimeSpan(date.Day - 1, date.Hour, date.Minute, date.Second);
        return intoMonth > DateTimeOffset.MaxValue - monthStart ? null : monthStart + intoMonth;
    }
}

/// <summary>The six fields curl's <c>ftp_213_date</c> reads from an <c>MDTM</c> timestamp.</summary>
/// <param name="Year">The year, 0000 to 9999.</param>
/// <param name="Month">The month, 00 to 12.</param>
/// <param name="Day">The day of the month, 00 to 31.</param>
/// <param name="Hour">The hour, 00 to 23.</param>
/// <param name="Minute">The minute, 00 to 59.</param>
/// <param name="Second">The second, 00 to 60.</param>
internal readonly record struct FtpMdtmDate(int Year, int Month, int Day, int Hour, int Minute, int Second);
