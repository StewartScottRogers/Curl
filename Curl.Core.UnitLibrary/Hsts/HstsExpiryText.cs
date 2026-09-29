using System.Globalization;

namespace Curl.Core.Hsts;

/// <summary>
/// Writes an expiry as curl's HSTS file holds it, <c>yyyyMMdd HH:mm:ss</c> in UTC, as
/// libcurl's <c>hsts_out</c> prints <c>gmtime</c>'s fields with <c>"%d%02d%02d %02d:%02d:%02d"</c>:
/// a year past 9999 is written with all its digits, which no <see cref="DateTime" /> holds.
/// </summary>
internal static class HstsExpiryText
{
    private const long SecondsPerDay = 86_400;

    /// <summary>Days from 0000-03-01 to 1970-01-01 in the proleptic Gregorian calendar.</summary>
    private const long DaysBeforeEpoch = 719_468;

    private const long DaysPerEra = 146_097;

    /// <summary>Writes <paramref name="unixSeconds" />, an instant at or after the Unix epoch.</summary>
    public static string Format(long unixSeconds)
    {
        long days = unixSeconds / SecondsPerDay;
        long secondOfDay = unixSeconds % SecondsPerDay;
        (long year, int month, int day) = CivilDate(days);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{year}{month:00}{day:00} {secondOfDay / 3600:00}:{secondOfDay / 60 % 60:00}:{secondOfDay % 60:00}");
    }

    /// <summary>
    /// The calendar date <paramref name="days" /> after 1970-01-01, by Howard Hinnant's
    /// <c>civil_from_days</c>, counted in 400-year eras that begin on 1 March.
    /// </summary>
    private static (long Year, int Month, int Day) CivilDate(long days)
    {
        long shifted = days + DaysBeforeEpoch;
        long era = shifted / DaysPerEra;
        long dayOfEra = shifted - (era * DaysPerEra);
        long yearOfEra = (dayOfEra - (dayOfEra / 1460) + (dayOfEra / 36524) - (dayOfEra / 146096)) / 365;
        long dayOfYear = dayOfEra - ((365 * yearOfEra) + (yearOfEra / 4) - (yearOfEra / 100));
        long monthFromMarch = ((5 * dayOfYear) + 2) / 153;
        int day = (int)(dayOfYear - (((153 * monthFromMarch) + 2) / 5) + 1);
        int month = (int)(monthFromMarch < 10 ? monthFromMarch + 3 : monthFromMarch - 9);
        return ((yearOfEra + (era * 400)) + (month <= 2 ? 1 : 0), month, day);
    }
}
