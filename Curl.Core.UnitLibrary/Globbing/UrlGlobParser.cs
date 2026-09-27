using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Curl.Core.Globbing;

/// <summary>
/// Reads a URL into <see cref="UrlGlobPiece" />s the way curl 8.21.0's <c>glob_parse</c>,
/// <c>glob_set</c> and <c>glob_range</c> read it, reporting the first malformed glob with
/// the reason and column curl reports.
/// </summary>
/// <remarks>
/// <para>
/// Outside a glob, <c>\</c> escapes only <c>{</c>, <c>[</c>, <c>}</c> and <c>]</c>; a
/// bracketed IPv6 literal (<c>[::1]</c>, <c>[fe80::1%25eth0]</c>) and <c>[]</c> are literal
/// text. Inside a set, <c>\</c> escapes any character, and elements may be empty
/// (<c>{a,}</c>) as long as the set is not <c>{}</c>.
/// </para>
/// <para>
/// A character range is one ASCII letter, <c>-</c>, one character, and <c>]</c> or
/// <c>:step]</c> with a step up to 256; it spans at most 26 characters. A numeric range is
/// digits, <c>-</c>, optional blanks, digits, and <c>]</c> or <c>:step]</c>, every number
/// fitting a <see cref="long" />; a first number with a leading zero pads every value to its
/// digit count. Either range may hold one value only with step 1, and its step may not
/// exceed its span. The product of every glob's value count must fit a <see cref="long" />.
/// </para>
/// </remarks>
internal sealed class UrlGlobParser(string url)
{
    /// <summary>curl's <c>MAX_IP6LEN</c>: a bracketed IPv6 literal is shorter than this.</summary>
    private const int MaxIPv6LiteralLength = 128;

    private const long MaxCharacterStep = 256;

    private const int MaxCharacterSpan = 'z' - 'a';

    private static readonly System.Buffers.SearchValues<char> ZoneCharacters = System.Buffers.SearchValues.Create(
        "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-._~");

    private readonly List<UrlGlobPiece> pieces = [];

    private int index;

    private int closedSetCount;

    /// <summary>Gets the pieces read so far, in URL order.</summary>
    public IReadOnlyList<UrlGlobPiece> Pieces => pieces;

    /// <summary>Gets the product of every piece's value count read so far.</summary>
    public long UrlCount { get; private set; } = 1;

    /// <summary>Reads the whole URL.</summary>
    /// <param name="error">The first malformed glob; <see langword="null" /> when there is none.</param>
    /// <returns><see langword="true" /> when the URL is a well-formed glob.</returns>
    public bool TryParse([NotNullWhen(false)] out UrlGlobError? error)
    {
        error = null;
        while (index < url.Length && error is null)
        {
            error = ReadNextPiece();
        }

        return error is null;
    }

    private UrlGlobError? ReadNextPiece()
    {
        var literal = new StringBuilder();
        UrlGlobError? error = ReadLiteral(literal);
        if (error is not null)
        {
            return error;
        }

        if (literal.Length > 0)
        {
            pieces.Add(UrlGlobPiece.Fixed(literal.ToString()));
            return null;
        }

        char opener = url[index++];
        return opener == '{' ? ReadSet() : ReadRange();
    }

    /// <summary>Reads literal text up to the next <c>{</c> or glob-opening <c>[</c>.</summary>
    private UrlGlobError? ReadLiteral(StringBuilder literal)
    {
        while (index < url.Length && url[index] != '{')
        {
            if (url[index] == '[')
            {
                if (!TryAppendLiteralBracket(literal))
                {
                    return null;
                }
            }
            else if ("}]".Contains(url[index], StringComparison.Ordinal))
            {
                return ErrorAtIndex("unmatched close brace/bracket");
            }
            else
            {
                AppendLiteralCharacter(literal);
            }
        }

        return null;
    }

    private bool TryAppendLiteralBracket(StringBuilder literal)
    {
        int literalLength = LiteralBracketLength();
        if (literalLength == 0)
        {
            return false;
        }

        literal.Append(url, index, literalLength);
        index += literalLength;
        return true;
    }

    /// <summary>
    /// Appends the literal character at the index, or the one after it when a <c>\</c>
    /// escapes a brace or bracket.
    /// </summary>
    private void AppendLiteralCharacter(StringBuilder literal)
    {
        index += IsEscapedGlobCharacter() ? 1 : 0;
        literal.Append(url[index++]);
    }

    private bool IsEscapedGlobCharacter() =>
        url[index] == '\\' && index + 1 < url.Length && "{[}]".Contains(url[index + 1], StringComparison.Ordinal);

