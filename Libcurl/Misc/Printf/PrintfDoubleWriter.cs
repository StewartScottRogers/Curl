using System.Globalization;
using System.Text;

namespace Libcurl.Misc.Printf;

/// <summary>
/// Writes a double as <c>lib/mprintf.c</c>'s <c>out_double</c> does: it clamps the width to 325 and the
/// precision to what a 326-byte buffer holds, formats with C's <c>%f</c>, <c>%e</c> or <c>%g</c> (the
/// <c>-</c>, <c>+</c>, space and <c>#</c> flags, never <c>0</c>), and cuts the result to 325 bytes.
/// </summary>
internal static class PrintfDoubleWriter
{
    private const int BufferSize = 326;
    private const int DefaultPrecision = 6;

    /// <summary>Writes <paramref name="number"/>.</summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="flags">The conversion's flags.</param>
    /// <param name="width">The field width.</param>
    /// <param name="precision">The precision, or -1 for C's default of 6.</param>
    /// <param name="number">The value.</param>
    public static void Write(PrintfOutput output, PrintfFlags flags, int width, int precision, double number)
    {
        width = Math.Min(width, BufferSize - 1);
        precision = LimitPrecision(precision, width, number);
        string text = Format(number, flags, width, precision < 0 ? DefaultPrecision : precision);
        output.Put(Encoding.ASCII.GetBytes(text.Length < BufferSize ? text : text[..(BufferSize - 1)]));
    }

    /// <summary>C's <c>snprintf</c> of one <c>%f</c>, <c>%e</c> or <c>%g</c> conversion, uncut.</summary>
    /// <param name="number">The value.</param>
    /// <param name="flags">The conversion's flags: <c>-</c>, <c>+</c>, space, <c>#</c>, upper case and the style.</param>
    /// <param name="width">The field width.</param>
    /// <param name="precision">The precision, at least 0.</param>
    /// <returns>The formatted field.</returns>
    internal static string Format(double number, PrintfFlags flags, int width, int precision)
    {
        string sign = double.IsNegative(number) && !double.IsNaN(number) ? "-"
            : (flags & PrintfFlags.ShowSign) != 0 ? "+"
            : (flags & PrintfFlags.Space) != 0 ? " "
            : string.Empty;
        string field = sign + FormatMagnitude(Math.Abs(number), flags, precision);
        return (flags & PrintfFlags.Left) != 0 ? field.PadRight(width) : field.PadLeft(width);
    }

    // out_double's own limits, applied before the C formatting.
    private static int LimitPrecision(int precision, int width, double number)
    {
        if (precision < 0)
        {
            return -1;
        }

        precision = precision > BufferSize - 1 ? BufferSize - 2 : precision;
        int maxPrecision = width > 0 && precision <= width ? BufferSize - 1 - width : BufferSize - 1;

        // One less for each digit of the integer part after the first, but never below zero.
        maxPrecision -= Math.Min(maxPrecision, CountTenfolds(number));
        return Math.Max(precision > maxPrecision ? maxPrecision - 1 : precision, 0);
    }

    // Infinity divided by ten stays infinity, so a value that is not finite has no integer digits to count.
    private static int CountTenfolds(double number)
    {
        int count = 0;
        for (double value = double.IsFinite(number) ? number : 0; value >= 10.0; value /= 10)
        {
            count++;
        }

        return count;
    }

    private static string FormatMagnitude(double magnitude, PrintfFlags flags, int precision)
    {
        string text = double.IsFinite(magnitude) ? FormatFinite(magnitude, flags, precision)
            : double.IsNaN(magnitude) ? "nan"
            : "inf";
        return (flags & PrintfFlags.Upper) != 0 ? text.ToUpperInvariant() : text;
    }

    private static string FormatFinite(double magnitude, PrintfFlags flags, int precision)
    {
        ExactDecimal exact = ExactDecimal.FromDouble(magnitude);
        bool alternate = (flags & PrintfFlags.Alternate) != 0;
        return (flags & PrintfFlags.FloatE) != 0 ? Exponential(exact, precision, alternate)
            : (flags & PrintfFlags.FloatG) != 0 ? General(exact, precision, alternate)
            : Fixed(exact, precision, alternate);
    }

    private static string Fixed(ExactDecimal exact, int precision, bool alternate)
    {
        ExactDecimal rounded = exact.Round(exact.PointPosition + precision);
        string integerPart = rounded.PointPosition > 0 ? rounded.DigitsAt(0, rounded.PointPosition) : "0";
        string point = precision > 0 || alternate ? "." : string.Empty;
        return integerPart + point + rounded.DigitsAt(rounded.PointPosition, precision);
    }

    private static string Exponential(ExactDecimal exact, int precision, bool alternate)
    {
        ExactDecimal rounded = exact.Round(precision + 1);
        int exponent = rounded.IsZero ? 0 : rounded.PointPosition - 1;
        string mantissa = rounded.DigitsAt(0, precision + 1);
        string point = precision > 0 || alternate ? "." : string.Empty;
        string exponentSign = exponent < 0 ? "-" : "+";
        return mantissa[..1] + point + mantissa[1..] + "e" + exponentSign
            + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
    }

    private static string General(ExactDecimal exact, int precision, bool alternate)
    {
        int significant = Math.Max(precision, 1);
        ExactDecimal rounded = exact.Round(significant);
        int exponent = rounded.IsZero ? 0 : rounded.PointPosition - 1;
        string text = exponent < significant && exponent >= -4
            ? Fixed(exact, significant - 1 - exponent, alternate)
            : Exponential(exact, significant - 1, alternate);
        return alternate ? text : WithoutTrailingZeros(text);
    }

    private static string WithoutTrailingZeros(string text)
    {
        int exponentAt = text.IndexOf('e', StringComparison.Ordinal);
        string mantissa = exponentAt < 0 ? text : text[..exponentAt];
        string exponent = exponentAt < 0 ? string.Empty : text[exponentAt..];
        if (mantissa.Contains('.', StringComparison.Ordinal))
        {
            mantissa = mantissa.TrimEnd('0').TrimEnd('.');
        }

        return mantissa + exponent;
    }
}
