using System.Buffers;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Reads the content word of a form part and the <c>;type=</c>, <c>;filename=</c>,
/// <c>;headers=</c> and <c>;encoder=</c> parameters after it, as curl 8.21.0's <c>get_param_part</c>
/// and <c>get_param_word</c> (<c>src/tool_formparse.c</c>) do, adding curl's warnings to the options
/// as it meets them.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A word runs to the next <c>;</c> or end character. Blanks (space, tab) before it are skipped
/// and, unless it is quoted, blanks after it are dropped. A word starting with <c>"</c> runs to the
/// next unescaped <c>"</c>, with <c>\"</c> and <c>\\</c> unescaped; anything but white space between
/// that quote and the next <c>;</c> or end character is skipped after
/// <c>Warning: Trailing data after quoted form parameter</c>. With no closing quote the word is read
/// as unquoted, quote included.</item>
/// <item>Parameter names match without regard to case. <c>type=</c> runs to the first of
/// <c>()&lt;&gt;@,;:\"[]?=</c>, CR, LF or space; after it, until another known parameter, each
/// <c>;</c>-separated piece is kept as part of the type (<c>type=text/plain; charset=utf-8</c>), and a
/// second <c>type=</c> is such a piece. <c>headers=@file</c> and <c>headers=&lt;file</c> add the file's
/// lines, skipping lines that start with <c>#</c> and blank lines, trimming trailing blanks and
/// appending a line that starts with a space to the header before it; a file that cannot be read
/// warns <c>Cannot read from &lt;file&gt;: No such file or directory</c> and adds nothing.</item>
/// <item>Any other piece is skipped after <c>Warning: skip unknown form field: &lt;piece&gt;</c>, unless
/// it is empty. A parameter the part does not allow is dropped after
/// <c>Warning: Field filename not allowed here: &lt;value&gt;</c> (or <c>encoder</c>).</item>
/// </list>
/// Measured with the local curl 8.21.0 against a loopback listener on 2026-09-26; the cases are in
/// <c>CommandLineFormOptionTests</c>.
/// </remarks>
internal sealed class FormPartParameterReader
{
    /// <summary>The characters that end a <c>;type=</c> value, as curl's <c>strcspn</c> list has them.</summary>
    private static readonly SearchValues<char> ContentTypeEnd = SearchValues.Create("()<>@,;:\\\"[]?=\r\n ");

    /// <summary>The characters curl's <c>ISSPACE</c> takes for white space.</summary>
    private static readonly SearchValues<char> WhiteSpace = SearchValues.Create(" \t\n\v\f\r");

    private readonly CommandLineOptions options;
    private readonly string text;
    private readonly IDataFileReader dataFileReader;
    private int position;
    private char endCharacter;

    /// <summary>Creates a reader positioned at the start of <paramref name="text"/>.</summary>
    /// <param name="options">The options the warnings are added to.</param>
    /// <param name="text">The content after the field name's <c>=</c>.</param>
    /// <param name="dataFileReader">Reads a <c>;headers=@file</c> file.</param>
    internal FormPartParameterReader(CommandLineOptions options, string text, IDataFileReader dataFileReader)
    {
        this.options = options;
        this.text = text;
        this.dataFileReader = dataFileReader;
    }

    /// <summary>The text from the current position to the end.</summary>
    internal string Rest => text[position..];

    private char Current => position < text.Length ? text[position] : '\0';

    /// <summary>Steps over one character: the <c>@</c>, <c>&lt;</c> or <c>,</c> before a file name.</summary>
    internal void SkipSeparator() => position++;

    /// <summary>Adds <c>Warning: &lt;message&gt;</c>, wrapped as curl wraps it, unless <c>-s</c> is in effect.</summary>
    /// <param name="message">The warning text.</param>
    internal void Warn(string message) =>
        options.AddWarningLinesUnlessSilent(WrappedMessage.Lines("Warning: ", message));

    /// <summary>Reads one part's content word and parameters, leaving the position on the character that ended it.</summary>
    /// <param name="endCharacter">The character that ends the part besides the end of the text: <c>,</c> between files, else <c>\0</c>.</param>
    /// <param name="fileNameAllowed"><see langword="false"/> to drop a <c>;filename=</c> with a warning.</param>
    /// <param name="encoderAllowed"><see langword="false"/> to drop an <c>;encoder=</c> with a warning.</param>
    /// <returns>What was read.</returns>
    internal FormPartParameters Read(char endCharacter, bool fileNameAllowed, bool encoderAllowed)
    {
        this.endCharacter = endCharacter;
        PartBuilder part = new() { Data = ReadTrimmedWord() };
        while (Current == ';')
        {
            position++;
            SkipBlanks();
            ReadParameter(part);
        }

        part.CloseContentType();
        string? fileName = Allowed(part.FileName, fileNameAllowed, "filename");
        string? encoder = Allowed(part.Encoder, encoderAllowed, "encoder");
        return new FormPartParameters(part.Data, part.ContentType, fileName, encoder, part.Headers, Current);
    }

    private string? Allowed(string? value, bool allowed, string parameterName)
    {
        if (allowed || value is null)
        {
            return value;
        }

        Warn($"Field {parameterName} not allowed here: {value}");
        return null;
    }

    private void ReadParameter(PartBuilder part)
    {
        if (!part.ContentTypeOpen && TryTakePrefix("type="))
        {
            ReadContentType(part);
        }
        else if (TryTakePrefix("filename="))
        {
            part.CloseContentType();
            part.FileName = ReadTrimmedWord();
        }
        else if (TryTakePrefix("headers="))
        {
            part.CloseContentType();
            ReadHeaders(part);
        }
        else if (TryTakePrefix("encoder="))
        {
            part.CloseContentType();
            part.Encoder = ReadTrimmedWord();
        }
        else
        {
            ReadOtherPiece(part);
        }
    }

