namespace Curl.Cli;

/// <summary>
/// Reads a numeric option value the way curl 8.21.0 does: an optional leading <c>-</c>,
/// then one or more ASCII digits and nothing else, fitting in an <see cref="int"/>.
/// Whitespace, a leading <c>+</c>, hexadecimal and fractions are refused.
/// </summary>
/// <remarks>
/// No option in <see cref="CommandLineOptionTable"/> uses it yet. The refusals were checked
/// against the local curl 8.21.0 on 2026-09-26: <c>--tftp-blksize abc</c> and
/// <c>--tftp-blksize 99999999999</c> are "expected a proper numerical parameter",
/// <c>--tftp-blksize -1</c> is "expected a positive numerical parameter", and
/// <c>--tftp-blksize -0</c> is accepted.
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
