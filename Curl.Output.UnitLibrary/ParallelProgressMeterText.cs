using System.Globalization;
using System.Text;

namespace Curl.Output;

/// <summary>
/// The text of curl's combined progress meter for a <c>-Z</c> run, as curl 8.21.0's
/// <c>progress_meter</c> in <c>src/tool_progress.c</c> writes it: one header line, then status
/// lines each starting with a carriage return, sizes five columns wide (<c>max5data</c>) and times
/// eight (<c>time2str</c>). Measured on curl 8.21.0 (Schannel), BL-521 Notes.
/// </summary>
public static class ParallelProgressMeterText
{
    /// <summary>The header line, without its line ending.</summary>
    public const string HeaderLine = "DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed";

    /// <summary>What a percentage curl does not know is written as.</summary>
    private const string UnknownPercent = "--";

    /// <summary>The unit letters <c>max5data</c> uses, one for each division by 1024.</summary>
    private const string SizeUnits = "kMGTP";

    /// <summary>
    /// Formats one status line: a carriage return, then the fields as <c>progress_meter</c>
    /// prints them, ending in five columns that hold the final line's line ending.
    /// </summary>
    /// <param name="figures">The figures drawn.</param>
    /// <param name="finalLineEnding">
    /// The line ending of the run's final line, or <see langword="null" /> for a line drawn while the
    /// run goes on.
    /// </param>
    /// <returns>The line.</returns>
    public static string StatusLine(ParallelProgressFigures figures, string? finalLineEnding)
    {
        StringBuilder line = new("\r");
        line.Append(Percent(figures.DownloadPercent)).Append(' ')
            .Append(Percent(figures.UploadPercent)).Append(' ')
            .Append(Size(figures.Downloaded)).Append(' ')
            .Append(Size(figures.Uploaded)).Append(' ')
            .Append(CultureInfo.InvariantCulture, $"{figures.Transfers,5} {figures.Live,5}  ")
            .Append(Time(figures.TotalSeconds)).Append(' ')
            .Append(Time(figures.SpentSeconds)).Append(' ')
            .Append(Time(figures.LeftSeconds)).Append(' ')
            .Append(Size(figures.Speed)).Append(' ');

        return finalLineEnding is null
            ? line.Append(' ', 5).ToString()
            : line.Append(' ', 4).Append(finalLineEnding).ToString();
    }

    /// <summary>
    /// Formats a byte count or speed in five columns as <c>max5data</c> does: right-aligned below
    /// 100000; then, dividing by 1024 for each unit, <c>xx.yU</c> while under 100 of the unit and
    /// <c>xxxxU</c> while under 10000, as in <c> 292k</c>, <c>71.5M</c> and <c>1589M</c>. Petabytes are
    /// the last unit, and <see cref="long.MaxValue" /> is <c>8191P</c>.
    /// </summary>
    /// <param name="bytes">The count, never negative.</param>
    /// <returns>Five characters.</returns>
    public static string Size(long bytes)
    {
        if (bytes < 100000)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes,5}");
        }

        long unitSize = 1024;
        int unit = 0;
        while (unit < SizeUnits.Length - 1 && bytes >= 10000 * unitSize)
        {
            unitSize *= 1024;
            unit++;
        }

        return unit > 0 && bytes < 100 * unitSize
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes / unitSize,2}.{bytes % unitSize / (unitSize / 10)}{SizeUnits[unit]}")
            : string.Create(CultureInfo.InvariantCulture, $"{bytes / unitSize,4}{SizeUnits[unit]}");
    }

    /// <summary>
    /// Formats a number of seconds in eight columns as <c>time2str</c> does: blank for none,
    /// <c>hh:mm:ss</c> up to 99 hours, <c>ddd hhh</c> up to 999 days, then days alone.
    /// </summary>
    /// <param name="seconds">The seconds; zero or fewer is blank.</param>
    /// <returns>Eight characters.</returns>
    public static string Time(long seconds)
    {
        if (seconds <= 0)
        {
            return new string(' ', 8);
        }

        long hours = seconds / 3600;
        if (hours <= 99)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{hours:D2}:{seconds % 3600 / 60:D2}:{seconds % 60:D2}");
        }

        long days = seconds / 86400;

        return days <= 999
            ? string.Create(CultureInfo.InvariantCulture, $"{days,3}d {hours % 24:D2}h")
            : string.Create(CultureInfo.InvariantCulture, $"{days,7}d");
    }

    /// <summary>
    /// Formats a percentage in three columns, left-aligned, as <c>progress_meter</c> does: <c>--</c>
    /// when unknown, else the number right-aligned in three and cut to three.
    /// </summary>
    /// <param name="percent">The percentage, or <see langword="null" /> when unknown.</param>
    /// <returns>Three characters.</returns>
    private static string Percent(long? percent)
    {
        string text = percent is { } known
            ? string.Create(CultureInfo.InvariantCulture, $"{known,3}")[..3]
            : UnknownPercent;

        return text.PadRight(3);
    }
}
