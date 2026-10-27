using System.Buffers;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Splits the bytes of a <c>-K</c> / <c>--config</c> file into <see cref="ConfigFileLine"/>s the way
/// curl 8.21.0's <c>parseconfig</c> (<c>src/tool_parsecfg.c</c>) does. It applies nothing; the
/// <c>--config</c> row of <see cref="CommandLineOptionTable"/> applies each line.
/// </summary>
/// <remarks>
/// The rules, ported from the source and each measured with the local curl 8.21.0 on 2026-09-26:
/// <list type="bullet">
/// <item>Every CR LF is read as LF, as a Windows text-mode read does; lines end at LF, and a NUL byte
/// cuts its read chunk short (<see cref="ReadLineBytes"/>). Each line's bytes are read in the given encoding.</item>
/// <item>A line that is empty, all spaces and tabs, or whose first non-blank character is <c>#</c> is skipped
/// and not counted, so line numbers count only option lines.</item>
/// <item>The option runs from the first character of the line, not the first non-blank one (so
/// <c>"  bogus = 1"</c> is the empty option), to a space or tab, or, when the option does not start with
/// <c>-</c>, to <c>=</c> or <c>:</c>.</item>
/// <item>Spaces, tabs and (for an option not starting with <c>-</c>) <c>=</c> and <c>:</c> are skipped
/// before the parameter.</item>
/// <item>A parameter starting with <c>"</c> runs to the next unescaped <c>"</c> or the line's end;
/// <c>\t</c>, <c>\n</c>, <c>\r</c> and <c>\v</c> are the control characters, a backslash before any other
/// character is that character, and anything after the closing quote is ignored. <c>""</c> is an
/// empty parameter.</item>
/// <item>Any other parameter runs to the first white-space character; when anything other than a
/// comment follows it, the line warns <c>&lt;file&gt;:&lt;n&gt; Option '&lt;option&gt;' uses argument with
/// unquoted whitespace. This may cause side-effects. Consider double quotes.</c> One starting with
/// <c>'</c> warns <c>&lt;file&gt;:&lt;n&gt; Option '&lt;option&gt;' uses argument with leading single quote.
/// It is probably a mistake. Consider double quotes.</c> and keeps the quote. An empty unquoted
/// parameter is no parameter.</item>
/// </list>
/// The 10 MiB line limit is <see cref="ReadableLength"/>, and the Windows build's Ctrl-Z end of file
/// is <see cref="TextModeContents"/>.
/// </remarks>
internal static class ConfigFileSyntax
{
    /// <summary>The Ctrl-Z byte a Windows text-mode read takes as the end of the file.</summary>
    private const byte TextModeEndOfFile = 0x1A;

    /// <summary>
    /// The bytes of a config file curl reads: on Windows, where curl 8.22.0 reads a <c>-K</c> file,
    /// <c>-K -</c> and <c>.curlrc</c> in text mode, those before the first Ctrl-Z (0x1A), which ends the
    /// read; elsewhere all of them, a Ctrl-Z being content. Measured with the local curl 8.22.0 on
    /// 2026-10-26 (CGR-113, BL-3642): <c>write-out = "A"</c> CR LF Ctrl-Z <c> bogus = 1</c> writes
    /// <c>A</c> with nothing on stderr, and <c>write-out = "A</c> Ctrl-Z <c>Z"</c> writes <c>A</c>.
    /// </summary>
    /// <param name="contents">The file's bytes.</param>
    /// <param name="readsInWindowsTextMode">Whether the file is read as curl's Windows build reads it.</param>
    /// <returns><paramref name="contents"/>, or its bytes before the first Ctrl-Z.</returns>
    internal static byte[] TextModeContents(byte[] contents, bool readsInWindowsTextMode)
    {
        int endOfFile = readsInWindowsTextMode ?Array.IndexOf(contents, TextModeEndOfFile) : -1;
        return endOfFile < 0 ? contents : contents[..endOfFile];
    }

    /// <summary>
    /// The shortest line, counting its LF, curl 8.21.0 cannot read: its line buffer holds 10 MiB
    /// (10485760 bytes) with the line's terminating NUL. Measured with the local curl 8.21.0 on
    /// 2026-10-12: a comment line of 10485758 bytes and its LF is read, one of 10485759 and its LF
    /// is not, and a last line of 10485759 bytes with no LF is read; a CR before the LF is not counted.
    /// </summary>
    internal const int UnreadableLineLength = 10 * 1024 * 1024;

