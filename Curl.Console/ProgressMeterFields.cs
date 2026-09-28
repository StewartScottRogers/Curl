using System.Globalization;

namespace Curl.Console;

/// <summary>
/// Formats and computes the fields of curl's progress meter status line as curl 8.21.0's
/// <c>lib/progress.c</c> does: sizes and speeds six columns wide (<c>max6out</c>), times seven
/// columns wide (<c>time2str</c>), percentages (<c>pgrs_est_percent</c>) and average speeds
/// (<c>trspeed</c>).
/// </summary>
internal static class ProgressMeterFields
{
    /// <summary>The unit letters <c>max6out</c> appends, one for each division by 1024.</summary>
    private const string SizeUnits = "kMGTPE";

    /// <summary>
    /// Formats a byte count or a speed in six columns as <c>max6out</c> does: right-aligned as
    /// it is below 100000, else divided by 1024 until under 1000 and written <c>xx.yyU</c> (up to
    /// 99) or <c>xxx.yU</c>, the fraction truncated, as in <c>195.3k</c> and <c>46.52M</c>.
    /// </summary>
    /// <param name="bytes">The count, never negative.</param>
    /// <returns>Six characters.</returns>
    internal static string Size(long bytes)
    {
        if (bytes < 100000)
        {
            return bytes.ToString(CultureInfo.InvariantCulture).PadLeft(6);
        }

        int unit = 0;
        while (bytes / 1024 >= 1000)
        {
            bytes /= 1024;
            unit++;
        }

        long whole = bytes / 1024;
        long rest = bytes % 1024;

        return whole <= 99
            ? string.Create(CultureInfo.InvariantCulture, $"{whole,2}.{rest * 100 / 1024:D2}{SizeUnits[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{whole,3}.{rest * 10 / 1024}{SizeUnits[unit]}");
    }

    /// <summary>
    /// Formats a number of seconds in seven columns as <c>time2str</c> does: blank for none,
    /// <c>  mm:ss</c> or <c>h:mm:ss</c> under ten hours, <c>HHh MMm</c> up to 99 hours, then
    /// days, months or years.
    /// </summary>
    /// <param name="seconds">The seconds; zero or fewer is blank.</param>
    /// <returns>Seven characters.</returns>
    internal static string Time(long seconds)
    {
        if (seconds <= 0)
        {
            return "       ";
        }

        long hours = seconds / 3600;

        return hours <= 99 ? HoursTime(seconds, hours) : DaysTime(seconds);
    }

    /// <summary>
    /// Computes the percentage of <paramref name="total" /> that <paramref name="current" /> is,
    /// as <c>pgrs_est_percent</c> does: in whole hundredths of a total over 10000, and zero for
    /// no total.
    /// </summary>
    /// <param name="total">The expected size.</param>
    /// <param name="current">The size so far.</param>
    /// <returns>The percentage, truncated.</returns>
    internal static long Percent(long total, long current)
    {
        if (total > 10000)
        {
            return current / (total / 100);
        }

        return total > 0 ? current * 100 / total : 0;
    }

    /// <summary>
    /// Computes an average speed in bytes per second as <c>trspeed</c> does, without
    /// overflowing: under one microsecond the size counts as sent in one.
    /// </summary>
    /// <param name="size">The bytes.</param>
    /// <param name="microseconds">The time they took.</param>
    /// <returns>The speed, capped at <see cref="long.MaxValue" />.</returns>
    internal static long Speed(long size, long microseconds)
    {
        if (microseconds < 1)
        {
            return size * 1000000;
        }

        if (size < long.MaxValue / 1000000)
        {
            return size * 1000000 / microseconds;
        }

        return microseconds >= 1000000 ? size / (microseconds / 1000000) : long.MaxValue;
    }

    /// <summary>Formats a time under 100 hours.</summary>
    /// <param name="seconds">The seconds.</param>
    /// <param name="hours">The whole hours in them.</param>
    /// <returns><c>  mm:ss</c>, <c>h:mm:ss</c> or <c>HHh MMm</c>.</returns>
    private static string HoursTime(long seconds, long hours)
    {
        long minutes = (seconds - (hours * 3600)) / 60;
        if (hours > 9)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{hours}h {minutes:D2}m");
        }

        long secondsLeft = seconds - (hours * 3600) - (minutes * 60);

        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:D2}:{secondsLeft:D2}")
            : string.Create(CultureInfo.InvariantCulture, $"  {minutes:D2}:{secondsLeft:D2}");
    }

    /// <summary>Formats a time of 100 hours or more.</summary>
    /// <param name="seconds">The seconds.</param>
    /// <returns><c>dd ddh hh</c>, then days, months or years right-aligned, or <c>&gt;99999y</c>.</returns>
    private static string DaysTime(long seconds)
    {
        long days = seconds / 86400;
        if (days <= 99)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{days,2}d {(seconds - (days * 86400)) / 3600:D2}h");
        }

        if (days <= 999)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{days,6}d");
        }

        if (days / 30 <= 999)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{days / 30,6}m");
        }

        return days / 365 <= 99999
            ? string.Create(CultureInfo.InvariantCulture, $"{days / 365,6}y")
            : ">99999y";
    }
}
