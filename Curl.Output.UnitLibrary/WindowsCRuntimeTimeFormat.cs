using System.Globalization;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Formats the current time for a <c>-w</c> <c>%time{format}</c> directive as the Windows
/// curl 8.21.0 (mingw, Schannel) does (<see cref="WriteOutTimeDialect.WindowsCRuntime"/>): the time is UTC, curl itself replaces <c>%f</c> with
/// the microseconds, <c>%z</c> with <c>+0000</c> and <c>%Z</c> with <c>UTC</c>, and the
/// rest goes to the C runtime's <c>strftime</c>.
/// </summary>
/// <remarks>
/// <para>
/// The conversions are the ones that <c>strftime</c> accepts: <c>%a %A %b %B %c %d %H %I %j
/// %m %M %p %s %S %U %w %W %x %X %y %Y %%</c>, each also with the <c>#</c> flag, which drops
/// leading zeros from a number, turns <c>%c</c> and <c>%x</c> into the long date, and turns
/// <c>%z</c> and <c>%Z</c> into the local time zone's standard name. Any other conversion,
/// <c>%#f</c>, <c>%#s</c>, or a <c>%</c> or <c>%#</c> ending the format makes the whole
/// format render nothing, as does a result of 256 bytes or more, which does not fit curl's
/// buffer. Names and the <c>%c</c>, <c>%x</c> and <c>%X</c> layouts are the United States
/// English ones. Measured on 2026-09-26; see ADR-0038.
/// </para>
/// </remarks>
internal static class WindowsCRuntimeTimeFormat
{
    private const int OutputBufferBytes = 256;
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
    private const string DateAndTimeLayout = "M/d/yyyy h:mm:ss tt";
    private const string LongDateLayout = "dddd, MMMM d, yyyy";
    private const string TimeLayout = "h:mm:ss tt";

    private static readonly Dictionary<char, Func<Moment, string>> Conversions = new()
    {
        ['a'] = moment => moment.Format("ddd"),
        ['A'] = moment => moment.Format("dddd"),
        ['b'] = moment => moment.Format("MMM"),
        ['B'] = moment => moment.Format("MMMM"),
        ['c'] = moment => moment.Format(DateAndTimeLayout),
        ['d'] = moment => moment.Format("dd"),
        ['f'] = moment => FormatNumber(moment.UtcNow.Ticks % TimeSpan.TicksPerSecond / TicksPerMicrosecond, "D6"),
        ['H'] = moment => moment.Format("HH"),
        ['I'] = moment => moment.Format("hh"),
        ['j'] = moment => FormatNumber(moment.UtcNow.DayOfYear, "D3"),
        ['m'] = moment => moment.Format("MM"),
        ['M'] = moment => moment.Format("mm"),
        ['p'] = moment => moment.Format("tt"),
        ['s'] = moment => FormatNumber(new DateTimeOffset(moment.UtcNow).ToUnixTimeSeconds(), "D"),
        ['S'] = moment => moment.Format("ss"),
        ['U'] = moment => FormatNumber(WeekOfYear(moment.UtcNow, (int)moment.UtcNow.DayOfWeek), "D2"),
        ['w'] = moment => FormatNumber((int)moment.UtcNow.DayOfWeek, "D"),
        ['W'] = moment => FormatNumber(WeekOfYear(moment.UtcNow, ((int)moment.UtcNow.DayOfWeek + 6) % 7), "D2"),
        ['x'] = moment => moment.Format("M/d/yyyy"),
        ['X'] = moment => moment.Format(TimeLayout),
        ['y'] = moment => moment.Format("yy"),
        ['Y'] = moment => moment.Format("yyyy"),
        ['z'] = _ => "+0000",
        ['Z'] = _ => "UTC",
        ['%'] = _ => "%",
    };

