using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Formats a <c>-z</c> time for curl's <c>If-Modified-Since</c> or <c>If-Unmodified-Since</c>
/// header. curl writes its <c>time_t</c> with <c>Curl_gmtime</c> and
/// <c>"%s, %02d %s %4d %02d:%02d:%02d GMT"</c>, so a year past 9999 keeps all its digits and
/// the weekday is the one the date falls on, whatever the <c>-z</c> text said (measured,
/// BL-1426 Notes: <c>-z "Mon, 01 Jan 40000 00:00:00 GMT"</c> sends
/// <c>Sat, 01 Jan 40000 00:00:00 GMT</c>). See ADR-0410, decision 2.
/// </summary>
internal static class HttpConditionDate
{
    private const long SecondsPerDay = 86400;

    private static readonly long LastUnixSecondsInRange = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>
    /// Formats <paramref name="timeCondition" />'s time: in RFC 1123 form when it falls in
    /// <see cref="DateTimeOffset" />'s range, and from its <see cref="TimeCondition.ValueUnixSeconds" />
    /// in the same form with the whole year when it is past 9999-12-31 23:59:59 UTC.
    /// </summary>
    /// <param name="timeCondition">The transfer's <c>-z</c> condition.</param>
    /// <returns>The header value, for example <c>Sat, 01 Jan 40000 00:00:00 GMT</c>.</returns>
    internal static string Format(TimeCondition timeCondition) =>
        timeCondition.ValueUnixSeconds > LastUnixSecondsInRange
            ? FormatPastYear9999(timeCondition.ValueUnixSeconds)
            : timeCondition.Value.UtcDateTime.ToString("r", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats Unix seconds after 9999-12-31 23:59:59 UTC in the proleptic Gregorian calendar,
    /// converting the day count to a civil date by 400-year eras of 146097 days.
    /// </summary>
    private static string FormatPastYear9999(long unixSeconds)
    {
        long days = unixSeconds / SecondsPerDay;
        long secondOfDay = unixSeconds % SecondsPerDay;
        long shifted = days + 719468;
        long era = shifted / 146097;
        long dayOfEra = shifted - (era * 146097);
        long yearOfEra = (dayOfEra - (dayOfEra / 1460) + (dayOfEra / 36524) - (dayOfEra / 146096)) / 365;
        long dayOfYear = dayOfEra - ((365 * yearOfEra) + (yearOfEra / 4) - (yearOfEra / 100));
        long marchMonth = ((5 * dayOfYear) + 2) / 153;
        long day = dayOfYear - (((153 * marchMonth) + 2) / 5) + 1;
        long monthIndex = (marchMonth + 2) % 12;
        long year = yearOfEra + (era * 400) + (monthIndex < 2 ? 1 : 0);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{DayNames[(days + 4) % 7]}, {day:00} {MonthNames[monthIndex]} {year} {secondOfDay / 3600:00}:{secondOfDay / 60 % 60:00}:{secondOfDay % 60:00} GMT");
    }
}
