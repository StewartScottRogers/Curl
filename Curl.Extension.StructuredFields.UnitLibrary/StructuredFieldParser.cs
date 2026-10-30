using System.Globalization;
using System.Text;

namespace Curl.Extension.StructuredFields;

/// <summary>
/// Parses a Structured Field value by the algorithms of RFC 9651 §4.2: a List (§4.2.1), a Dictionary (§4.2.2) or an
/// Item (§4.2.3), with every bare item type of §4.2.4 to §4.2.10. A value that does not parse throws a
/// <see cref="FormatException" /> saying what was wrong, and the field is then to be ignored whole (§4.2).
/// </summary>
internal sealed class StructuredFieldParser
{
    private const int MaximumIntegerDigits = 15;

    private const int MaximumDecimalDigits = 16;

    private const int MaximumIntegerPartDigits = 12;

    private const int MaximumFractionDigits = 3;

    private static readonly BareItem True = new(BareItemType.Boolean, true);

    private static readonly Func<char, bool> IsDigit = char.IsAsciiDigit;

    private readonly string _text;

    private int _position;

    private StructuredFieldParser(string text) => _text = text;

    private bool IsEmpty => _position == _text.Length;

    private char First => IsEmpty ? '\0' : _text[_position];

    /// <summary>Parses <paramref name="text" /> as a List (§4.2.1).</summary>
    /// <param name="text">The field value, every field line of the name joined with commas.</param>
    /// <returns>The List's members.</returns>
    public static IReadOnlyList<ListMember> ParseList(string text) => ParseWhole(text, parser => parser.ReadList());

    /// <summary>Parses <paramref name="text" /> as a Dictionary (§4.2.2).</summary>
    /// <param name="text">The field value, every field line of the name joined with commas.</param>
    /// <returns>The Dictionary's members, in order; a repeated key keeps its first place and its last value.</returns>
    public static IReadOnlyList<DictionaryMember> ParseDictionary(string text) => ParseWhole(text, parser => parser.ReadDictionary());

    /// <summary>Parses <paramref name="text" /> as an Item (§4.2.3).</summary>
    /// <param name="text">The field value.</param>
    /// <returns>The Item.</returns>
    public static Item ParseItem(string text) => ParseWhole(text, parser => parser.ReadItem());

    private static T ParseWhole<T>(string text, Func<StructuredFieldParser, T> read)
    {
        if (text.Any(character => character > '\x7f'))
        {
            throw new FormatException("the value is not ASCII");
        }

        StructuredFieldParser parser = new(text);
        parser.SkipSpaces();
        var value = read(parser);
        parser.SkipSpaces();
        return parser.IsEmpty ? value : throw parser.Unexpected();
    }

    private List<ListMember> ReadList()
    {
        List<ListMember> members = [];
        while (!IsEmpty)
        {
            members.Add(ReadItemOrInnerList());
            if (IsAtEndAfterMember())
            {
                break;
            }
        }

        return members;
    }

    private List<DictionaryMember> ReadDictionary()
    {
        List<DictionaryMember> members = [];
        while (!IsEmpty)
        {
            var key = ReadKey();
            ListMember value = Consumes('=') ? ReadItemOrInnerList() : new Item(True, ReadParameters());
            SetLast(members, new DictionaryMember(key, value), member => member.Key == key);
            if (IsAtEndAfterMember())
            {
                break;
            }
        }

        return members;
    }

    private bool IsAtEndAfterMember()
    {
        SkipWhitespace();
        if (IsEmpty)
        {
            return true;
        }

        if (!Consumes(','))
        {
            throw Unexpected();
        }

        SkipWhitespace();
        return IsEmpty ? throw new FormatException("a comma ends the value") : false;
    }

    private ListMember ReadItemOrInnerList() => First == '(' ? ReadInnerList() : ReadItem();

    private InnerList ReadInnerList()
    {
        _position++;
        List<Item> items = [];
        while (true)
        {
            SkipSpaces();
            if (Consumes(')'))
            {
                return new InnerList(items, ReadParameters());
            }

            items.Add(ReadItem());
            if (First is not (' ' or ')'))
            {
                throw IsEmpty ? new FormatException("an inner list is not closed") : Unexpected();
            }
        }
    }

    private Item ReadItem() => new(ReadBareItem(), ReadParameters());

    private List<Parameter> ReadParameters()
    {
        List<Parameter> parameters = [];
        while (Consumes(';'))
        {
            SkipSpaces();
            var key = ReadKey();
            var value = Consumes('=') ? ReadBareItem() : True;
            SetLast(parameters, new Parameter(key, value), parameter => parameter.Key == key);
        }

        return parameters;
    }

    private string ReadKey()
    {
        if (!IsLowercaseAlpha(First) && First != '*')
        {
            throw IsEmpty ? new FormatException("a key is missing") : Unexpected();
        }

        return ReadWhile(character => IsLowercaseAlpha(character) || char.IsAsciiDigit(character) || "_-.*".Contains(character));
    }

    private BareItem ReadBareItem()
    {
        if (IsEmpty)
        {
            throw new FormatException("an item is missing");
        }

        if (First == '-' || char.IsAsciiDigit(First))
        {
            return ReadNumber();
        }

        return First == '*' || char.IsAsciiLetter(First)
            ? new BareItem(BareItemType.Token, ReadWhile(IsTokenCharacter))
            : ReadDelimitedBareItem();
    }

