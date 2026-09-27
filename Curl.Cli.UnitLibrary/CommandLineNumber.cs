using System.Globalization;

namespace Curl.Cli;

/// <summary>
/// Reads a numeric option value the way curl 8.21.0 does. A decimal value is an optional
/// leading <c>-</c>, then one or more ASCII digits and nothing else, no larger than the
/// C <c>LONG_MAX</c> it is given, which <see cref="CommandLineOptionTable"/> sets to
/// <see cref="PlatformLongMaximum"/>; an octal value is one or more digits <c>0</c>-<c>7</c> and nothing
/// else, with no sign at all. Whitespace, a leading <c>+</c>, hexadecimal and fractions are
/// refused.
/// </summary>
/// <remarks>
/// <para>
/// <c>--create-file-mode</c> in <see cref="CommandLineOptionTable"/> reads octal; checked
/// against the local curl 8.21.0 on 2026-09-26, <c>0</c>, <c>0640</c>, <c>777</c> and
/// <c>00000000777</c> are accepted, <c>8</c>, <c>18</c>, <c>7a</c>, <c>-0</c>, <c>-1</c>,
/// <c>+7</c>, <c>0x7</c> and the empty value are "expected a proper numerical parameter",
/// and <c>1000</c>, <c>1777</c> and <c>10008</c> are "too large number": digits are read
/// until the first that is not octal, a value past the maximum is refused as it is read,
/// and only then is anything left over refused.
/// </para>
/// <para>
/// <c>--tftp-blksize</c> in <see cref="CommandLineOptionTable"/> uses the decimal reading. The refusals were checked
/// against the local curl 8.21.0 on 2026-09-26: <c>--tftp-blksize abc</c> and
/// <c>--tftp-blksize 99999999999</c> are "expected a proper numerical parameter",
/// <c>--tftp-blksize -1</c> is "expected a positive numerical parameter", and
/// <c>--tftp-blksize -0</c> is accepted.
/// </para>
/// <para>
/// A C <c>long</c> is 32 bits on Windows and 64 bits on Linux and macOS, so the ceiling of a
/// <c>long</c>-valued option follows the platform (ADR-0019): measured on 2026-09-26 against the
/// OpenSSL curl 8.18.0 in WSL (Ubuntu), <c>--tftp-blksize</c> and <c>--max-redirs</c> accept
/// <c>9223372036854775807</c> and refuse <c>9223372036854775808</c>, and <c>--connect-timeout</c>
/// and <c>-m</c> accept <c>9223372036854774</c> whole seconds and refuse <c>9223372036854775</c>.
/// Each reader takes the ceiling as an argument so both platforms' readings can be tested on one
/// host; <see cref="CommandLineOptionTable"/> passes <see cref="PlatformLongMaximum"/>.
/// </para>
/// </remarks>
public static class CommandLineNumber
{
    /// <summary>
    /// The largest <see cref="TimeSpan"/> in whole milliseconds; a longer duration is read as
    /// about <see cref="TimeSpan.MaxValue"/>.
    /// </summary>
    private const long MaximumDurationMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// Gets the C <c>LONG_MAX</c> of the curl this process stands in for:
    /// <see cref="LongMaximumFor"/> the operating system it runs on.
    /// </summary>
    public static long PlatformLongMaximum { get; } = LongMaximumFor(OperatingSystem.IsWindows());

    /// <summary>
    /// Gets the most whole seconds the curl this process stands in for accepts for
    /// <c>--connect-timeout</c> and <c>-m</c>: <see cref="MaximumWholeSecondsFor"/>
    /// <see cref="PlatformLongMaximum"/>.
    /// </summary>
    public static long MaximumWholeSeconds { get; } = MaximumWholeSecondsFor(PlatformLongMaximum);

    /// <summary>
    /// Gets a C <c>LONG_MAX</c>: 2^31-1 on Windows, where a <c>long</c> is 32 bits, and 2^63-1
    /// on Linux and macOS, where it is 64 bits.
    /// </summary>
    /// <param name="isWindows">Whether the platform is Windows.</param>
    /// <returns><see cref="int.MaxValue"/> on Windows; otherwise <see cref="long.MaxValue"/>.</returns>
    public static long LongMaximumFor(bool isWindows) => isWindows ? int.MaxValue : long.MaxValue;

