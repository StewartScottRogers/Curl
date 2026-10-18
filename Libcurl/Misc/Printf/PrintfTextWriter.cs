namespace Libcurl.Misc.Printf;

/// <summary>Writes strings and pointers as <c>lib/mprintf.c</c>'s <c>out_string</c> and <c>out_pointer</c> do.</summary>
internal static class PrintfTextWriter
{
    private static readonly byte[] Nil = "(nil)"u8.ToArray();

    /// <summary>
    /// Writes <paramref name="text"/>: at most <paramref name="precision"/> bytes of it, padded to the width
    /// as if it were the precision long, in double quotes for <c>%S</c>; a null string is <c>(nil)</c>
    /// unquoted when the precision allows five bytes and nothing otherwise.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="flags">The conversion's flags.</param>
    /// <param name="width">The field width.</param>
    /// <param name="precision">The most bytes to write, or -1 for all; another negative value is all too.</param>
    /// <param name="text">The string's bytes, or <see langword="null"/>.</param>
    public static void WriteString(PrintfOutput output, PrintfFlags flags, int width, int precision, byte[]? text)
    {
        (byte[] shown, long length, PrintfFlags shownFlags) = text is null
            ? NilString(flags, precision)
            : (text, LengthOf(text, precision), flags);
        int written = (int)Math.Min(length, shown.Length);
        WriteQuotedField(output, shownFlags, width - (int)Math.Min(length, int.MaxValue), shown.AsSpan(0, written));
    }

    // "(nil)", never quoted, when the precision leaves room for it; nothing otherwise.
    private static (byte[] Text, long Length, PrintfFlags Flags) NilString(PrintfFlags flags, int precision) =>
        precision == -1 || precision >= Nil.Length
            ? (Nil, Nil.Length, flags & ~PrintfFlags.Alternate)
            : ([], 0, flags);

    // C reads the precision as a size_t, so a negative one other than -1 is huge.
    private static long LengthOf(byte[] text, int precision) =>
        precision == -1 ? text.Length : precision < 0 ? long.MaxValue : precision;

    /// <summary>
    /// Writes a pointer: <c>%#x</c> of its value, or <c>(nil)</c> for zero, padded on the side opposite to
    /// the one <c>-</c> names, as <c>out_pointer</c> does.
    /// </summary>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="flags">The conversion's flags.</param>
    /// <param name="width">The field width.</param>
    /// <param name="precision">The precision, for a non-null pointer.</param>
    /// <param name="pointer">The pointer's value.</param>
    public static void WritePointer(PrintfOutput output, PrintfFlags flags, int width, int precision, long pointer)
    {
        if (pointer != 0)
        {
            PrintfNumberWriter.Write(output, flags | PrintfFlags.Hex | PrintfFlags.Alternate, width, precision, (ulong)pointer, 0);
            return;
        }

        bool left = (flags & PrintfFlags.Left) != 0;
        if (left)
        {
            output.PutRepeated((byte)' ', width - Nil.Length);
        }

        output.Put(Nil);
        if (!left)
        {
            output.PutRepeated((byte)' ', width - Nil.Length);
        }
    }

    private static void WriteQuotedField(PrintfOutput output, PrintfFlags flags, int padding, ReadOnlySpan<byte> text)
    {
        bool quoted = (flags & PrintfFlags.Alternate) != 0;
        bool left = (flags & PrintfFlags.Left) != 0;
        if (quoted)
        {
            output.Put((byte)'"');
        }

        if (!left)
        {
            output.PutRepeated((byte)' ', padding);
        }

        output.Put(text);
        if (left)
        {
            output.PutRepeated((byte)' ', padding);
        }

        if (quoted)
        {
            output.Put((byte)'"');
        }
    }
}
