namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Reads a number from an SCP <c>C</c> line as libssh2 1.11.1 reads the mode and the size,
/// with C's <c>strtol</c> and <c>strtoll</c>: leading white space, an optional sign, then
/// at least one digit and nothing after the digits; a value too large is clamped.
/// </summary>
internal static class ScpHeaderNumber
{
    // The magnitude of long.MinValue, the largest a clamped value can need.
    private static readonly UInt128 MagnitudeLimit = (UInt128)long.MaxValue + 1;

    /// <summary>
    /// Parses <paramref name="text" /> in <paramref name="radix" />.
    /// </summary>
    /// <param name="text">The field, such as <c>0644</c> or <c>11</c>.</param>
    /// <param name="radix">8 for a mode, 10 for a size.</param>
    /// <param name="value">The number, clamped to <see cref="long" />'s range; 0 when the text is not a number.</param>
    /// <returns>Whether the whole field is a number.</returns>
    internal static bool TryParse(string text, int radix, out long value)
    {
        ReadOnlySpan<char> digits = text.AsSpan().TrimStart(" \t\n\v\f\r");
        bool negative = digits.StartsWith('-');
        if (negative || digits.StartsWith('+'))
        {
            digits = digits[1..];
        }

        value = 0;
        if (digits.IsEmpty || !TryReadMagnitude(digits, radix, out UInt128 magnitude))
        {
            return false;
        }

        value = negative ? (long)-(Int128)magnitude : (long)UInt128.Min(magnitude, long.MaxValue);
        return true;
    }

    private static bool TryReadMagnitude(ReadOnlySpan<char> digits, int radix, out UInt128 magnitude)
    {
        magnitude = 0;
        foreach (char digit in digits)
        {
            int digitValue = digit - '0';
            if (digitValue < 0 || digitValue >= radix)
            {
                return false;
            }

            magnitude = UInt128.Min((magnitude * (uint)radix) + (uint)digitValue, MagnitudeLimit);
        }

        return true;
    }
}
