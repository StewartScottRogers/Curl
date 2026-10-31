using System.Globalization;
using System.Text;

namespace Curl.Extension.CompressionDictionary;

/// <summary>
/// Parses a header field value as an RFC 8941 dictionary (section 4.2.2). Any syntax error fails the whole
/// field, as section 4.2 requires; a later member with the same key replaces an earlier one.
/// </summary>
internal sealed class StructuredFieldDictionaryParser
{
    private const string KeyPunctuation = "_-.*";
    private const string TokenPunctuation = "!#$%&'*+-.^_`|~:/";

    private static readonly StructuredFieldMember BareTrueMember = new(new StructuredFieldItem(StructuredFieldItemKind.Boolean, "1"), null);

    private readonly string _text;
    private int _position;

    private StructuredFieldDictionaryParser(string text) => _text = text;

    /// <summary>Parses <paramref name="fieldValue"/>; null when it is not a valid RFC 8941 dictionary.</summary>
    public static IReadOnlyDictionary<string, StructuredFieldMember>? Parse(string fieldValue)
    {
        var parser = new StructuredFieldDictionaryParser(fieldValue.Trim(' ', '\t'));
        return parser.ParseDictionary();
    }

    private bool AtEnd => _position >= _text.Length;

    private char Current => _text[_position];

    private bool IsAt(char character) => !AtEnd && Current == character;

    private bool TrySkip(char character)
    {
        if (!IsAt(character))
        {
            return false;
        }

        _position++;
        return true;
    }

    private void SkipWhile(Func<char, bool> predicate)
    {
        while (!AtEnd && predicate(Current))
        {
            _position++;
        }
    }

    private Dictionary<string, StructuredFieldMember>? ParseDictionary()
    {
        var members = new Dictionary<string, StructuredFieldMember>(StringComparer.Ordinal);
        while (!AtEnd)
        {
            var member = ParseMember(out var key);
            if (member is null)
            {
                return null;
            }

            members[key] = member;
            if (!SkipMemberSeparator())
            {
                return null;
            }
        }

        return members;
    }

    private StructuredFieldMember? ParseMember(out string key)
    {
        key = ParseKey() ?? string.Empty;
        if (key.Length == 0)
        {
            return null;
        }

        if (!TrySkip('='))
        {
            return ParseParameters() ? BareTrueMember : null;
        }

        return IsAt('(') ? ParseInnerList() : ParseItemMember();
    }

    private StructuredFieldMember? ParseItemMember()
    {
        var item = ParseItem();
        return item is null ? null : new StructuredFieldMember(item, null);
    }

    private bool SkipMemberSeparator()
    {
        SkipWhitespace();
        if (AtEnd)
        {
            return true;
        }

        if (!TrySkip(','))
        {
            return false;
        }

        SkipWhitespace();
        return !AtEnd;
    }

    private void SkipWhitespace() => SkipWhile(IsWhitespace);

    private static bool IsWhitespace(char character) => character is ' ' or '\t';

    private static bool IsSpace(char character) => character == ' ';

    private StructuredFieldMember? ParseInnerList()
    {
        _position++;
        var items = new List<StructuredFieldItem>();
        while (true)
        {
            SkipWhile(IsSpace);
            if (AtEnd)
            {
                return null;
            }

            if (TrySkip(')'))
            {
                return ParseParameters() ? new StructuredFieldMember(null, items) : null;
            }

            var item = ParseItem();
            if (item is null || !IsInnerListItemEnd())
            {
                return null;
            }

            items.Add(item);
        }
    }

    private bool IsInnerListItemEnd() => AtEnd || Current is ' ' or ')';

    private StructuredFieldItem? ParseItem()
    {
        var item = ParseBareItem();
        return item is not null && ParseParameters() ? item : null;
    }

    private bool ParseParameters()
    {
        while (TrySkip(';'))
        {
            SkipWhile(IsSpace);
            if (ParseKey() is null || !ParseParameterValue())
            {
                return false;
            }
        }

        return true;
    }

    private bool ParseParameterValue() => !TrySkip('=') || ParseBareItem() is not null;

    private string? ParseKey()
    {
        if (AtEnd || !(char.IsAsciiLetterLower(Current) || Current == '*'))
        {
            return null;
        }

        var start = _position;
        SkipWhile(IsKeyCharacter);
        return _text[start.._position];
    }

    private static bool IsKeyCharacter(char character) =>
        char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || KeyPunctuation.Contains(character, StringComparison.Ordinal);

    private StructuredFieldItem? ParseBareItem()
    {
        if (AtEnd)
        {
            return null;
        }

        return Current switch
        {
            '"' => ParseString(),
            ':' => ParseByteSequence(),
            '?' => ParseBoolean(),
            _ => ParseNumberOrToken(),
        };
    }

    private StructuredFieldItem? ParseNumberOrToken()
    {
        if (Current == '-' || char.IsAsciiDigit(Current))
        {
            return ParseNumber();
        }

        return char.IsAsciiLetter(Current) || Current == '*' ? ParseToken() : null;
    }

    private StructuredFieldItem? ParseNumber()
    {
        var start = _position;
        TrySkip('-');
        var digitsStart = _position;
        SkipWhile(IsNumberCharacter);
        return IsValidNumber(_text[digitsStart.._position])
            ? new StructuredFieldItem(StructuredFieldItemKind.Number, _text[start.._position])
            : null;
    }

    private static bool IsNumberCharacter(char character) => char.IsAsciiDigit(character) || character == '.';

    private static bool IsValidNumber(string number) =>
        number.Length > 0 && char.IsAsciiDigit(number[0]) && char.IsAsciiDigit(number[^1]) && number.IndexOf('.') == number.LastIndexOf('.');

    private StructuredFieldItem? ParseString()
    {
        _position++;
        var value = new StringBuilder();
        while (!AtEnd)
        {
            var character = Current;
            _position++;
            if (character == '"')
            {
                return new StructuredFieldItem(StructuredFieldItemKind.String, value.ToString());
            }

            if (!AppendStringCharacter(value, character))
            {
                return null;
            }
        }

        return null;
    }

    private bool AppendStringCharacter(StringBuilder value, char character)
    {
        if (character == '\\')
        {
            if (!IsAt('"') && !IsAt('\\'))
            {
                return false;
            }

            character = Current;
            _position++;
        }

        value.Append(character);
        return IsVisibleAscii(character);
    }

    private static bool IsVisibleAscii(char character) => character is >= ' ' and <= '~';

    private StructuredFieldItem ParseToken()
    {
        var start = _position;
        _position++;
        SkipWhile(IsTokenCharacter);
        return new StructuredFieldItem(StructuredFieldItemKind.Token, _text[start.._position]);
    }

    private static bool IsTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || TokenPunctuation.Contains(character, StringComparison.Ordinal);

    private StructuredFieldItem? ParseByteSequence()
    {
        _position++;
        var end = _text.IndexOf(':', _position);
        if (end < 0)
        {
            return null;
        }

        var encoded = _text[_position..end];
        _position = end + 1;
        var decoded = new byte[encoded.Length];
        return Convert.TryFromBase64String(encoded, decoded, out _)
            ? new StructuredFieldItem(StructuredFieldItemKind.ByteSequence, encoded)
            : null;
    }

    private StructuredFieldItem? ParseBoolean()
    {
        _position++;
        if (!IsAt('0') && !IsAt('1'))
        {
            return null;
        }

        var value = Current.ToString(CultureInfo.InvariantCulture);
        _position++;
        return new StructuredFieldItem(StructuredFieldItemKind.Boolean, value);
    }
}
