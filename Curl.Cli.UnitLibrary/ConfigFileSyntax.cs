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
/// <item>The bytes are read in the given encoding and every CR LF as LF, as a Windows text-mode read does; lines end at LF.</item>
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
/// Not modelled: a NUL byte ending a line early, and the 10 MiB line limit.
/// </remarks>
internal static class ConfigFileSyntax
{
    /// <summary>The characters C's <c>isspace</c> accepts, which end an unquoted parameter.</summary>
    private static readonly SearchValues<char> WhiteSpace = SearchValues.Create(" \t\n\v\f\r");

    /// <summary>Splits <paramref name="contents"/> into its option lines.</summary>
    /// <param name="file">The file name as curl shows it in warnings.</param>
    /// <param name="contents">The file's bytes.</param>
    /// <param name="encoding">The encoding the bytes are read in: UTF-8, or on Windows the ANSI code page for a file that is not valid UTF-8 (<see cref="ConfigFileApplier"/>).</param>
    /// <returns>The option lines in file order.</returns>
    internal static IReadOnlyList<ConfigFileLine> ReadLines(string file, byte[] contents, Encoding encoding)
    {
        string text = encoding.GetString(contents).Replace("\r\n", "\n", StringComparison.Ordinal);
        List<ConfigFileLine> lines = [];
        foreach (string line in text.Split('\n'))
        {
            if (!IsBlankOrComment(line))
            {
                lines.Add(ReadLine(file, lines.Count + 1, line));
            }
        }

        return lines;
    }

    private static bool IsBlankOrComment(string line)
    {
        string fromFirstNonBlank = line.TrimStart(' ', '\t');
        return fromFirstNonBlank.Length == 0 || fromFirstNonBlank[0] == '#';
    }

    private static ConfigFileLine ReadLine(string file, int number, string line)
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

        return ReadParameter(file, number, option, rest[parameterStart..]);
    }

    /// <summary>
    /// Whether <paramref name="character"/> ends an option, or is skipped before a parameter: a space
    /// or tab, or <c>=</c> or <c>:</c> when the option does not start with <c>-</c>.
    /// </summary>
    private static bool IsBlankOrSeparator(char character, bool dashed) =>
        IsBlank(character) || (!dashed && IsSeparator(character));

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private static bool IsSeparator(char character) => character is '=' or ':';

    private static ConfigFileLine ReadParameter(string file, int number, string option, string text)
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
                $"{file}:{number} Option '{option}' uses argument with leading single quote. It is probably a mistake. Consider double quotes."));
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
                $"{file}:{number} Option '{option}' uses argument with unquoted whitespace. This may cause side-effects. Consider double quotes."));
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
