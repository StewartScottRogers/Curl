namespace Libcurl.Misc.Printf;

/// <summary>Writes an integer or character as <c>lib/mprintf.c</c>'s <c>out_number</c> does.</summary>
internal static class PrintfNumberWriter
{
    // out_number's work buffer holds this many digits; a longer precision is cut to fit.
    private const int DigitRoom = 325;
    private const string LowerDigits = "0123456789abcdef";
    private const string UpperDigits = "0123456789ABCDEF";

    /// <summary>Writes <paramref name="number"/>, or for <c>%c</c> the byte it holds.</summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="flags">The conversion's flags.</param>
    /// <param name="width">The field width.</param>
    /// <param name="precision">The minimum digit count, or -1 for none given.</param>
    /// <param name="number">The value's bits, read as unsigned.</param>
    /// <param name="signedNumber">The value, for a signed decimal conversion.</param>
    public static void Write(PrintfOutput output, PrintfFlags flags, int width, int precision, ulong number, long signedNumber)
    {
        if ((flags & PrintfFlags.Character) != 0)
        {
            WriteCharacter(output, flags, width, (byte)number);
            return;
        }

        int numberBase = BaseOf(flags);
        bool negative = (flags & (PrintfFlags.Octal | PrintfFlags.Hex | PrintfFlags.Unsigned)) == 0 && signedNumber < 0;
        ulong magnitude = negative ? (ulong)(-(signedNumber + 1)) + 1 : number;
        string digits = FormatDigits(magnitude, numberBase, flags, ref width, precision == -1 ? 1 : precision);
        string prefix = SignOf(flags, negative) + HexPrefixOf(flags, numberBase);
        WriteField(output, flags, width - prefix.Length, prefix, digits);
    }

    private static int BaseOf(PrintfFlags flags) =>
        (flags & PrintfFlags.Octal) != 0 ? 8 : (flags & PrintfFlags.Hex) != 0 ? 16 : 10;

    private static void WriteCharacter(PrintfOutput output, PrintfFlags flags, int width, byte character)
    {
        bool left = (flags & PrintfFlags.Left) != 0;
        if (!left)
        {
            output.PutRepeated((byte)' ', width - 1);
        }

        output.Put(character);
        if (left)
        {
            output.PutRepeated((byte)' ', width - 1);
        }
    }

    private static string FormatDigits(
        ulong magnitude, int numberBase, PrintfFlags flags, ref int width, int precision)
    {
        string digits = DigitsOf(magnitude, numberBase, flags);
        width -= digits.Length;
        precision -= digits.Length;
        if ((flags & PrintfFlags.Alternate) != 0 && numberBase == 8 && precision <= 0)
        {
            digits = "0" + digits;
            width--;
        }

        if (precision > 0)
        {
            width -= precision;
            digits = new string('0', Math.Min(precision, DigitRoom - digits.Length)) + digits;
        }

        return digits;
    }

    // No digits at all for zero: the default precision of 1 is what prints its "0".
    private static string DigitsOf(ulong magnitude, int numberBase, PrintfFlags flags)
    {
        string digitSet = (flags & PrintfFlags.Upper) != 0 ? UpperDigits : LowerDigits;
        string digits = string.Empty;
        for (ulong rest = magnitude; rest > 0; rest /= (ulong)numberBase)
        {
            digits = digitSet[(int)(rest % (ulong)numberBase)] + digits;
        }

        return digits;
    }

    private static string SignOf(PrintfFlags flags, bool negative) =>
        negative ? "-"
            : (flags & PrintfFlags.ShowSign) != 0 ? "+"
            : (flags & PrintfFlags.Space) != 0 ? " "
            : string.Empty;

    private static string HexPrefixOf(PrintfFlags flags, int numberBase)
    {
        if ((flags & PrintfFlags.Alternate) == 0 || numberBase != 16)
        {
            return string.Empty;
        }

        return (flags & PrintfFlags.Upper) != 0 ? "0X" : "0x";
    }

    private static void WriteField(PrintfOutput output, PrintfFlags flags, int width, string prefix, string digits)
    {
        bool left = (flags & PrintfFlags.Left) != 0;
        bool zeros = (flags & PrintfFlags.PadWithZeros) != 0;
        if (!left && !zeros)
        {
            output.PutRepeated((byte)' ', width);
        }

        output.Put(System.Text.Encoding.ASCII.GetBytes(prefix));
        if (!left && zeros)
        {
            output.PutRepeated((byte)'0', width);
        }

        output.Put(System.Text.Encoding.ASCII.GetBytes(digits));
        if (left)
        {
            output.PutRepeated((byte)' ', width);
        }
    }
}
