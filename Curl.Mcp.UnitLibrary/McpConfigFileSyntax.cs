using System.Buffers;
using System.Text;

namespace Curl.Mcp;

/// <summary>
/// Splits the text of a <c>-K</c> / <c>--config</c> file into its option lines as curl's
/// <c>parseconfig</c> does, the rules of <c>Curl.Cli</c>'s <c>ConfigFileSyntax</c> without its
/// warnings (ADR-0564): blank lines and <c>#</c> comments are skipped; the option runs from the
/// line's first character to a space or tab, or, unless it starts with <c>-</c>, to <c>=</c> or
/// <c>:</c>; a quoted parameter runs to the next unescaped <c>"</c>, any other to white space.
/// </summary>
internal static class McpConfigFileSyntax
{
    private static readonly SearchValues<char> WhiteSpace = SearchValues.Create(" \t\n\v\f\r");

    /// <summary>Reads the option lines of <paramref name="text" />.</summary>
    /// <param name="text">The config file's text.</param>
    /// <returns>Each line's option and its parameter, <see langword="null" /> when it has none, in file order.</returns>
    internal static IReadOnlyList<KeyValuePair<string, string?>> ReadLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => line.TrimStart(' ', '\t') is { Length: > 0 } content && content[0] != '#')
            .Select(ReadLine)
            .ToList();

    private static KeyValuePair<string, string?> ReadLine(string line)
    {
        bool dashed = line.StartsWith('-');
        int optionEnd = 0;
        while (optionEnd < line.Length && !IsBlankOrSeparator(line[optionEnd], dashed))
        {
            optionEnd++;
        }

        string rest = line[Math.Min(optionEnd + 1, line.Length)..];
        int parameterStart = 0;
        while (parameterStart < rest.Length && IsBlankOrSeparator(rest[parameterStart], dashed))
        {
            parameterStart++;
        }

        return KeyValuePair.Create(line[..optionEnd], ReadParameter(rest[parameterStart..]));
    }

    private static bool IsBlankOrSeparator(char character, bool dashed) =>
        IsBlank(character) || (!dashed && IsSeparator(character));

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private static bool IsSeparator(char character) => character is '=' or ':';

    private static string? ReadParameter(string text)
    {
        if (text.StartsWith('"'))
        {
            return Unescape(text);
        }

        int end = text.AsSpan().IndexOfAny(WhiteSpace);
        string parameter = end < 0 ? text : text[..end];
        return parameter.Length == 0 ? null : parameter;
    }

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
