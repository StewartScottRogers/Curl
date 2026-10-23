using System.Globalization;
using System.Text;

namespace Curl.Conformance.Differential;

/// <summary>
/// Writes bytes and strings as Python's <c>repr()</c> does, the form the tester's report shows
/// outputs in (<c>b'...'</c>), so a report reads the same as the tester's.
/// </summary>
public static class PythonLiteral
{
    /// <summary>Writes <paramref name="bytes"/> as a Python bytes literal.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The literal, such as <c>b'a\r\n'</c>.</returns>
    public static string FormatBytes(ReadOnlySpan<byte> bytes)
    {
        var quote = ChooseQuote(bytes.Contains((byte)'\''), bytes.Contains((byte)'"'));
        var literal = new StringBuilder("b").Append(quote);
        foreach (var value in bytes)
        {
            AppendCharacter(literal, (char)value, quote, value is < 0x20 or >= 0x7f);
        }

        return literal.Append(quote).ToString();
    }

    /// <summary>Writes <paramref name="text"/> as a Python string literal.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The literal, such as <c>'jar.txt'</c>.</returns>
    public static string FormatString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var quote = ChooseQuote(text.Contains('\'', StringComparison.Ordinal), text.Contains('"', StringComparison.Ordinal));
        var literal = new StringBuilder().Append(quote);
        foreach (var character in text)
        {
            AppendCharacter(literal, character, quote, IsUnprintableInString(character));
        }

        return literal.Append(quote).ToString();
    }

    private static char ChooseQuote(bool hasSingleQuote, bool hasDoubleQuote) => hasSingleQuote && !hasDoubleQuote ? '"' : '\'';

    // Python's str repr writes printable Latin-1 letters as they are; these are the unprintable ones below U+0100.
    private static bool IsUnprintableInString(char character) =>
        character < ' ' || (character >= '\x7f' && character <= '\xa0') || character == '\xad';

    private static void AppendCharacter(StringBuilder literal, char character, char quote, bool isUnprintable)
    {
        var escape = NamedEscape(character)
            ?? (character == quote ? "\\" + quote : null)
            ?? (isUnprintable ? "\\x" + ((int)character).ToString("x2", CultureInfo.InvariantCulture) : null);
        _ = escape is null ? literal.Append(character) : literal.Append(escape);
    }

    private static string? NamedEscape(char character) => character switch
    {
        '\\' => @"\\",
        '\t' => @"\t",
        '\n' => @"\n",
        '\r' => @"\r",
        _ => null,
    };
}