    /// <summary>
    /// Gets the length of the <c>[</c> at the current index when it opens literal text - an
    /// IPv6 literal through its <c>]</c>, or <c>[]</c> - and zero when it opens a range.
    /// </summary>
    private int LiteralBracketLength()
    {
        int close = url.IndexOf(']', index);
        if (close < 0)
        {
            return 0;
        }

        int length = close - index + 1;
        bool isLiteral = length == 2
            || (length < MaxIPv6LiteralLength && IsIPv6Literal(url.AsSpan(index + 1, length - 2)));
        return isLiteral ? length : 0;
    }

    /// <summary>
    /// Gets whether the text between brackets is an IPv6 address as curl's URL parser takes
    /// one: hexadecimal digits, colons and dots, then optionally <c>%</c> or <c>%25</c> and a
    /// zone of letters, digits, <c>-</c>, <c>.</c>, <c>_</c> or <c>~</c>.
    /// </summary>
    private static bool IsIPv6Literal(ReadOnlySpan<char> bracketed)
    {
        int zoneStart = bracketed.IndexOf('%');
        ReadOnlySpan<char> address = zoneStart < 0 ? bracketed : bracketed[..zoneStart];
        if (zoneStart >= 0 && !IsZone(bracketed[(zoneStart + 1)..]))
        {
            return false;
        }

        return !address.ContainsAnyExcept("0123456789abcdefABCDEF:.")
            && IPAddress.TryParse(address, out IPAddress? parsed)
            && parsed.AddressFamily == AddressFamily.InterNetworkV6;
    }

    private static bool IsZone(ReadOnlySpan<char> zone)
    {
        ReadOnlySpan<char> name = zone.StartsWith("25") ? zone[2..] : zone;
        return !name.IsEmpty && !name.ContainsAnyExcept(ZoneCharacters);
    }

    /// <summary>Reads a <c>{a,b}</c> set; the index is just past its <c>{</c>.</summary>
    private UrlGlobError? ReadSet()
    {
        int start = index;
        var elements = new List<string>();
        var element = new StringBuilder();
        while (index < url.Length)
        {
            if (url[index] == '}')
            {
                return CloseSet(start, elements, element.ToString());
            }

            UrlGlobError? error = ReadSetCharacter(elements, element);
            if (error is not null)
            {
                return error;
            }
        }

        return ErrorAtIndex("unmatched brace");
    }

    /// <summary>
    /// Reads one character of an open set other than its closing <c>}</c>: a <c>,</c> ends
    /// the element, <c>\</c> escapes the character after it, and an opening brace or
    /// bracket, or a <c>]</c>, is an error.
    /// </summary>
    private UrlGlobError? ReadSetCharacter(List<string> elements, StringBuilder element)
    {
        char character = url[index];
        if ("{[".Contains(character, StringComparison.Ordinal))
        {
            return ErrorAtIndex("nested brace");
        }

        if (character == ']')
        {
            return ErrorAtIndex("unexpected close bracket");
        }

        if (character == ',')
        {
            elements.Add(element.ToString());
            element.Clear();
            index++;
            return null;
        }

        AppendSetCharacter(element);
        return null;
    }

    /// <summary>Appends the set character at the index, or the one after it when it is a <c>\</c>.</summary>
    private void AppendSetCharacter(StringBuilder element)
    {
        index += url[index] == '\\' && index + 1 < url.Length ? 1 : 0;
        element.Append(url[index++]);
    }

    private UrlGlobError? CloseSet(int start, List<string> elements, string lastElement)
    {
        if (index == start)
        {
            return ErrorAtIndex("empty string within braces");
        }

        elements.Add(lastElement);
        index++;
        closedSetCount++;
        if (!TryMultiplyUrlCount(elements.Count))
        {
            return new UrlGlobError("range overflow", 0);
        }

        pieces.Add(UrlGlobPiece.Set(elements));
        return null;
    }

    /// <summary>Reads a <c>[...]</c> range; the index is just past its <c>[</c>.</summary>
    private UrlGlobError? ReadRange()
    {
        char first = index < url.Length ? url[index] : '\0';
        if (char.IsAsciiLetter(first))
        {
            return ReadCharacterRange(first);
        }

        return char.IsAsciiDigit(first)
            ? ReadNumberRange()
            : ErrorAtIndex("bad range specification");
    }