    private BareItem ReadDelimitedBareItem() => First switch
    {
        '"' => ReadString(),
        ':' => ReadByteSequence(),
        _ => ReadMarkedBareItem(),
    };

    private BareItem ReadMarkedBareItem() => First switch
    {
        '?' => ReadBoolean(),
        '@' => ReadDate(),
        '%' => ReadDisplayString(),
        _ => throw Unexpected(),
    };

    private BareItem ReadNumber()
    {
        var sign = Consumes('-') ? "-" : string.Empty;
        if (!char.IsAsciiDigit(First))
        {
            throw new FormatException("a number has no digits");
        }

        var integerPart = ReadWhile(IsDigit);
        return Consumes('.') ? ReadDecimalFraction(sign, integerPart) : Integer(sign, integerPart);
    }

    private static BareItem Integer(string sign, string integerPart) =>
        integerPart.Length > MaximumIntegerDigits
            ? throw new FormatException("an integer has more than 15 digits")
            : new BareItem(BareItemType.Integer, long.Parse(sign + integerPart, CultureInfo.InvariantCulture));

    private BareItem ReadDecimalFraction(string sign, string integerPart)
    {
        var fraction = ReadWhile(IsDigit);
        return integerPart.Length > MaximumIntegerPartDigits || fraction.Length is 0 or > MaximumFractionDigits
            ? throw new FormatException("a decimal has more than 12 integer digits or not 1 to 3 fraction digits")
            : new BareItem(BareItemType.Decimal, decimal.Parse(sign + integerPart + "." + fraction, CultureInfo.InvariantCulture));
    }

    private BareItem ReadString()
    {
        _position++;
        StringBuilder text = new();
        while (true)
        {
            var character = Next("a string is not closed");
            if (character == '"')
            {
                return new BareItem(BareItemType.String, text.ToString());
            }

            text.Append(character == '\\' ? Escaped() : Visible(character));
        }
    }

    private char Escaped()
    {
        var character = Next("a string ends in a backslash");
        return character is '"' or '\\' ? character : throw new FormatException("a string escapes a character other than \" or \\");
    }

    private static char Visible(char character) =>
        character is < ' ' or > '~' ? throw new FormatException("a string holds a control character") : character;

    private BareItem ReadByteSequence()
    {
        _position++;
        var end = _text.IndexOf(':', _position);
        if (end < 0)
        {
            throw new FormatException("a byte sequence is not closed");
        }

        var base64 = _text[_position..end];
        _position = end + 1;
        if (!base64.All(character => char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '='))
        {
            throw new FormatException("a byte sequence is not base64");
        }

        return new BareItem(BareItemType.ByteSequence, DecodeBase64(base64.TrimEnd('=')));
    }

    private static byte[] DecodeBase64(string unpadded)
    {
        try
        {
            return Convert.FromBase64String(unpadded + new string('=', (4 - (unpadded.Length % 4)) % 4));
        }
        catch (FormatException)
        {
            throw new FormatException("a byte sequence is not base64");
        }
    }

    private BareItem ReadBoolean()
    {
        _position++;
        var value = First;
        _position += value is '0' or '1' ? 1 : throw new FormatException("a boolean is not ?0 or ?1");
        return new BareItem(BareItemType.Boolean, value == '1');
    }

    private BareItem ReadDate()
    {
        _position++;
        var number = ReadNumber();
        return number.Type == BareItemType.Integer
            ? number with { Type = BareItemType.Date }
            : throw new FormatException("a date is not an integer");
    }

    private BareItem ReadDisplayString()
    {
        _position++;
        if (!Consumes('"'))
        {
            throw new FormatException("a display string does not start with %\"");
        }

        List<byte> bytes = [];
        while (true)
        {
            var character = Visible(Next("a display string is not closed"));
            if (character == '"')
            {
                return new BareItem(BareItemType.DisplayString, DecodeUtf8([.. bytes]));
            }

            bytes.Add(character == '%' ? PercentEncodedByte() : (byte)character);
        }
    }

    private byte PercentEncodedByte()
    {
        var hex = _position + 2 <= _text.Length ? _text.Substring(_position, 2) : string.Empty;
        _position += hex.Length == 2 && hex.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f')
            ? 2
            : throw new FormatException("a display string's % is not followed by two lowercase hex digits");
        return byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new FormatException("a display string is not UTF-8");
        }
    }

    private static void SetLast<T>(List<T> members, T member, Predicate<T> sameKey)
    {
        var index = members.FindIndex(sameKey);
        if (index < 0)
        {
            members.Add(member);
        }
        else
        {
            members[index] = member;
        }
    }

    private static bool IsLowercaseAlpha(char character) => character is >= 'a' and <= 'z';

    private static bool IsTokenCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~:/".Contains(character);

    private string ReadWhile(Func<char, bool> accepts)
    {
        var start = _position;
        while (!IsEmpty && accepts(_text[_position]))
        {
            _position++;
        }

        return _text[start.._position];
    }

    private char Next(string whenEmpty) => IsEmpty ? throw new FormatException(whenEmpty) : _text[_position++];

    private bool Consumes(char character)
    {
        if (First != character)
        {
            return false;
        }

        _position++;
        return true;
    }

    private void SkipSpaces() => ReadWhile(character => character == ' ');

    private void SkipWhitespace() => ReadWhile(character => character is ' ' or '\t');

    private FormatException Unexpected() => new($"unexpected '{_text[_position]}' at offset {_position}");
}
