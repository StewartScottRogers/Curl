using System.Globalization;
using System.Text;

namespace Libcurl.Misc.Printf;

/// <summary>
/// One argument as <c>lib/mprintf.c</c>'s <c>va_input</c> holds it: an integer's bits (already cut to
/// the C type its conversion reads), a double, or a string's bytes or a <c>%n</c> target.
/// </summary>
/// <param name="Number">An integer or pointer argument's bits.</param>
/// <param name="Floating">A floating-point argument.</param>
/// <param name="Reference">A string's bytes (<see langword="null"/> for a null string), or a <c>%n</c> target.</param>
internal readonly record struct PrintfValue(long Number, double Floating, object? Reference);

/// <summary>
/// Reads the arguments a parsed format uses, each as the C type its conversion names: an integer
/// conversion takes any .NET integer (or <see cref="char"/>) and keeps the bits a C
/// <c>va_arg</c> of that type would (<c>%d</c> 32, <c>%ld</c> 32 on Windows and 64 elsewhere,
/// <c>%lld</c>, <c>%zd</c> and <c>%Od</c> 64), a floating conversion a <see cref="double"/> or
/// <see cref="float"/>, <c>%s</c> a <see cref="string"/> (its UTF-8 bytes), a <see cref="byte"/> array
/// (up to its first NUL) or <see langword="null"/>, and <c>%p</c> an integer or <see langword="null"/>.
/// </summary>
internal static class PrintfArguments
{
    /// <summary>Reads every argument <paramref name="format"/> uses.</summary>
    /// <param name="format">The parsed format.</param>
    /// <param name="arguments">The arguments, in order.</param>
    /// <param name="platform">The platform whose C <c>long</c> width applies.</param>
    /// <returns>One value per argument the format reads.</returns>
    /// <exception cref="ArgumentException">
    /// Fewer arguments than the format reads, or one whose .NET type its conversion cannot take
    /// (where C's behaviour is undefined).
    /// </exception>
    public static PrintfValue[] Read(PrintfFormat format, object?[] arguments, PrintfPlatform platform)
    {
        PrintfInputType[] types = format.InputTypes;
        if (arguments.Length < types.Length)
        {
            throw new ArgumentException(
                $"The format reads {types.Length} arguments but {arguments.Length} were given.", nameof(arguments));
        }

        var values = new PrintfValue[types.Length];
        for (int position = 0; position < types.Length; position++)
        {
            values[position] = ReadValue(types[position], arguments[position], platform);
        }

        return values;
    }

    private static PrintfValue ReadValue(PrintfInputType type, object? argument, PrintfPlatform platform) => type switch
    {
        PrintfInputType.String => new PrintfValue(0, 0, ReadString(argument)),
        PrintfInputType.CountPointer => new PrintfValue(0, 0, argument),
        PrintfInputType.Double or PrintfInputType.LongDouble => new PrintfValue(0, ReadDouble(argument), null),
        PrintfInputType.Pointer => new PrintfValue(argument is null ? 0 : ReadIntegerBits(argument), 0, null),
        _ => new PrintfValue(CutToType(type, ReadIntegerBits(argument), platform), 0, null),
    };

    // Keeps only the bits a C va_arg of the type would: 32 for int and, on Windows, long; 64 otherwise. Width and
    // Precision arguments are ints too.
    private static long CutToType(PrintfInputType type, long bits, PrintfPlatform platform) => type switch
    {
        PrintfInputType.IntUnsigned => (uint)bits,
        PrintfInputType.Long => platform.IsWindows ? (int)bits : bits,
        PrintfInputType.LongUnsigned => platform.IsWindows ? (uint)bits : bits,
        PrintfInputType.LongLong or PrintfInputType.LongLongUnsigned => bits,
        _ => (int)bits,
    };

    private static long ReadIntegerBits(object? argument) => argument switch
    {
        ulong value => unchecked((long)value),
        nuint value => unchecked((long)(ulong)value),
        nint value => value,
        _ => ReadConvertibleInteger(argument),
    };

    // Every .NET integer below 64 unsigned bits, and char, converts to long without loss.
    private static long ReadConvertibleInteger(object? argument)
    {
        if (argument is IConvertible value && value.GetTypeCode() is >= TypeCode.Char and <= TypeCode.Int64)
        {
            return value.ToInt64(CultureInfo.InvariantCulture);
        }

        throw Mismatch(argument, "an integer");
    }

    private static double ReadDouble(object? argument) => argument switch
    {
        double value => value,
        float value => value,
        _ => throw Mismatch(argument, "a double"),
    };

    private static byte[]? ReadString(object? argument) => argument switch
    {
        null => null,
        string text => Encoding.UTF8.GetBytes(text),
        byte[] bytes => Array.IndexOf(bytes, (byte)0) is var end and >= 0 ? bytes[..end] : bytes,
        _ => throw Mismatch(argument, "a string"),
    };

    private static ArgumentException Mismatch(object? argument, string wanted) =>
        new($"The format reads {wanted} where {argument?.GetType().Name ?? "null"} was given.", "arguments");
}
