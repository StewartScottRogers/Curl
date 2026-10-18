using System.Globalization;
using System.Numerics;

namespace Libcurl.Misc.Printf;

/// <summary>
/// A non-negative finite double's exact decimal value, 0.<see cref="Digits"/> × 10^<see cref="PointPosition"/>,
/// which C's <c>printf</c> rounds half to even to print: every double is a whole number over a power of
/// two, so it has a finite decimal expansion.
/// </summary>
/// <param name="Digits">The significant digits, with no leading or trailing zero; empty for zero.</param>
/// <param name="PointPosition">How many digits come before the decimal point (negative for leading zeros after it).</param>
internal readonly record struct ExactDecimal(string Digits, int PointPosition)
{
    private const int MantissaBits = 52;
    private const int ExponentBias = 1075;

    /// <summary>Gets a value indicating whether the value is zero.</summary>
    public bool IsZero => Digits.Length == 0;

    /// <summary>The exact decimal value of <paramref name="magnitude"/>.</summary>
    /// <param name="magnitude">A finite double, at least zero.</param>
    /// <returns>Its exact value.</returns>
    public static ExactDecimal FromDouble(double magnitude)
    {
        long bits = BitConverter.DoubleToInt64Bits(magnitude);
        int exponentBits = (int)(bits >> MantissaBits);
        long mantissa = bits & ((1L << MantissaBits) - 1);
        int exponent = exponentBits == 0 ? 1 - ExponentBias : exponentBits - ExponentBias;
        mantissa |= exponentBits == 0 ? 0 : 1L << MantissaBits;
        if (mantissa == 0)
        {
            return new ExactDecimal(string.Empty, 0);
        }

        // m × 2^e is m × 2^e exactly for e ≥ 0, and m × 5^-e over 10^-e for e < 0.
        BigInteger whole = exponent >= 0 ? new BigInteger(mantissa) << exponent : mantissa * BigInteger.Pow(5, -exponent);
        string text = whole.ToString(CultureInfo.InvariantCulture);
        return new ExactDecimal(text.TrimEnd('0'), text.Length + Math.Min(exponent, 0));
    }

    /// <summary>The value rounded half to even to its first <paramref name="keep"/> significant digits.</summary>
    /// <param name="keep">How many leading digits to keep; zero or less rounds to the digit before them.</param>
    /// <returns>The rounded value.</returns>
    public ExactDecimal Round(int keep)
    {
        if (keep >= Digits.Length)
        {
            return this;
        }

        if (keep < 0)
        {
            return new ExactDecimal(string.Empty, 0);
        }

        string head = Digits[..keep];
        if (!RoundsUp(keep))
        {
            return new ExactDecimal(head.TrimEnd('0'), PointPosition);
        }

        string unchanged = head.TrimEnd('9');
        return unchanged.Length == 0
            ? new ExactDecimal("1", PointPosition + 1)
            : new ExactDecimal(unchanged[..^1] + (char)(unchanged[^1] + 1), PointPosition);
    }

    /// <summary>The <paramref name="count"/> digits from position <paramref name="start"/>, zeros outside <see cref="Digits"/>.</summary>
    /// <param name="start">The first digit's position; 0 is the first significant digit.</param>
    /// <param name="count">How many digits.</param>
    /// <returns>The digits.</returns>
    public string DigitsAt(int start, int count)
    {
        var digits = new char[Math.Max(count, 0)];
        for (int offset = 0; offset < digits.Length; offset++)
        {
            int position = start + offset;
            digits[offset] = position >= 0 && position < Digits.Length ? Digits[position] : '0';
        }

        return new string(digits);
    }

    // Digits holds no trailing zero, so any digit after a '5' makes it more than half.
    private bool RoundsUp(int keep)
    {
        char next = Digits[keep];
        if (next != '5')
        {
            return next > '5';
        }

        return Digits.Length > keep + 1 || (keep > 0 && (Digits[keep - 1] - '0') % 2 == 1);
    }
}