    private static readonly Dictionary<char, Func<Moment, string>> AlternateConversions = new()
    {
        ['a'] = Conversions['a'],
        ['A'] = Conversions['A'],
        ['b'] = Conversions['b'],
        ['B'] = Conversions['B'],
        ['c'] = moment => moment.Format(LongDateLayout + " " + TimeLayout),
        ['d'] = WithoutLeadingZeros(Conversions['d']),
        ['H'] = WithoutLeadingZeros(Conversions['H']),
        ['I'] = WithoutLeadingZeros(Conversions['I']),
        ['j'] = WithoutLeadingZeros(Conversions['j']),
        ['m'] = WithoutLeadingZeros(Conversions['m']),
        ['M'] = WithoutLeadingZeros(Conversions['M']),
        ['p'] = Conversions['p'],
        ['S'] = WithoutLeadingZeros(Conversions['S']),
        ['U'] = WithoutLeadingZeros(Conversions['U']),
        ['w'] = Conversions['w'],
        ['W'] = WithoutLeadingZeros(Conversions['W']),
        ['x'] = moment => moment.Format(LongDateLayout),
        ['X'] = Conversions['X'],
        ['y'] = WithoutLeadingZeros(Conversions['y']),
        ['Y'] = Conversions['Y'],
        ['z'] = moment => moment.TimeZoneName,
        ['Z'] = moment => moment.TimeZoneName,
        ['%'] = Conversions['%'],
    };

    /// <summary>
    /// Formats the time <paramref name="timeProvider"/> reads now with <paramref name="format"/>,
    /// the text between the braces of <c>%time{…}</c>.
    /// </summary>
    /// <param name="format">The format, without the braces.</param>
    /// <param name="timeProvider">Supplies the current UTC time and the local time zone.</param>
    /// <returns>The formatted time, or an empty string when curl would print nothing.</returns>
    public static string Format(string format, TimeProvider timeProvider)
    {
        Moment moment = new(timeProvider.GetUtcNow().UtcDateTime, timeProvider.LocalTimeZone.StandardName);
        StringBuilder output = new();
        int position = 0;
        while (position < format.Length)
        {
            if (format[position] != '%')
            {
                output.Append(format[position]);
                position++;
            }
            else if (!TryAppendConversion(format, ref position, moment, output))
            {
                return string.Empty;
            }
        }

        return Encoding.UTF8.GetByteCount(output.ToString()) < OutputBufferBytes ? output.ToString() : string.Empty;
    }

    private static bool TryAppendConversion(string format, ref int position, Moment moment, StringBuilder output)
    {
        int conversionIndex = position + 1;
        bool alternate = conversionIndex < format.Length && format[conversionIndex] == '#';
        if (alternate)
        {
            conversionIndex++;
        }

        Dictionary<char, Func<Moment, string>> conversions = alternate ? AlternateConversions : Conversions;
        if (conversionIndex >= format.Length || !conversions.TryGetValue(format[conversionIndex], out Func<Moment, string>? conversion))
        {
            return false;
        }

        output.Append(conversion(moment));
        position = conversionIndex + 1;
        return true;
    }

    private static int WeekOfYear(DateTime date, int daysSinceWeekStart)
    {
        return (date.DayOfYear - 1 + 7 - daysSinceWeekStart) / 7;
    }

    private static string FormatNumber(long value, string format)
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static Func<Moment, string> WithoutLeadingZeros(Func<Moment, string> conversion)
    {
        return moment =>
        {
            string digits = conversion(moment).TrimStart('0');
            return digits.Length == 0 ? "0" : digits;
        };
    }

    /// <summary>The instant being formatted and the local time zone's standard name.</summary>
    private readonly struct Moment(DateTime utcNow, string timeZoneName)
    {
        public DateTime UtcNow { get; } = utcNow;

        public string TimeZoneName { get; } = timeZoneName;

        public string Format(string layout)
        {
            return UtcNow.ToString(layout, CultureInfo.InvariantCulture);
        }
    }
}
