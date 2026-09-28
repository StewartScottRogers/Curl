using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Splits netrc text into tokens as curl 8.21.0 does, and enforces its limits: a line of
/// 16383 bytes or more, or a token of 4096 bytes or more, is a syntax error, and so is a
/// quoted token that never closes. Lines are checked only when the scanner reaches them, so
/// text after the point where a lookup stops is never judged.
/// </summary>
/// <param name="text">The netrc text.</param>
internal sealed class NetrcTokenScanner(string text)
{
    /// <summary>The most UTF-8 bytes a line may hold, not counting its line feed.</summary>
    internal const int MaximumLineBytes = 16382;

    /// <summary>The most UTF-8 bytes a token may hold, after its quotes and escapes are removed.</summary>
    internal const int MaximumTokenBytes = 4095;

    private const int EndOfText = -1;

    private int position;

    private int uncheckedLineStart;

    /// <summary>Gets whether the scanner met a syntax error; once set, it reads nothing more.</summary>
    internal bool HasSyntaxError { get; private set; }

    /// <summary>
    /// Reads the next token: a run of characters up to whitespace, or a <c>"</c>-quoted
    /// string, which may span lines, in which <c>\n</c>, <c>\r</c> and <c>\t</c> are a line
    /// feed, carriage return and tab and a backslash before any other character is that
    /// character. A quote inside an unquoted token is kept; a quoted token ends at its
    /// closing quote even when more characters follow.
    /// </summary>
    /// <returns>The token; <see langword="null" /> at the end of the text or on a syntax error.</returns>
    internal string? ReadToken() => WithinTokenLimit(ReadTokenOfAnyLength());

    /// <summary>
    /// Reads the next token where a keyword is expected: a token starting with <c>#</c>,
    /// quoted or not, comments out the rest of its line and is skipped whatever its length.
    /// </summary>
    /// <returns>The token; <see langword="null" /> at the end of the text or on a syntax error.</returns>
    internal string? ReadKeyword()
    {
        string? token = ReadTokenOfAnyLength();
        while (token is not null && token.StartsWith('#'))
        {
            SkipRestOfLine();
            token = ReadTokenOfAnyLength();
        }

        return WithinTokenLimit(token);
    }

    /// <summary>Skips to the line feed that ends the current line, leaving it unread.</summary>
    internal void SkipRestOfLine()
    {
        while (Peek() is not EndOfText and not '\n')
        {
            position++;
        }
    }

    /// <summary>
    /// Skips a <c>macdef</c> macro: the rest of the line that names it, then every line up to
    /// and not including the first that is empty or holds only whitespace.
    /// </summary>
    internal void SkipMacroDefinition()
    {
        SkipRestOfLine();
        while (Peek() == '\n')
        {
            position++;
            if (CurrentLineIsBlank())
            {
                return;
            }

            SkipRestOfLine();
        }
    }

    // Measured: a form feed does not separate tokens.
    private string? ReadTokenOfAnyLength()
    {
        SkipWhitespace();
        int first = Peek();
        if (first == EndOfText)
        {
            return null;
        }

        return first == '"' ? ReadQuotedToken() : ReadUnquotedToken();
    }

    private string? WithinTokenLimit(string? token)
    {
        if (token is not null && Encoding.UTF8.GetByteCount(token) > MaximumTokenBytes)
        {
            HasSyntaxError = true;
            return null;
        }

        return token;
    }

    private static bool IsWhitespace(int character) =>
        character is ' ' or '\t' or '\n' or '\r';

    private bool CurrentLineIsBlank()
    {
        while (Peek() is not '\n' and not EndOfText)
        {
            if (!IsWhitespace(Peek()))
            {
                return false;
            }

            position++;
        }

        return true;
    }

    private void SkipWhitespace()
    {
        while (IsWhitespace(Peek()))
        {
            position++;
        }
    }

    private string ReadUnquotedToken()
    {
        int start = position;
        while (Peek() is not EndOfText && !IsWhitespace(Peek()))
        {
            position++;
        }

        return text[start..position];
    }

    private string? ReadQuotedToken()
    {
        var token = new StringBuilder();
        position++;
        for (int character = Peek(); character != '"'; character = Peek())
        {
            if (character == '\\')
            {
                position++;
                character = Unescape(Peek());
            }

            if (character == EndOfText)
            {
                HasSyntaxError = true;
                return null;
            }

            token.Append((char)character);
            position++;
        }

        position++;
        return token.ToString();
    }

    private static int Unescape(int character) => character switch
    {
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        _ => character,
    };

    private int Peek()
    {
        if (HasSyntaxError || position >= text.Length)
        {
            return EndOfText;
        }

        if (position >= uncheckedLineStart && !CheckLineAtUncheckedStart())
        {
            return EndOfText;
        }

        return text[position];
    }

    private bool CheckLineAtUncheckedStart()
    {
        int lineFeed = text.IndexOf('\n', uncheckedLineStart);
        int end = lineFeed < 0 ? text.Length : lineFeed;
        if (Encoding.UTF8.GetByteCount(text.AsSpan(uncheckedLineStart, end - uncheckedLineStart)) > MaximumLineBytes)
        {
            HasSyntaxError = true;
            return false;
        }

        uncheckedLineStart = end + 1;
        return true;
    }
}