    /// <summary>
    /// Gets the most whole seconds curl accepts for <c>--connect-timeout</c> and <c>-m</c> where
    /// a C <c>long</c> holds at most <paramref name="longMaximum"/>: that divided by 1000, less
    /// one. That is 2147482 on Windows and 9223372036854774 on Linux and macOS.
    /// </summary>
    /// <param name="longMaximum">The platform's C <c>LONG_MAX</c>.</param>
    /// <returns>The most whole seconds accepted.</returns>
    public static long MaximumWholeSecondsFor(long longMaximum) => (longMaximum / 1000) - 1;

    /// <summary>
    /// Reads <paramref name="value"/> as a number that is zero or more. <c>-0</c> reads as zero.
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="longMaximum">The platform's C <c>LONG_MAX</c>, the largest number accepted.</param>
    /// <param name="number">The number read; zero when the value is refused.</param>
    /// <returns>
    /// <see langword="null"/> when the value was read; <see cref="CommandLineRefusal.ExpectedProperNumericalParameter"/>
    /// when it is malformed or larger than <paramref name="longMaximum"/>; <see cref="CommandLineRefusal.ExpectedPositiveNumericalParameter"/>
    /// when it is negative.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spelledOption"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static CommandLineRefusal? ParseNonNegative(string spelledOption, string value, long longMaximum, out long number)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(value);

        number = 0;
        bool negative = value.StartsWith('-');
        if (!TryReadDigits(value.AsSpan(negative ? 1 : 0), longMaximum, out long magnitude))
        {
            return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
        }

        if (negative && magnitude != 0)
        {
            return CommandLineRefusal.ExpectedPositiveNumericalParameter(spelledOption);
        }

