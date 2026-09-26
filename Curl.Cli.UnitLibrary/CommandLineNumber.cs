namespace Curl.Cli;

/// <summary>
/// Reads a numeric option value the way curl 8.21.0 does. A decimal value is an optional
/// leading <c>-</c>, then one or more ASCII digits and nothing else, fitting in an
/// <see cref="int"/>; an octal value is one or more digits <c>0</c>-<c>7</c> and nothing
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
/// </remarks>
public static class CommandLineNumber
{
    /// <summary>
    /// Reads <paramref name="value"/> as a number that is zero or more. <c>-0</c> reads as zero.
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, for naming it in a refusal.</param>
    /// <param name="value">The option's value.</param>
    /// <param name="number">The number read; zero when the value is refused.</param>
    /// <returns>
    /// <see langword="null"/> when the value was read; <see cref="CommandLineRefusal.ExpectedProperNumericalParameter"/>
    /// when it is malformed or too large; <see cref="CommandLineRefusal.ExpectedPositiveNumericalParameter"/>
    /// when it is negative.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="spelledOption"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static CommandLineRefusal? ParseNonNegative(string spelledOption, string value, out int number)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(value);

        number = 0;
        bool negative = value.StartsWith('-');
        if (!TryReadDigits(value.AsSpan(negative ? 1 : 0), out int magnitude))
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

    private static bool TryReadDigits(ReadOnlySpan<char> digits, out int magnitude)
    {
        magnitude = 0;
        if (digits.IsEmpty || digits.ContainsAnyExceptInRange('0', '9'))
        {
            return false;
        }

        long total = 0;
        foreach (char digit in digits)
        {
            total = (total * 10) + (digit - '0');
            if (total > int.MaxValue)
            {
                return false;
            }
        }

        magnitude = (int)total;
        return true;
    }
}
