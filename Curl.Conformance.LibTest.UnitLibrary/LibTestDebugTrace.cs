using System.Globalization;
using Curl.Api.Callbacks;

namespace Curl.Conformance.LibTest;

/// <summary>
/// curl 8.22.0's <c>tests/libtest/testtrace.c</c> <c>libtest_debug_cb</c> and <c>debug_dump</c>: a
/// <c>CURLOPT_DEBUGFUNCTION</c> that writes text as <c>== Info: </c> lines and every other kind of
/// data as a hex and text dump, with <c>debug_config</c>'s <c>tracetime</c> and <c>nohex</c> as
/// <paramref name="traceTime"/> and <paramref name="noHex"/>. Bytes are written one character each
/// (Latin-1), so the writer gets exactly the bytes upstream's <c>stderr</c> gets.
/// </summary>
/// <param name="error">Where the trace goes, upstream's <c>stderr</c>.</param>
/// <param name="timeProvider">Where a <paramref name="traceTime"/> prefix's local time comes from.</param>
/// <param name="traceTime">Whether each line starts with the local time, <c>HH:MM:SS.uuuuuu </c>.</param>
/// <param name="noHex">Whether dumps leave out the hex column, 64 bytes a row instead of 16.</param>
public sealed class LibTestDebugTrace(TextWriter error, TimeProvider timeProvider, bool traceTime, bool noHex)
{
    private const int HexWidth = 0x10;
    private const int TextWidth = 0x40;
    private const byte CarriageReturn = 0x0D;
    private const byte LineFeed = 0x0A;

    private readonly TextWriter error = error ?? throw new ArgumentNullException(nameof(error));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary><c>libtest_debug_cb</c>: traces one piece of debug data; always answers 0.</summary>
    /// <param name="type">What kind of data it is; a kind upstream does not name is ignored.</param>
    /// <param name="data">The data.</param>
    /// <returns>0, as upstream's callback returns.</returns>
    public int Trace(CurlInfoType type, ReadOnlySpan<byte> data)
    {
        string? text = DumpTitle(type);
        if (type == CurlInfoType.Text)
        {
            error.Write($"{TimePrefix()}== Info: {Latin1(data)}");
        }
        else if (text is not null)
        {
            DebugDump(TimePrefix(), text, error, data, noHex);
        }

        return 0;
    }

    /// <summary>
    /// <c>debug_dump(timebuf, text, stream, ptr, size, nohex)</c>: a title line
    /// <c>&lt;text&gt;, N bytes (0xN)</c>, then one row per 16 bytes (64 with <paramref name="noHex"/>):
    /// the offset, the hex column unless <paramref name="noHex"/>, and the bytes as text with anything
    /// outside 0x20 to 0x7F as a dot. With <paramref name="noHex"/> a CR LF ends the row early.
    /// </summary>
    /// <param name="timePrefix">What starts the title line: a time, or empty.</param>
    /// <param name="text">The title, such as <c>=&gt; Send header</c>.</param>
    /// <param name="stream">Where the dump goes.</param>
    /// <param name="data">The bytes to dump.</param>
    /// <param name="noHex">Whether to leave out the hex column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timePrefix"/>, <paramref name="text"/> or <paramref name="stream"/> is <see langword="null"/>.</exception>
    public static void DebugDump(string timePrefix, string text, TextWriter stream, ReadOnlySpan<byte> data, bool noHex)
    {
        ArgumentNullException.ThrowIfNull(timePrefix);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(stream);
        int width = noHex ? TextWidth : HexWidth;
        stream.Write($"{timePrefix}{text}, {data.Length} bytes (0x{data.Length:x})\n");
        for (int offset = 0; offset < data.Length;)
        {
            stream.Write($"{offset:x4}: ");
            if (!noHex)
            {
                WriteHexColumn(stream, data, offset, width);
            }

            offset = WriteTextColumn(stream, data, offset, width, noHex);
            stream.Write('\n');
        }

        stream.Flush();
    }

    private static string? DumpTitle(CurlInfoType type) => type switch
    {
        CurlInfoType.HeaderOut => "=> Send header",
        CurlInfoType.DataOut => "=> Send data",
        CurlInfoType.SslDataOut => "=> Send SSL data",
        CurlInfoType.HeaderIn => "<= Recv header",
        CurlInfoType.DataIn => "<= Recv data",
        CurlInfoType.SslDataIn => "<= Recv SSL data",
        _ => null,
    };

    private static void WriteHexColumn(TextWriter stream, ReadOnlySpan<byte> data, int offset, int width)
    {
        for (int column = 0; column < width; column++)
        {
            int index = offset + column;
            stream.Write(index < data.Length ? $"{data[index]:x2} " : "   ");
        }
    }

    // Writes one row's text column and answers where the next row starts: a full width on, or
    // just past a CR LF when noHex ends the row there, as upstream's "i +=" adjustments do.
    private static int WriteTextColumn(TextWriter stream, ReadOnlySpan<byte> data, int offset, int width, bool noHex)
    {
        for (int column = 0; column < width && offset + column < data.Length; column++)
        {
            int index = offset + column;
            if (EndsRowAt(data, index, noHex))
            {
                return index + 2;
            }

            stream.Write(Printable(data[index]));
            if (EndsRowAt(data, index + 1, noHex))
            {
                return index + 3;
            }
        }

        return offset + width;
    }

    private static bool EndsRowAt(ReadOnlySpan<byte> data, int index, bool noHex) =>
        noHex && index + 1 < data.Length && data[index] == CarriageReturn && data[index + 1] == LineFeed;

    private static char Printable(byte value) => value is >= 0x20 and < 0x80 ? (char)value : '.';

    private static string Latin1(ReadOnlySpan<byte> data) => System.Text.Encoding.Latin1.GetString(data);

    private string TimePrefix() =>
        traceTime ? timeProvider.GetLocalNow().ToString("HH:mm:ss.ffffff ", CultureInfo.InvariantCulture) : string.Empty;
}
