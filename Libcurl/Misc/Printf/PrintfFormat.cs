namespace Libcurl.Misc.Printf;

/// <summary>The conversion and display flags of one <c>lib/mprintf.c</c> conversion (its <c>FLAGS_*</c>).</summary>
[Flags]
internal enum PrintfFlags
{
    None = 0,

    /// <summary>The <c>space</c> flag: a space stands where a positive number's sign would.</summary>
    Space = 1 << 0,
    ShowSign = 1 << 1,
    Left = 1 << 2,
    Alternate = 1 << 3,
    /// <summary>The <c>h</c> length modifier; only <c>%n</c> reads it, storing a 16-bit count.</summary>
    Short = 1 << 4,

    /// <summary>One <c>l</c> (or Windows' <c>I32</c>): a C <c>long</c>.</summary>
    Long = 1 << 5,

    /// <summary><c>ll</c>, <c>q</c>, <c>z</c>, <c>O</c> or Windows' <c>I</c>/<c>I64</c>: a 64-bit integer.</summary>
    LongLong = 1 << 6,

    /// <summary>The <c>L</c> length modifier: a C <c>long double</c> (read here as a double).</summary>
    LongDouble = 1 << 7,
    /// <summary>A leading <c>0</c> in the width, cleared by <see cref="Left"/>.</summary>
    PadWithZeros = 1 << 8,

    /// <summary>The conversion reads an unsigned integer (<c>u o x X</c>).</summary>
    Unsigned = 1 << 9,
    Octal = 1 << 10,
    Hex = 1 << 11,
    /// <summary>Upper-case output: <c>X</c>, <c>E</c>, <c>F</c> and <c>G</c>.</summary>
    Upper = 1 << 12,

    /// <summary>The format gave a literal width.</summary>
    Width = 1 << 13,

    /// <summary>The width is a <c>*</c>; <see cref="PrintfSegment.Width"/> is then an argument index.</summary>
    WidthFromArgument = 1 << 14,

    /// <summary>The format gave a literal precision.</summary>
    Precision = 1 << 15,

    /// <summary>The precision is a <c>.*</c>; <see cref="PrintfSegment.Precision"/> is then an argument index.</summary>
    PrecisionFromArgument = 1 << 16,

    /// <summary>The <c>%c</c> conversion.</summary>
    Character = 1 << 17,

    /// <summary>Exponential style (<c>e E</c>); with neither this nor <see cref="FloatG"/> a double prints fixed.</summary>
    FloatE = 1 << 18,

    /// <summary>General style (<c>g G</c>).</summary>
    FloatG = 1 << 19,

    /// <summary>The segment is format text only, with no conversion after it.</summary>
    TextOnly = 1 << 20,
}

/// <summary>How one argument is read, by the conversion that uses it (<c>lib/mprintf.c</c>'s <c>MTYPE_*</c>).</summary>
internal enum PrintfInputType
{
    String,
    Pointer,
    CountPointer,
    Int,
    Long,
    LongLong,
    IntUnsigned,
    LongUnsigned,
    LongLongUnsigned,
    Double,
    LongDouble,

    /// <summary>An <c>int</c> read for a <c>*</c> width.</summary>
    Width,

    /// <summary>An <c>int</c> read for a <c>.*</c> precision.</summary>
    Precision,
}

/// <summary>
/// One output segment: <see cref="TextLength"/> bytes of the format from <see cref="TextStart"/>, then,
/// unless <see cref="PrintfFlags.TextOnly"/> is set, argument <see cref="Input"/> converted.
/// </summary>
/// <param name="Width">The field width, or the argument index it is read from.</param>
/// <param name="Precision">The precision, or the argument index it is read from.</param>
/// <param name="Flags">The conversion's flags.</param>
/// <param name="Input">The zero-based index of the argument converted.</param>
/// <param name="TextStart">Where the format text before the conversion starts.</param>
/// <param name="TextLength">How many bytes of format text come before the conversion.</param>
internal readonly record struct PrintfSegment(
    int Width, int Precision, PrintfFlags Flags, int Input, int TextStart, int TextLength);

/// <summary>A parsed format: its segments and the type each argument is read as.</summary>
/// <param name="Format">The format's bytes, up to its first NUL.</param>
/// <param name="Segments">The output segments, in order.</param>
/// <param name="InputTypes">The type each argument is read as; its length is how many are read.</param>
internal sealed record PrintfFormat(byte[] Format, IReadOnlyList<PrintfSegment> Segments, PrintfInputType[] InputTypes);