    private UrlGlobError? ReadCharacterRange(char first)
    {
        char last = first;
        long step = 0;
        if (index + 3 < url.Length && url[index + 1] == '-')
        {
            last = url[index + 2];
            step = ReadCharacterRangeEnd(url[index + 3]);
        }

        if (!IsWellFormedCharacterRange(first, last, step))
        {
            return ErrorAtIndex("bad range");
        }

        return AddRange(UrlGlobPiece.CharacterRange(first, (int)step, ((last - first) / step) + 1));
    }

    private UrlGlobError? AddRange(UrlGlobPiece range)
    {
        if (!TryMultiplyUrlCount(range.Count))
        {
            return ErrorAtIndex("range overflow");
        }

        pieces.Add(range);
        return null;
    }

    /// <summary>
    /// Reads what follows <c>x-y</c> in a character range and returns its step, or zero when
    /// it is not <c>]</c> or <c>:step]</c>; moves past <c>x-y]</c>, or past <c>x-y:</c> and as
    /// much of <c>step]</c> as was well formed, and not at all otherwise.
    /// </summary>
    private long ReadCharacterRangeEnd(char end)
    {
        if (end == ']')
        {
            index += 4;
            return 1;
        }

        if (end != ':')
        {
            return 0;
        }

        index += 4;
        return TryReadNumber(MaxCharacterStep, out long step) && TryConsume(']') ? step : 0;
    }

    private static bool IsWellFormedCharacterRange(char first, char last, long step) =>
        step != 0
        && (first == last
            ? step == 1
            : first < last && step <= last - first && last - first <= MaxCharacterSpan);

    private UrlGlobError? ReadNumberRange()
    {
        int padLength = url[index] == '0' ? DigitRunLength() : 0;
        long step = ReadNumberRangeBounds(out long first, out long last);
        if (!IsWellFormedNumberRange(first, last, step))
        {
            return ErrorAtIndex("bad range");
        }

        if (last > long.MaxValue - step)
        {
            return ErrorAtIndex("range end/step overflow");
        }

        return AddRange(UrlGlobPiece.NumberRange(first, step, ((last - first) / step) + 1, padLength));
    }

    /// <summary>
    /// Reads <c>first-last]</c> or <c>first-last:step]</c> as far as it is well formed and
    /// returns the step, or zero when it is not well formed.
    /// </summary>
    private long ReadNumberRangeBounds(out long first, out long last)
    {
        last = 0;
        if (!TryReadNumber(long.MaxValue, out first) || !TryConsume('-'))
        {
            return 0;
        }

        SkipBlanks();
        return TryReadNumber(long.MaxValue, out last) ? ReadNumberStep() : 0;
    }

    /// <summary>Reads <c>]</c> or <c>:step]</c> and returns the step, or zero when neither is there.</summary>
    private long ReadNumberStep()
    {
        if (TryConsume(']'))
        {
            return 1;
        }

        return TryConsume(':') && TryReadNumber(long.MaxValue, out long step) && TryConsume(']') ? step : 0;
    }

    private static bool IsWellFormedNumberRange(long first, long last, long step) =>
        step != 0 && (first == last ? step == 1 : first < last && step <= last - first);

    private int DigitRunLength()
    {
        int length = url.AsSpan(index).IndexOfAnyExceptInRange('0', '9');
        return length < 0 ? url.Length - index : length;
    }

    /// <summary>
    /// Reads the run of ASCII digits at the index as a number of at most
    /// <paramref name="maximum" /> and moves past it; stays put when there is no run or the
    /// number is larger.
    /// </summary>
    private bool TryReadNumber(long maximum, out long number)
    {
        ReadOnlySpan<char> digits = url.AsSpan(index, DigitRunLength());
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number) || number > maximum)
        {
            number = 0;
            return false;
        }

        index += digits.Length;
        return true;
    }

    private bool TryConsume(char expected)
    {
        if (index < url.Length && url[index] == expected)
        {
            index++;
            return true;
        }

        return false;
    }

    private void SkipBlanks()
    {
        while (index < url.Length && url[index] is ' ' or '\t')
        {
            index++;
        }
    }

    /// <summary>
    /// Gets an error at the current index, in the column curl reports: curl moves past each
    /// set's closing <c>}</c> without counting it, so every closed set before the error puts
    /// the column one further left than the character it names.
    /// </summary>
    private UrlGlobError ErrorAtIndex(string reason) => new(reason, index + 1 - closedSetCount);

    private bool TryMultiplyUrlCount(long valueCount)
    {
        if (valueCount > long.MaxValue / UrlCount)
        {
            return false;
        }

        UrlCount *= valueCount;
        return true;
    }
}
