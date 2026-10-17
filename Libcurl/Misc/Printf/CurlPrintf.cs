using System.Text;

namespace Libcurl.Misc.Printf;

/// <summary>
/// libcurl's deprecated printf family (ADR-0668): <c>curl_mprintf</c>, <c>curl_mfprintf</c>,
/// <c>curl_msprintf</c>, <c>curl_msnprintf</c> and <c>curl_maprintf</c>, formatting exactly as curl
/// 8.22.0's <c>lib/mprintf.c</c> does - its conversions (<c>d i u o x X c s S p n f F e E g G</c>), flags
/// (<c>- + space # 0</c>), length modifiers (<c>h l ll L q z O</c>, and <c>I I32 I64</c> on Windows),
/// widths and precisions from the format or from <c>*</c> arguments, positional arguments
/// (<c>%2$d</c>), and its limits of 128 arguments and 128 segments.
/// </summary>
/// <remarks>
/// <para>
/// Each argument is read as the C type its conversion names, never as its .NET type: an integer
/// conversion takes any .NET integer or <see cref="char"/> and keeps the bits C's <c>va_arg</c> would
/// (<c>%d</c> 32 bits, <c>%ld</c> 32 on Windows and 64 elsewhere, <c>%lld</c>, <c>%zd</c> and
/// <c>%Od</c> 64), so <c>%u</c> of -1 is 4294967295. A floating conversion takes a <see cref="double"/>
/// or <see cref="float"/>; <c>%s</c> a <see cref="string"/> (its UTF-8 bytes), a <see cref="byte"/>
/// array (to its first NUL) or <see langword="null"/>; <c>%p</c> an integer or <see langword="null"/>;
/// and <c>%n</c> a <see cref="System.Runtime.CompilerServices.StrongBox{T}"/> of <see cref="long"/>,
/// <see cref="int"/> or <see cref="short"/>, given the count of bytes written so far. Where C's
/// behaviour is undefined - too few arguments, or one of a type its conversion cannot take - these
/// throw <see cref="ArgumentException"/>.
/// </para>
/// <para>
/// A format <c>lib/mprintf.c</c> refuses (a positional and a sequential conversion mixed, an argument
/// left unused, more than 128 arguments or segments) prints nothing and answers 0. The format is read
/// as UTF-8 bytes up to its first NUL, and every count is in bytes. The <c>v</c> forms
/// (<c>curl_mvprintf</c> and the rest) are these same methods given an argument array.
/// </para>
/// </remarks>
public static class CurlPrintf
{
    // DYN_APRINTF: curl_maprintf's dynamic buffer refuses to grow to 8,000,000 bytes with its NUL.
    private const int AllocatedPrintLimit = 7_999_999;

    /// <summary><c>curl_mprintf</c>: formats to standard output.</summary>
    /// <param name="format">The format.</param>
    /// <param name="arguments">The arguments it reads.</param>
    /// <returns>How many bytes were written.</returns>
    public static int MPrintf(string format, params object?[] arguments)
    {
        using Stream standardOutput = Console.OpenStandardOutput();
        return MFPrintf(standardOutput, format, arguments);
    }

    /// <summary><c>curl_mfprintf</c>: formats to <paramref name="stream"/>, the <c>FILE *</c>.</summary>
    /// <param name="stream">Where the bytes go.</param>
    /// <param name="format">The format.</param>
    /// <param name="arguments">The arguments it reads.</param>
    /// <returns>How many bytes were written.</returns>
    public static int MFPrintf(Stream stream, string format, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Render(PrintfPlatform.Current, new PrintfStreamOutput(stream), format, arguments).Count;
    }

    /// <summary>
    /// <c>curl_msprintf</c>: formats into <paramref name="buffer"/> and ends the text with a NUL byte.
    /// </summary>
    /// <param name="buffer">The buffer; it must hold the text and its NUL.</param>
    /// <param name="format">The format.</param>
    /// <param name="arguments">The arguments it reads.</param>
    /// <returns>How many bytes were written, not counting the NUL.</returns>
    /// <exception cref="ArgumentException">
    /// The buffer cannot hold the text and its NUL, which in C writes past its end.
    /// </exception>
    public static int MSPrintf(Span<byte> buffer, string format, params object?[] arguments)
    {
        var output = new PrintfMemoryOutput(buffer.Length);
        int count = Render(PrintfPlatform.Current, output, format, arguments).Count;
        if (count == buffer.Length)
        {
            throw new ArgumentException("The buffer cannot hold the formatted text and its NUL.", nameof(buffer));
        }

        output.Written.CopyTo(buffer);
        buffer[count] = 0;
        return count;
    }

    /// <summary>
    /// <c>curl_msnprintf</c>: formats into <paramref name="buffer"/>, whose length is the
    /// <c>maxlength</c>, keeping at most its length less one bytes of the text and ending them with a NUL
    /// byte (an empty buffer is left as it is).
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="format">The format.</param>
    /// <param name="arguments">The arguments it reads.</param>
    /// <returns>How many bytes of the text the buffer holds, not counting the NUL.</returns>
    public static int MSNPrintf(Span<byte> buffer, string format, params object?[] arguments)
    {
        var output = new PrintfMemoryOutput(buffer.Length);
        int count = Render(PrintfPlatform.Current, output, format, arguments).Count;
        output.Written.CopyTo(buffer);
        if (buffer.IsEmpty)
        {
            return count;
        }

        // Full: the last byte kept gives way to the NUL.
        int end = count == buffer.Length ? count - 1 : count;
        buffer[end] = 0;
        return end;
    }

    /// <summary><c>curl_maprintf</c>: formats into a new string.</summary>
    /// <param name="format">The format.</param>
    /// <param name="arguments">The arguments it reads.</param>
    /// <returns>
    /// The text, read as UTF-8; or <see langword="null"/> when it is 8,000,000 bytes or longer, where
    /// libcurl's buffer (with the text's NUL) refuses to grow.
    /// </returns>
    public static string? MAPrintf(string format, params object?[] arguments)
    {
        var output = new PrintfMemoryOutput(AllocatedPrintLimit);
        Render(PrintfPlatform.Current, output, format, arguments);
        return output.Stopped ? null : Encoding.UTF8.GetString(output.Written);
    }

    /// <summary>Formats into <paramref name="output"/> as <paramref name="platform"/>'s libcurl does.</summary>
    /// <param name="platform">The platform whose C type widths and length modifiers apply.</param>
    /// <param name="output">Where the bytes go.</param>
    /// <param name="format">The format.</param>
    /// <param name="arguments">The arguments it reads.</param>
    /// <returns><paramref name="output"/>.</returns>
    internal static PrintfOutput Render(PrintfPlatform platform, PrintfOutput output, string format, object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(arguments);
        byte[] bytes = Encoding.UTF8.GetBytes(format);
        int end = Array.IndexOf(bytes, (byte)0);
        PrintfFormat? parsed = PrintfFormatParser.Parse(end < 0 ? bytes : bytes[..end], platform);
        if (parsed is not null)
        {
            PrintfRenderer.Render(parsed, PrintfArguments.Read(parsed, arguments, platform), output);
        }

        return output;
    }
}
