using System.Runtime.CompilerServices;

namespace Libcurl.Misc.Printf;

/// <summary>
/// Writes a parsed format's segments to an output, as <c>lib/mprintf.c</c>'s <c>formatf</c> does: each
/// segment's text, then its argument converted with the width and precision it names or reads, until
/// the output stops taking bytes.
/// </summary>
internal static class PrintfRenderer
{
    /// <summary>Writes every segment of <paramref name="format"/>.</summary>
    /// <param name="format">The parsed format.</param>
    /// <param name="values">Its arguments, read.</param>
    /// <param name="output">Where the bytes go.</param>
    public static void Render(PrintfFormat format, PrintfValue[] values, PrintfOutput output)
    {
        foreach (PrintfSegment segment in format.Segments)
        {
            output.Put(format.Format.AsSpan(segment.TextStart, segment.TextLength));
            if ((segment.Flags & PrintfFlags.TextOnly) == 0)
            {
                WriteConversion(segment, format.InputTypes[segment.Input], values, output);
            }

            if (output.Stopped)
            {
                return;
            }
        }
    }

    private static void WriteConversion(PrintfSegment segment, PrintfInputType type, PrintfValue[] values, PrintfOutput output)
    {
        (PrintfFlags flags, int width) = ResolveWidth(segment, values);
        int precision = ResolvePrecision(segment, values);
        PrintfValue value = values[segment.Input];
        if (type is >= PrintfInputType.Int and <= PrintfInputType.LongLongUnsigned)
        {
            bool isUnsigned = type >= PrintfInputType.IntUnsigned;
            PrintfNumberWriter.Write(
                output,
                isUnsigned ? flags | PrintfFlags.Unsigned : flags,
                width,
                precision,
                (ulong)value.Number,
                isUnsigned ? 0 : value.Number);
            return;
        }

        WriteOther(type, flags, width, precision, value, output);
    }

    private static void WriteOther(
        PrintfInputType type, PrintfFlags flags, int width, int precision, PrintfValue value, PrintfOutput output)
    {
        switch (type)
        {
            case PrintfInputType.String:
                PrintfTextWriter.WriteString(output, flags, width, precision, (byte[]?)value.Reference);
                break;
            case PrintfInputType.Pointer:
                PrintfTextWriter.WritePointer(output, flags, width, precision, value.Number);
                break;
            case PrintfInputType.Double or PrintfInputType.LongDouble:
                PrintfDoubleWriter.Write(output, flags, width, precision, value.Floating);
                break;
            default:
                // CountPointer: a conversion's own input is never a width or precision, which claim
                // fresh arguments, so nothing else reaches here.
                StoreCount(flags, output.Count, value.Reference);
                break;
        }
    }

    private static (PrintfFlags Flags, int Width) ResolveWidth(PrintfSegment segment, PrintfValue[] values)
    {
        PrintfFlags flags = segment.Flags;
        if ((flags & PrintfFlags.WidthFromArgument) == 0)
        {
            return (flags, segment.Width);
        }

        int width = (int)values[segment.Width].Number;
        if (width >= 0)
        {
            return (flags, width);
        }

        // "A negative field width is taken as a '-' flag followed by a positive field width."
        flags = (flags | PrintfFlags.Left) & ~PrintfFlags.PadWithZeros;
        return (flags, width == int.MinValue ? int.MaxValue : -width);
    }

    private static int ResolvePrecision(PrintfSegment segment, PrintfValue[] values)
    {
        if ((segment.Flags & PrintfFlags.PrecisionFromArgument) != 0)
        {
            // "A negative precision is taken as if the precision were omitted."
            return Math.Max((int)values[segment.Precision].Number, -1);
        }

        return (segment.Flags & PrintfFlags.Precision) != 0 ? segment.Precision : -1;
    }

    private static void StoreCount(PrintfFlags flags, int count, object? target)
    {
        const PrintfFlags lengths = PrintfFlags.Short | PrintfFlags.Long | PrintfFlags.LongLong;
        long stored = (flags & lengths) == PrintfFlags.Short ? (short)count : count;
        switch (target)
        {
            case StrongBox<long> box:
                box.Value = stored;
                break;
            case StrongBox<int> box:
                box.Value = (int)stored;
                break;
            case StrongBox<short> box:
                box.Value = (short)stored;
                break;
            default:
                throw new ArgumentException(
                    "%n takes a StrongBox<long>, StrongBox<int> or StrongBox<short> for the count.", "arguments");
        }
    }
}