        number = magnitude;
        return null;
    }

    /// <summary>
    /// Reads <paramref name="value"/> as a whole number of at least <c>-1</c>, as curl 8.21.0 reads
    /// <c>--max-redirs</c>, where <c>-1</c> means no limit: an optional <c>-</c> then decimal digits,
    /// at most <paramref name="longMaximum"/>. <c>-0</c> reads as zero and <c>-01</c> as <c>-1</c>.
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="longMaximum">The platform's C <c>LONG_MAX</c>, the largest number accepted.</param>
    /// <param name="number">The number read; zero when the value is refused.</param>
    /// <returns>
    /// <see langword="null"/> when the value was read; otherwise
    /// <see cref="CommandLineRefusal.ExpectedProperNumericalParameter"/>, whether the value is
    /// malformed, too large or below <c>-1</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spelledOption"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static CommandLineRefusal? ParseMinusOneOrMore(string spelledOption, string value, long longMaximum, out long number)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(value);

        number = 0;
        bool negative = value.StartsWith('-');
        if (!TryReadDigits(value.AsSpan(negative ? 1 : 0), longMaximum, out long magnitude) || (negative && magnitude > 1))
        {
            return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
        }

        number = negative ? -magnitude : magnitude;
        return null;
    }

    /// <summary>
    /// Reads <paramref name="value"/> as an unsigned octal number no larger than <paramref name="maximum"/>.
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="maximum">The largest number accepted.</param>
    /// <param name="number">The number read; zero when the value is refused.</param>
    /// <returns>
    /// <see langword="null"/> when the value was read; <see cref="CommandLineRefusal.TooLargeNumber"/>
    /// when its leading octal digits exceed <paramref name="maximum"/>;
    /// <see cref="CommandLineRefusal.ExpectedProperNumericalParameter"/> when it does not start with an
    /// octal digit or has anything after its octal digits.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spelledOption"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static CommandLineRefusal? ParseOctal(string spelledOption, string value, int maximum, out int number)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(value);

        number = 0;
        int digitCount = value.AsSpan().IndexOfAnyExceptInRange('0', '7');
        ReadOnlySpan<char> digits = digitCount < 0 ? value : value.AsSpan(0, digitCount);
        if (digits.IsEmpty)
        {
            return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
        }

        if (!TryReadOctalDigits(digits, maximum, out int magnitude))
        {
            return CommandLineRefusal.TooLargeNumber(spelledOption);
        }

        if (digits.Length != value.Length)
        {
            return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
        }

        number = magnitude;
        return null;
    }

    /// <summary>
    /// Reads <paramref name="value"/> as a byte offset: one or more ASCII digits and nothing
    /// else, fitting in a <see cref="long"/>. There is no sign at all, so <c>-0</c> is refused
    /// along with <c>-1</c>, as curl 8.21.0 refuses them for <c>-C</c>/<c>--continue-at</c>.
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="offset">The offset read; zero when the value is refused.</param>
    /// <returns>
    /// <see langword="null"/> when the value was read; otherwise
    /// <see cref="CommandLineRefusal.ExpectedProperNumericalParameter"/>, whatever is wrong with it.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spelledOption"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static CommandLineRefusal? ParseOffset(string spelledOption, string value, out long offset)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(value);

        ReadOnlySpan<char> rest = value;
        if (TryReadLeadingDigits(ref rest, out offset) == LeadingDigits.Read && rest.IsEmpty)
        {
            return null;
        }

        offset = 0;
        return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
    }

    /// <summary>
    /// Reads <paramref name="value"/> as a size the way curl 8.21.0 reads <c>--max-filesize</c>:
    /// digits, an optional <c>.</c> and fraction digits, and an optional one-letter unit,
    /// <c>b</c> (bytes, the default), <c>k</c>, <c>m</c>, <c>g</c>, <c>t</c> or <c>p</c> (powers
    /// of 1024), in either case.
    /// </summary>
    /// <remarks>
    /// A fraction needs a unit larger than a byte, and keeps only as many digits as the unit
    /// has decimal places to hold: 3 for <c>k</c>, 6 for <c>m</c>, 9 for <c>g</c>, 12 for
    /// <c>t</c> and 15 for <c>p</c>, so <c>0.3333k</c> is 340 bytes, not 341. The kept digits are
    /// multiplied by the unit and divided, rounding down, unless that product would not fit, when
    /// the unit is divided first, so <c>8191.99999p</c> is 9223372025595734116. Measured against
    /// the local curl 8.21.0 on 2026-09-26, through <c>--libcurl</c> and the exit 63 message.
    /// </remarks>
    /// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="size">The size in bytes; zero when the value is refused.</param>
    /// <returns>
    /// <see langword="null"/> when the value was read;
    /// <see cref="CommandLineRefusal.TooLargeNumber"/> when its whole number, or the size, does
    /// not fit in a <see cref="long"/>; <see cref="CommandLineRefusal.ExpectedProperNumericalParameter"/>
    /// when it does not start with a digit, or a <c>.</c> is not followed by digits that fit;
    /// <see cref="CommandLineRefusal.BadlyUsedHere"/> for any other unit, anything after the unit,
    /// or a fraction of a byte.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spelledOption"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static CommandLineRefusal? ParseSize(string spelledOption, string value, out long size)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(value);

        size = 0;
        ReadOnlySpan<char> rest = value;
        LeadingDigits whole = TryReadLeadingDigits(ref rest, out long wholeUnits);
        if (whole != LeadingDigits.Read)
        {
            return whole == LeadingDigits.TooLarge
                ? CommandLineRefusal.TooLargeNumber(spelledOption)
                : CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
        }

        Fraction fraction = default;
        if (rest.StartsWith('.'))
        {
            rest = rest[1..];
            int lengthBefore = rest.Length;
            if (TryReadLeadingDigits(ref rest, out long fractionDigits) != LeadingDigits.Read)
            {
                return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
            }

            fraction = new Fraction(fractionDigits, lengthBefore - rest.Length);
        }

        return ScaleByUnit(spelledOption, rest, wholeUnits, fraction, out size);
    }

    /// <summary>
    /// Reads <paramref name="value"/> as a duration in seconds the way curl 8.21.0 reads
    /// <c>--connect-timeout</c> and <c>-m</c>/<c>--max-time</c>: whole seconds, then an optional
    /// <c>.</c> and fraction digits kept to the millisecond, rounding down. Anything after the
    /// digits is ignored, so <c>1,5</c> and <c>1e999</c> are one second and <c>0x10</c> is zero.
    /// </summary>
    /// <remarks>
    /// Measured against the local curl 8.21.0 (Windows, where a C <c>long</c> is 32 bits) on
    /// 2026-09-26, through the milliseconds <c>--libcurl</c> writes: <c>3.14</c> is 3140,
    /// <c>1.123456789012</c> is 1123 and <c>0.0001</c> is 0. At most
    /// <see cref="MaximumWholeSecondsFor"/> <paramref name="longMaximum"/> whole seconds are
    /// accepted. A fraction of more than ten digits, or one larger than <c>21474836</c>, drops its
    /// last digits until it is neither, before the milliseconds are taken from it; where a
    /// <c>long</c> is 64 bits curl drops them only past ten digits, which leaves the same
    /// milliseconds, so the one rule serves both (WSL curl 8.18.0: <c>1.12345678901234</c> is
    /// 1123). A duration longer than a <see cref="TimeSpan"/> holds, possible only where a
    /// <c>long</c> is 64 bits, is read as the longest whole milliseconds one holds, about 29,000
    /// years.
    /// </remarks>
    /// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="longMaximum">The platform's C <c>LONG_MAX</c>, from which the most whole seconds follow.</param>
    /// <param name="duration">The duration read; zero when the value is refused.</param>
    /// <returns>
    /// <see langword="null"/> when the value was read;
    /// <see cref="CommandLineRefusal.ExpectedProperNumericalParameter"/> when it does not start with
    /// a digit (so a sign, a space or a leading <c>.</c>) or its whole seconds are more than
    /// <see cref="MaximumWholeSecondsFor"/> <paramref name="longMaximum"/>; <see cref="CommandLineRefusal.TooLargeNumber"/> when a
    /// <c>.</c> is not followed by digits that fit in a <see cref="long"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spelledOption"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static CommandLineRefusal? ParseSeconds(string spelledOption, string value, long longMaximum, out TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(value);

        duration = TimeSpan.Zero;
        ReadOnlySpan<char> rest = value;
        if (TryReadLeadingDigits(ref rest, out long wholeSeconds) != LeadingDigits.Read || wholeSeconds > MaximumWholeSecondsFor(longMaximum))
        {
            return CommandLineRefusal.ExpectedProperNumericalParameter(spelledOption);
        }

        long milliseconds = 0;
        if (rest.StartsWith('.'))
        {
            rest = rest[1..];
            int lengthBefore = rest.Length;
            if (TryReadLeadingDigits(ref rest, out long fractionDigits) != LeadingDigits.Read)
            {
                return CommandLineRefusal.TooLargeNumber(spelledOption);
            }

            milliseconds = FractionInMilliseconds(fractionDigits, lengthBefore - rest.Length);
        }

        // At most 9223372036854774 whole seconds and 999 milliseconds, so the sum fits in a long.
        duration = TimeSpan.FromMilliseconds(Math.Min((wholeSeconds * 1000) + milliseconds, MaximumDurationMilliseconds));
        return null;
    }

    /// <summary>
    /// The milliseconds in a fraction written with <paramref name="digitCount"/> digits, as curl's
    /// <c>secs2ms</c> takes them: digits are dropped from the end while there are more than ten or
    /// the fraction is larger than a 32-bit <c>LONG_MAX</c> divided by 100.
    /// </summary>
    private static long FractionInMilliseconds(long digits, int digitCount)
    {
        while (digitCount > 10 || digits > int.MaxValue / 100)
        {
            digits /= 10;
            digitCount--;
        }

        long divisor = 1;
        for (int digit = 1; digit < digitCount; digit++)
        {
            divisor *= 10;
        }

        return digits * 100 / divisor;
    }

    private static CommandLineRefusal? ScaleByUnit(string spelledOption, ReadOnlySpan<char> unit, long wholeUnits, Fraction fraction, out long size)
    {
        size = 0;
        if (!TryReadUnit(unit, out int decimalPlaces, out long unitBytes) || (fraction.IsGiven && unitBytes == 1))
        {
            return CommandLineRefusal.BadlyUsedHere(spelledOption);
        }

        if (wholeUnits > long.MaxValue / unitBytes)
        {
            return CommandLineRefusal.TooLargeNumber(spelledOption);
        }

        // The fraction is less than one unit, and the whole units are at most long.MaxValue
        // divided by the unit, so the sum always fits.
        size = (wholeUnits * unitBytes) + fraction.InBytes(decimalPlaces, unitBytes);
        return null;
    }

    /// <summary>
    /// Reads a size's unit: nothing or <c>b</c> is bytes, and <c>k</c>, <c>m</c>, <c>g</c>,
    /// <c>t</c> and <c>p</c>, in either case, are successive powers of 1024.
    /// </summary>
    private static bool TryReadUnit(ReadOnlySpan<char> unit, out int decimalPlaces, out long unitBytes)
    {
        int power = unit.Length switch
        {
            0 => 0,
            1 => "bkmgtp".IndexOf(char.ToLowerInvariant(unit[0]), StringComparison.Ordinal),
            _ => -1,
        };
        decimalPlaces = power * 3;
        unitBytes = power < 0 ? 0 : 1L << (power * 10);
        return power >= 0;
    }

    /// <summary>
    /// Reads the run of ASCII digits at the start of <paramref name="rest"/> and moves past it;
    /// leaves <paramref name="rest"/> alone when there is no run or it does not fit.
    /// </summary>
    private static LeadingDigits TryReadLeadingDigits(ref ReadOnlySpan<char> rest, out long number)
    {
        int digitCount = rest.IndexOfAnyExceptInRange('0', '9');
        ReadOnlySpan<char> digits = digitCount < 0 ? rest : rest[..digitCount];
        if (digits.IsEmpty)
        {
            number = 0;
            return LeadingDigits.Missing;
        }

        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return LeadingDigits.TooLarge;
        }

        rest = rest[digits.Length..];
        return LeadingDigits.Read;
    }

    private static bool TryReadOctalDigits(ReadOnlySpan<char> digits, int maximum, out int magnitude)
    {
        magnitude = 0;
        long total = 0;
        foreach (char digit in digits)
        {
            total = (total * 8) + (digit - '0');
            if (total > maximum)
            {
                return false;
            }
        }

        magnitude = (int)total;
        return true;
    }

    private static bool TryReadDigits(ReadOnlySpan<char> digits, long longMaximum, out long magnitude)
    {
        if (digits.IsEmpty
            || digits.ContainsAnyExceptInRange('0', '9')
            || !long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out magnitude)
            || magnitude > longMaximum)
        {
            magnitude = 0;
            return false;
        }

        return true;
    }

    /// <summary>What <see cref="TryReadLeadingDigits"/> found.</summary>
    private enum LeadingDigits
    {
        Missing,
        TooLarge,
        Read,
    }

    /// <summary>The digits after a size's <c>.</c>, as a whole number and how many digits it was written with.</summary>
    private readonly struct Fraction(long digits, int digitCount)
    {
        public bool IsGiven => digitCount > 0;

        /// <summary>
        /// The fraction of one unit in bytes: only the first <paramref name="decimalPlaces"/>
        /// digits count, and the result rounds down.
        /// </summary>
        public long InBytes(int decimalPlaces, long unitBytes)
        {
            long keptDigits = digits;
            long divisor = 1;
            for (int digit = 0; digit < digitCount; digit++)
            {
                if (digit < decimalPlaces)
                {
                    divisor *= 10;
                }
                else
                {
                    keptDigits /= 10;
                }
            }

            return keptDigits <= long.MaxValue / unitBytes
                ? keptDigits * unitBytes / divisor
                : keptDigits * (unitBytes / divisor);
        }
    }
}