    private void ReadContentType(PartBuilder part)
    {
        SkipBlanks();
        int start = position;
        int length = text.AsSpan(start).IndexOfAny(ContentTypeEnd);
        position = length < 0 ? text.Length : start + length;
        part.OpenContentType(text, start, position);
    }

    /// <summary>Keeps a piece after an open <c>;type=</c> as part of the type; otherwise skips it with a warning.</summary>
    private void ReadOtherPiece(PartBuilder part)
    {
        if (part.ContentTypeOpen)
        {
            ExtendContentType(part);
            return;
        }

        (string unknown, _) = ReadWord();
        if (unknown.Length > 0)
        {
            Warn($"skip unknown form field: {unknown}");
        }
    }

    private void ExtendContentType(PartBuilder part)
    {
        int end = position;
        for (; position < text.Length && Current != ';' && Current != endCharacter; position++)
        {
            if (!IsBlank(Current))
            {
                end = position + 1;
            }
        }

        part.ContentTypeEnd = end;
    }

    private void ReadHeaders(PartBuilder part)
    {
        if (Current is not ('@' or '<'))
        {
            SkipBlanks();
            part.Headers.Add(ReadTrimmedWord());
            return;
        }

        position++;
        SkipBlanks();
        string file = ReadTrimmedWord();
        if (dataFileReader.TryReadFile(file, out byte[] contents))
        {
            AddHeaderFileLines(part.Headers, Encoding.UTF8.GetString(contents));
        }
        else
        {
            Warn($"Cannot read from {file}: No such file or directory");
        }
    }

    /// <summary>Adds a header file's lines as curl's <c>read_field_headers</c> does.</summary>
    private static void AddHeaderFileLines(List<string> headers, string contents)
    {
        foreach (string line in contents.Split('\n'))
        {
            string header = line.TrimEnd(' ', '\t', '\r', '\n');
            if (line.StartsWith('#') || header.Length == 0)
            {
                continue;
            }

            if (line.StartsWith(' ') && headers.Count > 0)
            {
                headers[^1] += header;
            }
            else
            {
                headers.Add(header);
            }
        }
    }

    /// <summary>Reads a word after skipping blanks, dropping the blanks after it unless it was quoted.</summary>
    private string ReadTrimmedWord()
    {
        SkipBlanks();
        (string word, bool quoted) = ReadWord();
        return quoted ? word : word.TrimEnd(' ', '\t');
    }

    /// <summary>Reads a word as curl's <c>get_param_word</c> does.</summary>
    private (string Word, bool Quoted) ReadWord()
    {
        if (Current == '"' && TryReadQuotedWord(out string quoted))
        {
            return (quoted, true);
        }

        int start = position;
        SkipToPieceEnd();
        return (text[start..position], false);
    }

    private bool TryReadQuotedWord(out string word)
    {
        StringBuilder unescaped = new();
        for (int index = position + 1; index < text.Length; index++)
        {
            char character = text[index];
            if (IsEscape(index))
            {
                unescaped.Append(text[++index]);
            }
            else if (character == '"')
            {
                position = index + 1;
                SkipTrailingDataAfterQuote();
                word = unescaped.ToString();
                return true;
            }
            else
            {
                unescaped.Append(character);
            }
        }

        word = string.Empty;
        return false;
    }

    /// <summary><see langword="true"/> for a backslash before a backslash or a double quote.</summary>
    private bool IsEscape(int index) =>
        text[index] == '\\' && index + 1 < text.Length && text[index + 1] is '\\' or '"';

    private void SkipTrailingDataAfterQuote()
    {
        int start = position;
        SkipToPieceEnd();
        if (text.AsSpan(start, position - start).ContainsAnyExcept(WhiteSpace))
        {
            Warn("Trailing data after quoted form parameter");
        }
    }

    private void SkipToPieceEnd()
    {
        while (position < text.Length && Current != ';' && Current != endCharacter)
        {
            position++;
        }
    }

    private void SkipBlanks()
    {
        while (IsBlank(Current))
        {
            position++;
        }
    }

    /// <summary>Steps over <paramref name="prefix"/> when the text continues with it, ignoring ASCII case, as curl's <c>checkprefix</c> does.</summary>
    private bool TryTakePrefix(string prefix)
    {
        if (text.Length - position < prefix.Length || !Ascii.EqualsIgnoreCase(text.AsSpan(position, prefix.Length), prefix))
        {
            return false;
        }

        position += prefix.Length;
        return true;
    }

    private static bool IsBlank(char character) => character is ' ' or '\t';

    /// <summary>The parameters of one part while they are being read.</summary>
    private sealed class PartBuilder
    {
        private string? contentTypeText;
        private int contentTypeStart;

        public string Data { get; init; } = string.Empty;

        public string? ContentType { get; private set; }

        public string? FileName { get; set; }

        public string? Encoder { get; set; }

        public List<string> Headers { get; } = [];

        /// <summary><see langword="true"/> while a <c>;type=</c> value may still grow; curl's <c>endct</c>.</summary>
        public bool ContentTypeOpen => contentTypeText is not null;

        /// <summary>Where the open content type ends in the text.</summary>
        public int ContentTypeEnd { get; set; }

        public void OpenContentType(string text, int start, int end)
        {
            contentTypeText = text;
            contentTypeStart = start;
            ContentTypeEnd = end;
        }

        /// <summary>Fixes the content type at its current end; nothing when none is open.</summary>
        public void CloseContentType()
        {
            if (contentTypeText is not null)
            {
                ContentType = contentTypeText[contentTypeStart..ContentTypeEnd];
                contentTypeText = null;
            }
        }
    }
}
