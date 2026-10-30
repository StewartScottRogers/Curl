using System.Text;

namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Splits <c>.proto</c> text into tokens, skipping whitespace, <c>//</c> and <c>/* */</c>
/// comments. Malformed text throws a <see cref="ProtoSyntaxException" /> naming where.
/// </summary>
internal sealed class ProtoTokenizer
{
    private const string SymbolCharacters = "=;{}[]()<>,.-+:";

    private const string EscapeLetters = "abfnrtv\\'\"?";

    private const string EscapedCharacters = "\a\b\f\n\r\t\v\\'\"?";

    private readonly string text;

    private int position;

    private int line = 1;

    private int column = 1;

    private ProtoTokenizer(string text)
    {
        this.text = text;
    }

    /// <summary>Splits the whole text into tokens, the last of them <see cref="ProtoTokenKind.End" />.</summary>
    public static List<ProtoToken> Tokenize(string text)
    {
        var tokenizer = new ProtoTokenizer(text);
        var tokens = new List<ProtoToken>();
        ProtoToken token;
        do
        {
            token = tokenizer.ReadToken();
            tokens.Add(token);
        }
        while (token.Kind != ProtoTokenKind.End);

        return tokens;
    }

    private ProtoToken ReadToken()
    {
        SkipWhitespaceAndComments();
        var (start, startLine, startColumn) = (position, line, column);
        var decoded = new StringBuilder();
        var kind = ReadTokenKind(decoded);
        var tokenText = kind == ProtoTokenKind.String ? decoded.ToString() : text[start..position];
        return new ProtoToken(kind, tokenText, startLine, startColumn, start, position);
    }

    private ProtoTokenKind ReadTokenKind(StringBuilder decoded)
    {
        var character = CharacterAt(position);
        if (position == text.Length)
        {
            return ProtoTokenKind.End;
        }

        if (character is '"' or '\'')
        {
            ReadString(decoded);
            return ProtoTokenKind.String;
        }

        return ReadUnquotedTokenKind(character);
    }

    private ProtoTokenKind ReadUnquotedTokenKind(char character)
    {
        if (IsIdentifierCharacter(character) && !char.IsAsciiDigit(character))
        {
            return ReadIdentifier();
        }

        if (StartsNumber(character))
        {
            return ReadNumber();
        }

        if (!SymbolCharacters.Contains(character))
        {
            throw Error(line, column, $"Unexpected character '{character}'.");
        }

        Advance();
        return ProtoTokenKind.Symbol;
    }

    private static bool IsIdentifierCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character == '_';

    private ProtoTokenKind ReadIdentifier()
    {
        while (IsIdentifierCharacter(CharacterAt(position)))
        {
            Advance();
        }

        return ProtoTokenKind.Identifier;
    }

    private bool StartsNumber(char character) =>
        char.IsAsciiDigit(character) || (character == '.' && char.IsAsciiDigit(CharacterAt(position + 1)));

    private ProtoTokenKind ReadNumber()
    {
        var (start, startLine, startColumn) = (position, line, column);
        while (IsNumberCharacter(position))
        {
            Advance();
        }

        var literal = text[start..position];
        if (ProtoNumberLiteral.IsInteger(literal))
        {
            return ProtoTokenKind.Integer;
        }

        return ProtoNumberLiteral.IsFloat(literal)
            ? ProtoTokenKind.Float
            : throw Error(startLine, startColumn, $"Invalid number \"{literal}\".");
    }

    private bool IsNumberCharacter(int index)
    {
        var character = CharacterAt(index);
        return char.IsAsciiLetterOrDigit(character) || "_.".Contains(character) || IsExponentSign(index);
    }

    private bool IsExponentSign(int index) => "+-".Contains(CharacterAt(index)) && "eE".Contains(CharacterAt(index - 1));

    private void ReadString(StringBuilder decoded)
    {
        var (startLine, startColumn) = (line, column);
        var quote = Advance();
        while (CharacterAt(position) != quote)
        {
            if (position == text.Length || CharacterAt(position) == '\n')
            {
                throw Error(startLine, startColumn, "Unterminated string literal.");
            }

            var character = Advance();
            decoded.Append(character == '\\' ? ReadEscape() : character);
        }

        Advance();
    }

    private char ReadEscape()
    {
        var character = CharacterAt(position);
        var letter = EscapeLetters.IndexOf(character, StringComparison.Ordinal);
        if (letter >= 0)
        {
            Advance();
            return EscapedCharacters[letter];
        }

        if ("xX".Contains(character))
        {
            Advance();
            return (char)ReadDigits(16, 2);
        }

        return char.IsBetween(character, '0', '7')
            ? (char)ReadDigits(8, 3)
            : throw Error(line, column, "Invalid escape sequence in string literal.");
    }

    private int ReadDigits(int radix, int maximumCount)
    {
        var value = 0;
        var count = 0;
        while (count < maximumCount && DigitValue(CharacterAt(position)) < radix)
        {
            value = (value * radix) + DigitValue(Advance());
            count++;
        }

        return count > 0 ? value : throw Error(line, column, "Expected a hexadecimal digit after \\x.");
    }

    private static int DigitValue(char character) =>
        char.IsAsciiHexDigit(character) ? Convert.ToInt32(character.ToString(), 16) : int.MaxValue;

    private void SkipWhitespaceAndComments()
    {
        while (true)
        {
            if (char.IsWhiteSpace(CharacterAt(position)))
            {
                Advance();
            }
            else if (IsAt("//"))
            {
                SkipLineComment();
            }
            else if (IsAt("/*"))
            {
                SkipBlockComment();
            }
            else
            {
                return;
            }
        }
    }

    private void SkipLineComment()
    {
        while (position < text.Length && text[position] != '\n')
        {
            Advance();
        }
    }

    private void SkipBlockComment()
    {
        var (startLine, startColumn) = (line, column);
        Advance();
        Advance();
        while (!IsAt("*/"))
        {
            if (position == text.Length)
            {
                throw Error(startLine, startColumn, "Unterminated block comment.");
            }

            Advance();
        }

        Advance();
        Advance();
    }

    private bool IsAt(string prefix) => text.AsSpan(position).StartsWith(prefix, StringComparison.Ordinal);

    private char CharacterAt(int index) => index < text.Length ? text[index] : '\0';

    private char Advance()
    {
        var character = text[position++];
        (line, column) = character == '\n' ? (line + 1, 1) : (line, column + 1);
        return character;
    }

    private static ProtoSyntaxException Error(int line, int column, string message) =>
        new(new ProtoSyntaxError(line, column, message));
}
