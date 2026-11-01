namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// One <c>[...]</c> bracket expression read as glibc's <c>fnmatch</c> (<c>posix/fnmatch_loop.c</c>)
/// reads it, flags 0, on code points: a leading <c>!</c> or <c>^</c> negates, a first <c>]</c> is a
/// member, a backslash escapes, <c>a-z</c> is a range in code point order, <c>[:name:]</c> a
/// class, and <c>[=c=]</c> and <c>[.c.]</c> the character <c>c</c>, as in a locale with no collation
/// rules (<c>C.UTF-8</c>). Its quirks are kept: an unknown class name, a trailing backslash, a range
/// with no end, or a <c>[.</c> that names more than one character or never closes fails the match
/// here, and so does a bracket left open after a member already matched, while one left open
/// before any member matched, or a <c>[=</c> at the pattern's end, makes its <c>[</c> an ordinary
/// character, while a <c>[=</c> not closed by <c>=]</c> one character on is an ordinary <c>[</c> member.
/// A <c>[.c.]</c> may start or end a range; a <c>[=c=]</c> starts none.
/// </summary>
internal sealed class FtpGlibcBracket
{
    private const int ClassNameLimit = 256;

    private readonly int[] p;
    private readonly int test;
    private int i;

    private FtpGlibcBracket(int[] pattern, int start, int test)
    {
        p = pattern;
        i = start;
        this.test = test;
    }

    /// <summary>Answers whether <paramref name="test" /> is in the bracket whose content starts at <paramref name="start" />.</summary>
    /// <param name="pattern">The pattern's code points, ending at 0.</param>
    /// <param name="start">Where the bracket's content starts, just past its <c>[</c>.</param>
    /// <param name="test">The string's character, not 0.</param>
    /// <param name="end">Where the pattern goes on past the closing <c>]</c>, when the outcome is <see cref="FtpBracketOutcome.Matched" />.</param>
    /// <returns>The outcome; never <see cref="FtpBracketOutcome.Failed" />.</returns>
    public static FtpBracketOutcome Evaluate(int[] pattern, int start, int test, out int end)
    {
        var bracket = new FtpGlibcBracket(pattern, start, test);
        FtpBracketOutcome outcome = bracket.Answer();
        end = bracket.i;
        return outcome;
    }

    private FtpBracketOutcome Answer()
    {
        bool negated = SkipNegation();
        FtpBracketOutcome found = FindMember();
        if (found == FtpBracketOutcome.Matched && !SkipRest())
        {
            return FtpBracketOutcome.NotMatched;
        }

        return found switch
        {
            FtpBracketOutcome.Unterminated => found,
            FtpBracketOutcome.Failed => FtpBracketOutcome.NotMatched,
            _ => Negate(found == FtpBracketOutcome.Matched, negated),
        };
    }

    private static FtpBracketOutcome Negate(bool matched, bool negated) =>
        matched != negated ? FtpBracketOutcome.Matched : FtpBracketOutcome.NotMatched;

    // glibc's class names: letters from a to y only.
    private static bool IsClassLetter(int c) => c is >= 'a' and < 'z';

    private bool SkipNegation()
    {
        bool negated = p[i] is '!' or '^';
        i += negated ? 1 : 0;
        return negated;
    }

    private bool ClosesClass() => p[i] == ':' && p[i + 1] == ']';

    private bool StartsRange() => p[i] == '-' && p[i + 1] != 0 && p[i + 1] != ']';

    private bool EndsRange(int c) => c == '-' && p[i] != ']';

    // The main loop: Matched once a member holds the character, NotMatched at the closing ],
    // Unterminated at the pattern's end, Failed where glibc returns FNM_NOMATCH whatever the negation.
    private FtpBracketOutcome FindMember()
    {
        int c = p[i++];
        while (true)
        {
            FtpBracketOutcome? outcome = ReadElement(ref c);
            if (outcome is not null)
            {
                return outcome.Value;
            }

            if (c == ']')
            {
                return FtpBracketOutcome.NotMatched;
            }
        }
    }

    // Reads the element c starts and leaves the next one in c; an outcome when the loop ends here.
    private FtpBracketOutcome? ReadElement(ref int c)
    {
        if (c == '\\')
        {
            return ReadEscaped(ref c);
        }

        if (c == '[')
        {
            return ReadOpening(ref c);
        }

        return c == 0 ? FtpBracketOutcome.Unterminated : ReadOrdinary(ref c);
    }

    private FtpBracketOutcome? ReadEscaped(ref int c)
    {
        if (p[i] == 0)
        {
            return FtpBracketOutcome.Failed;
        }

        c = p[i++];
        return ReadOrdinary(ref c);
    }

    private FtpBracketOutcome? ReadOpening(ref int c) => p[i] switch
    {
        ':' => ReadClass(ref c),
        '=' => ReadEquivalence(ref c),
        '.' => ReadSymbol(ref c),
        _ => ReadOrdinary(ref c),
    };

    // From the = of [=, past a character that is not the pattern's end: =] follows it.
    private bool ClosesEquivalence() => p[i + 2] == '=' && p[i + 3] == ']';