    /// <summary>
    /// How many of <paramref name="contents"/>'s bytes curl reads before a line it cannot read: all of
    /// them, or the bytes before the first line of <see cref="UnreadableLineLength"/> or more.
    /// </summary>
    /// <param name="contents">The file's bytes.</param>
    /// <returns>The length of the readable start of <paramref name="contents"/>.</returns>
    internal static int ReadableLength(byte[] contents)
    {
        int lineStart = 0;
        while (lineStart < contents.Length)
        {
            int lineFeed = Array.IndexOf(contents, (byte)'\n', lineStart);
            int lineEnd = lineFeed < 0 ? contents.Length : lineFeed + 1;
            if (CountedLineLength(contents, lineStart, lineEnd) >= UnreadableLineLength)
            {
                return lineStart;
            }

            lineStart = lineEnd;
        }

        return contents.Length;
    }

    /// <summary>The line's length as curl's buffer counts it: a CR LF is read as one LF.</summary>
    private static int CountedLineLength(byte[] contents, int lineStart, int lineEnd)
    {
        bool endsInCarriageReturnLineFeed = lineEnd - lineStart >= 2 && contents[lineEnd - 1] == '\n' && contents[lineEnd - 2] == '\r';
        return lineEnd - lineStart - (endsInCarriageReturnLineFeed ? 1 : 0);
    }

    /// <summary>The characters C's <c>isspace</c> accepts, which end an unquoted parameter.</summary>
    private static readonly SearchValues<char> WhiteSpace = SearchValues.Create(" \t\n\v\f\r");

    /// <summary>Splits <paramref name="contents"/> into its option lines.</summary>
    /// <param name="file">The file name as curl shows it in warnings.</param>
    /// <param name="contents">The file's bytes.</param>
    /// <param name="encoding">The encoding the bytes are read in: UTF-8, or on Windows the ANSI code page for a file that is not valid UTF-8 (<see cref="ConfigFileApplier"/>).</param>
    /// <param name="terminalColumns">The width the warnings wrap at: <see cref="WrappedMessage.TerminalColumns"/> of the parse's <c>COLUMNS</c> (BL-2172).</param>
    /// <returns>The option lines in file order.</returns>
    internal static IReadOnlyList<ConfigFileLine> ReadLines(string file, byte[] contents, Encoding encoding, int terminalColumns)
    {
        List<ConfigFileLine> lines = [];
        foreach (byte[] lineBytes in ReadLineBytes(WithLineFeedLineEnds(contents)))
        {
            string line = encoding.GetString(lineBytes);
            if (!IsBlankOrComment(line))
            {
                lines.Add(ReadLine(file, lines.Count + 1, line, terminalColumns));
            }
        }

        return lines;
    }

    /// <summary>
    /// The size of the buffer curl 8.22.0's <c>Curl_get_line</c> (<c>lib/curl_get_line.c</c>) hands
    /// <c>fgets</c>, so each read takes at most 127 bytes: a line is read in such chunks.
    /// </summary>
    private const int ReadBufferSize = 128;

    /// <summary><paramref name="contents"/> with every CR LF read as LF, as a Windows text-mode read does.</summary>
    private static byte[] WithLineFeedLineEnds(byte[] contents)
    {
        List<byte> bytes = new(contents.Length);
        for (int index = 0; index < contents.Length; index++)
        {
            if (contents[index] != '\r' || index + 1 == contents.Length || contents[index + 1] != '\n')
            {
                bytes.Add(contents[index]);
            }
        }

        return [.. bytes];
    }

    /// <summary>
    /// Splits <paramref name="bytes"/> into lines without their LF the way curl 8.22.0's
    /// <c>Curl_get_line</c> reads them: in <c>fgets</c> chunks of up to 127 bytes or through the next
    /// LF, each kept only up to its first NUL (curl takes the chunk's <c>strlen</c>). A chunk cut by a
    /// NUL loses its LF, so the line goes on into the next chunk, the next line's; a chunk that starts
    /// with a NUL stops the read, dropping the line it is part of and every line after it, silently.
    /// Measured with the local curl 8.22.0 on 2026-10-26 (BL-3579): <c>bog</c> NUL <c>us</c> LF
    /// <c>write-out = "C"</c> is the unknown option <c>bogwrite-out</c>, a NUL 113 bytes into a
    /// 300-byte parameter keeps 188 of its bytes, and a UTF-16LE file is ignored.
    /// </summary>
    private static List<byte[]> ReadLineBytes(byte[] bytes)
    {
        List<byte[]> lines = [];
        List<byte> line = [];
        int chunkStart = 0;
        while (chunkStart < bytes.Length)
        {
            int chunkEnd = ChunkEnd(bytes, chunkStart);
            int nul = Array.IndexOf(bytes, (byte)0, chunkStart, chunkEnd - chunkStart);
            int keptLength = (nul < 0 ? chunkEnd : nul) - chunkStart;
            if (keptLength == 0)
            {
                return lines;
            }

            line.AddRange(bytes.AsSpan(chunkStart, keptLength));
            if (line[^1] == '\n')
            {
                lines.Add([.. line[..^1]]);
                line.Clear();
            }

            chunkStart = chunkEnd;
        }

        lines.Add([.. line]);
        return lines;
    }

