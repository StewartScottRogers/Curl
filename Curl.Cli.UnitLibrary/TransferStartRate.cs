namespace Curl.Cli;

/// <summary>
/// Reads a <c>--rate</c> value, <c>N[/[M]U]</c>, into the least time between two serial transfer starts,
/// as curl 8.21.0's <c>set_rate</c> does (measured 2026-09-29, BL-650 Notes): <c>N</c> transfers per
/// <c>M</c> (default 1) units of <c>s</c>, <c>m</c>, <c>h</c> or <c>d</c>, per hour when no <c>/</c> is
/// given, in whole milliseconds rounded down.
/// </summary>
/// <remarks>
/// Only the leading digits of <c>N</c> are read, so <c>2 /s</c> and <c>1x/s</c> are two and one per
/// second; no digit, a sign, a leading blank or a number above <see cref="long.MaxValue"/> is not a
/// proper number, and zero is badly used. After the <c>/</c>, <c>M</c> is read when it is digits that fit
/// a <see cref="long"/>, else taken as one with the unit read from where it began, and anything after the
/// unit letter is ignored (<c>2/sx</c> is two per second). An unknown unit is badly used, with
/// <c>curl: unsupported --rate unit</c>; <c>M</c> times the unit (an unknown unit counting as an hour)
/// above <see cref="long.MaxValue"/> is a too large number, with <c>curl: too large --rate unit</c>; and
/// more transfers than milliseconds in the period is a too large number.
/// </remarks>
internal static class TransferStartRate
{
    private const long MillisecondsPerSecond = 1000;

    private const long MillisecondsPerMinute = 60 * MillisecondsPerSecond;

    private const long MillisecondsPerHour = 60 * MillisecondsPerMinute;

    private const long MillisecondsPerDay = 24 * MillisecondsPerHour;

    /// <summary>Reads <paramref name="value"/> as <see cref="TransferStartRate"/> describes.</summary>
    /// <param name="spelledOption">The option as typed, for the refusal.</param>
    /// <param name="value">The <c>--rate</c> value.</param>
    /// <param name="errorsHidden"><see langword="true"/> when <c>-s</c> without <c>-S</c> hides curl's error messages.</param>
    /// <param name="milliseconds">The least time between two transfer starts, in milliseconds; zero when refused.</param>
    /// <returns>The refusal, or <see langword="null"/> when the value was read.</returns>
    internal static CommandLineRefusal? Parse(string spelledOption, string value, bool errorsHidden, out long milliseconds)
    {
        milliseconds = 0;
        int position = 0;
        if (!TryReadNumber(value, ref position, out long transfers))
        {
            return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
        }

        if (transfers == 0)
        {
            return CommandLineRefusal.BadlyUsedHere(spelledOption);
        }

        int slash = value.IndexOf('/', StringComparison.Ordinal);
        long periodMilliseconds = MillisecondsPerHour;
        if (slash >= 0)
        {
            CommandLineRefusal? periodRefusal = ReadPeriod(spelledOption, value, slash + 1, errorsHidden, out periodMilliseconds);
            if (periodRefusal is not null)
            {
                return periodRefusal;
            }
        }

        if (transfers > periodMilliseconds)
        {
            return CommandLineRefusal.TooLargeNumber(spelledOption);
        }

        milliseconds = periodMilliseconds / transfers;
        return null;
    }

    /// <summary>Reads the <c>[M]U</c> after the <c>/</c> into the period's length in milliseconds.</summary>
    private static CommandLineRefusal? ReadPeriod(string spelledOption, string value, int position, bool errorsHidden, out long periodMilliseconds)
    {
        if (!TryReadNumber(value, ref position, out long units))
        {
            units = 1;
        }

        long? unitMilliseconds = position < value.Length ? UnitMilliseconds(value[position]) : null;
        bool unitTooLarge = long.MaxValue / (unitMilliseconds ?? MillisecondsPerHour) < units;
        periodMilliseconds = unitTooLarge ? 0 : (unitMilliseconds ?? MillisecondsPerHour) * units;

        return unitMilliseconds is null || unitTooLarge
            ? CommandLineRefusal.RequestRateUnitRefused(spelledOption, errorsHidden, unitMilliseconds is null, unitTooLarge)
            : null;
    }

    private static long? UnitMilliseconds(char unit) =>
        unit switch
        {
            's' => MillisecondsPerSecond,
            'm' => MillisecondsPerMinute,
            'h' => MillisecondsPerHour,
            'd' => MillisecondsPerDay,
            _ => null,
        };

    /// <summary>
    /// Reads the ASCII digits at <paramref name="position"/> as curl's <c>curlx_str_number</c> does, moving
    /// past them only when there is at least one and their number fits a <see cref="long"/>.
    /// </summary>
    private static bool TryReadNumber(string text, ref int position, out long number)
    {
        number = 0;
        int end = position;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            int digit = text[end] - '0';
            if (number > (long.MaxValue - digit) / 10)
            {
                return false;
            }

            number = (number * 10) + digit;
            end++;
        }

        bool read = end > position;
        position = end;
        return read;
    }
}