    // [=c=]: with no collation rules, the class of c holds c alone, and it starts no range. A [= at the
    // pattern's end leaves the bracket open; one not closed by =] one character on is an ordinary [.
    private FtpBracketOutcome? ReadEquivalence(ref int c)
    {
        if (p[i + 1] == 0)
        {
            return FtpBracketOutcome.Unterminated;
        }

        if (!ClosesEquivalence())
        {
            return ReadOrdinary(ref c);
        }

        int member = p[i + 1];
        i += 4;
        if (member == test)
        {
            return FtpBracketOutcome.Matched;
        }

        c = p[i++];
        return null;
    }

    // From the . of [.: leaves i past the closing .] and answers the name's length, or -1 when it never closes.
    private int ReadSymbolLength()
    {
        int length = 0;
        while (true)
        {
            int c = p[++i];
            if (c == '.' && p[i + 1] == ']')
            {
                i += 2;
                return length;
            }

            if (c == 0)
            {
                return -1;
            }

            length++;
        }
    }

    // From the . of [.: the one character a collating symbol names, or -1 for a longer, empty or open one.
    private int ReadSymbolCharacter()
    {
        int symbol = p[i + 1];
        return ReadSymbolLength() == 1 ? symbol : -1;
    }

    // [.c.]: with no collation rules, only a one-character name; a - after it starts a range even before ].
    private FtpBracketOutcome? ReadSymbol(ref int c)
    {
        int symbol = ReadSymbolCharacter();
        return symbol < 0 ? FtpBracketOutcome.Failed : ReadMember(symbol, p[i] == '-' && p[i + 1] != 0, ref c);
    }

    // [:name:]; letters only from a to y, as glibc tests them, or the [ is an ordinary member.
    private FtpBracketOutcome? ReadClass(ref int c)
    {
        int startp = i;
        var name = new System.Text.StringBuilder();
        while (true)
        {
            if (name.Length == ClassNameLimit)
            {
                return FtpBracketOutcome.Failed;
            }

            c = p[++i];
            if (ClosesClass())
            {
                i += 2;
                return TestClass(name.ToString(), ref c);
            }

            if (!IsClassLetter(c))
            {
                i = startp;
                c = '[';
                return ReadOrdinary(ref c);
            }

            name.Append((char)c);
        }
    }

    private FtpBracketOutcome? TestClass(string name, ref int c)
    {
        Func<int, bool>? contains = FtpWideCharacterClass.Named(name);
        if (contains is null)
        {
            return FtpBracketOutcome.Failed;
        }

        if (contains(test))
        {
            return FtpBracketOutcome.Matched;
        }

        c = p[i++];
        return null;
    }

    // A character and the range it may start: glibc's normal_bracket.
    private FtpBracketOutcome? ReadOrdinary(ref int c) => ReadMember(c, StartsRange(), ref c);

    private FtpBracketOutcome? ReadMember(int member, bool startsRange, ref int c)
    {
        if (!startsRange && member == test)
        {
            return FtpBracketOutcome.Matched;
        }

        c = p[i++];
        return EndsRange(c) ? ReadRangeEnd(member, ref c) : null;
    }

    private FtpBracketOutcome? ReadRangeEnd(int first, ref int c)
    {
        int last = ReadRangeLast();
        if (last <= 0)
        {
            return FtpBracketOutcome.Failed;
        }

        if (first <= test && test <= last)
        {
            return FtpBracketOutcome.Matched;
        }

        c = p[i++];
        return null;
    }

    // A range's end: an escaped character, a [.c.] symbol, or the character itself; 0 or -1 when there is none.
    private int ReadRangeLast()
    {
        int last = p[i++];
        if (last == '[' && p[i] == '.')
        {
            return ReadSymbolCharacter();
        }

        return last == '\\' ? p[i++] : last;
    }

    // After a member matched: skips to the closing ]; false when the bracket never closes.
    private bool SkipRest()
    {
        int c;
        do
        {
            c = p[i++];
            if (c == 0 || !SkipElement(ref c))
            {
                return false;
            }
        }
        while (c != ']');

        return true;
    }

    private bool SkipElement(ref int c)
    {
        if (c == '\\')
        {
            return p[i++] != 0;
        }

        return c != '[' || SkipOpening(ref c);
    }

    private bool SkipOpening(ref int c) => p[i] switch
    {
        ':' => SkipClass(ref c),
        '=' => SkipEquivalence(),
        '.' => ReadSymbolLength() >= 0,
        _ => true,
    };

    // Skips [=c=]; false when it is anything else.
    private bool SkipEquivalence()
    {
        bool closes = p[i + 1] != 0 && ClosesEquivalence();
        i += 4;
        return closes;
    }

    // Skips [:name:] unchecked and reads the character after it into c; a [ that starts no name is skipped alone.
    private bool SkipClass(ref int c)
    {
        int startp = i;
        for (int length = 1; ; length++)
        {
            c = p[++i];
            if (length == ClassNameLimit)
            {
                return false;
            }

            if (ClosesClass())
            {
                i += 2;
                c = p[i++];
                return c != 0;
            }

            if (!IsClassLetter(c))
            {
                i = startp;
                c = '[';
                return true;
            }
        }
    }
}