    /// <summary>Where the <c>fgets</c> chunk starting at <paramref name="chunkStart"/> ends: after its LF, after 127 bytes, or at the end.</summary>
    private static int ChunkEnd(byte[] bytes, int chunkStart)
    {
        int limit = Math.Min(bytes.Length, chunkStart + ReadBufferSize - 1);
        int lineFeed = Array.IndexOf(bytes, (byte)'\n', chunkStart, limit - chunkStart);
        return lineFeed < 0 ? limit : lineFeed + 1;
    }

    private static bool IsBlankOrComment(string line)
    {
        string fromFirstNonBlank = line.TrimStart(' ', '\t');
        return fromFirstNonBlank.Length == 0 || fromFirstNonBlank[0] == '#';
    }

    private static ConfigFileLine ReadLine(string file, int number, string line, int terminalColumns)
    {
        bool dashed = line.StartsWith('-');
        int optionEnd = 0;
        while (optionEnd < line.Length && !IsBlankOrSeparator(line[optionEnd], dashed))
        {
            optionEnd++;
        }

        string option = line[..optionEnd];
        string rest = line[Math.Min(optionEnd + 1, line.Length)..];
        int parameterStart = 0;
        while (parameterStart < rest.Length && IsBlankOrSeparator(rest[parameterStart], dashed))
        {
            parameterStart++;
        }

        return ReadParameter(file, number, option, rest[parameterStart..], terminalColumns);
    }

    /// <summary>
    /// Whether <paramref name="character"/> ends an option, or is skipped before a parameter: a space
    /// or tab, or <c>=</c> or <c>:</c> when the option does not start with <c>-</c>.
    /// </summary>
    private static bool IsBlankOrSeparator(char character, bool dashed) =>
        IsBlank(character) || (!dashed && IsSeparator(character));

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private static bool IsSeparator(char character) => character is '=' or ':';

    private static ConfigFileLine ReadParameter(string file, int number, string option, string text, int terminalColumns)
    {
        if (text.StartsWith('"'))
        {
            return new(number, option, Unescape(text), []);
        }

        List<string> warningLines = [];
        if (text.StartsWith('\''))
        {
            warningLines.AddRange(WrappedMessage.Lines(
                "Warning: ",
                $"{file}:{number} Option '{option}' uses argument with leading single quote. It is probably a mistake. Consider double quotes.",
                terminalColumns));
        }

        int parameterEnd = text.AsSpan().IndexOfAny(WhiteSpace);
        if (parameterEnd < 0)
        {
            parameterEnd = text.Length;
        }
        else if (HasMoreThanAComment(text[(parameterEnd + 1)..]))
        {
            warningLines.AddRange(WrappedMessage.Lines(
                "Warning: ",
                $"{file}:{number} Option '{option}' uses argument with unquoted whitespace. This may cause side-effects. Consider double quotes.",
                terminalColumns));
        }

        return new(number, option, parameterEnd == 0 ? null : text[..parameterEnd], warningLines);
    }

    private static bool HasMoreThanAComment(string afterParameter)
    {
        string next = afterParameter.TrimStart(' ', '\t');
        return next.Length > 0 && next[0] is not ('\r' or '#');
    }

    /// <summary>Reads a quoted parameter, <paramref name="quoted"/> starting at its opening quote.</summary>
    private static string Unescape(string quoted)
    {
        StringBuilder parameter = new();
        for (int index = 1; index < quoted.Length && quoted[index] != '"'; index++)
        {
            if (quoted[index] == '\\')
            {
                index++;
                if (index == quoted.Length)
                {
                    break;
                }

                parameter.Append(EscapedCharacter(quoted[index]));
            }
            else
            {
                parameter.Append(quoted[index]);
            }
        }

        return parameter.ToString();
    }

    private static char EscapedCharacter(char letter) => letter switch
    {
        't' => '\t',
        'n' => '\n',
        'r' => '\r',
        'v' => '\v',
        _ => letter,
    };
}
