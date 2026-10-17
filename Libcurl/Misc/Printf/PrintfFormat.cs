namespace Libcurl.Misc.Printf;

/// <summary>The conversion and display flags of one <c>lib/mprintf.c</c> conversion (its <c>FLAGS_*</c>).</summary>
[Flags]
internal enum PrintfFlags
{
    None = 0,
    Space = 1 << 0,
    ShowSign = 1 << 1,
    Left = 1 << 2,
    Alternate = 1 << 3,
    Short = 1 << 4,
    Long = 1 << 5,
    LongLong = 1 << 6,
    LongDouble = 1 << 7,
    PadWithZeros = 1 << 8,
    Unsigned = 1 << 9,
    Octal = 1 << 10,
    Hex = 1 << 11,
    Upper = 1 << 12,
    Width = 1 << 13,
    WidthFromArgument = 1 << 14,
    Precision = 1 << 15,
    PrecisionFromArgument = 1 << 16,
    Character = 1 << 17,
    FloatE = 1 << 18,
    FloatG = 1 << 19,
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
    Width,
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
