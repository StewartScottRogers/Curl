namespace Curl.Protocol.Ftp;

/// <summary>
/// One <c>[...]</c> bracket expression read as glibc's <c>fnmatch</c> (<c>posix/fnmatch_loop.c</c>)
/// reads it, flags 0, on code points: a leading <c>!</c> or <c>^</c> negates, a first <c>]</c> is a
/// member, a backslash escapes, <c>a-z</c> is a range in code point order, and <c>[:name:]</c> a
/// class. Its quirks are kept: an unknown class name, a trailing backslash or a range with no end
/// fails the match here, and so does a bracket left open after a member already matched, while one
/// left open before any member matched makes its <c>[</c> an ordinary character.
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
            if (p[i] == 0)
            {
                return FtpBracketOutcome.Failed;
            }

            c = p[i++];
            return ReadOrdinary(ref c);
        }

        if (c == '[' && p[i] == ':')
        {
            return ReadClass(ref c);
        }

        return c == 0 ? FtpBracketOutcome.Unterminated : ReadOrdinary(ref c);
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
    private FtpBracketOutcome? ReadOrdinary(ref int c)
    {
        if (!StartsRange() && c == test)
        {
            return FtpBracketOutcome.Matched;
        }

        int first = c;
        c = p[i++];
        return EndsRange(c) ? ReadRangeEnd(first, ref c) : null;
    }

    private FtpBracketOutcome? ReadRangeEnd(int first, ref int c)
    {
        int last = p[i++];
        if (last == '\\')
        {
            last = p[i++];
        }

        if (last == 0)
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

        return c != '[' || p[i] != ':' || SkipClass(ref c);
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
