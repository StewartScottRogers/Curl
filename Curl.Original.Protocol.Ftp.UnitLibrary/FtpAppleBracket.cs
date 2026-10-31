namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// One <c>[...]</c> bracket expression read as macOS libc's <c>fnmatch</c> (FreeBSD's
/// <c>rangematch</c>) reads it, flags 0, on code points: a leading <c>!</c> or <c>^</c> negates, a
/// first <c>]</c> is a member, a backslash escapes, <c>a-z</c> is a range in code point order, and
/// <c>[:name:]</c> a class. A bracket left open, an unknown class name, a range with no end or a byte
/// that is not UTF-8 fails the whole match, as upstream's <c>unit1307.c</c> pins for macOS.
/// </summary>
internal sealed class FtpAppleBracket
{
    private readonly int[] p;
    private readonly int test;
    private int i;
    private bool found;

    private FtpAppleBracket(int[] pattern, int start, int test)
    {
        p = pattern;
        i = start;
        this.test = test;
    }

    /// <summary>Answers whether <paramref name="test" /> is in the bracket whose content starts at <paramref name="start" />.</summary>
    /// <param name="pattern">The pattern's code points, ending at 0, a byte that is not UTF-8 as a negative number.</param>
    /// <param name="start">Where the bracket's content starts, just past its <c>[</c>.</param>
    /// <param name="test">The string's character, not 0.</param>
    /// <param name="end">Where the pattern goes on past the closing <c>]</c>, when the outcome is <see cref="FtpBracketOutcome.Matched" />.</param>
    /// <returns>The outcome; never <see cref="FtpBracketOutcome.Unterminated" />.</returns>
    public static FtpBracketOutcome Evaluate(int[] pattern, int start, int test, out int end)
    {
        var bracket = new FtpAppleBracket(pattern, start, test);
        FtpBracketOutcome outcome = bracket.Answer();
        end = bracket.i;
        return outcome;
    }

    private FtpBracketOutcome Answer()
    {
        bool negated = SkipNegation();
        int origin = i;
        while (!Closes(origin))
        {
            if (!ReadElement())
            {
                return FtpBracketOutcome.Failed;
            }
        }

        i++;
        return found != negated ? FtpBracketOutcome.Matched : FtpBracketOutcome.NotMatched;
    }

    private bool SkipNegation()
    {
        bool negated = p[i] is '!' or '^';
        i += negated ? 1 : 0;
        return negated;
    }

    // A ] closes the bracket unless it comes first.
    private bool Closes(int origin) => p[i] == ']' && i != origin;

    private bool StartsClass() => p[i] == '[' && p[i + 1] == ':';

    private bool StartsRange() => p[i] == '-' && p[i + 1] != 0 && p[i + 1] != ']';

    private bool ClassNameGoesOn(int close) => p[close] > 0 && !(p[close] == ':' && p[close + 1] == ']');

    // One class, character or range; false when the bracket is malformed.
    private bool ReadElement()
    {
        int? read = StartsClass() ? ReadClass() : null;
        return read is null ? ReadCharacterOrRange() : read.Value != 0;
    }

    private bool ReadCharacterOrRange()
    {
        i += p[i] == '\\' ? 1 : 0;
        int first = p[i++];
        if (first <= 0)
        {
            return false;
        }

        return StartsRange() ? ReadRangeEnd(first) : Note(first == test);
    }

    // [:name:]: 1 when read, 0 when the name is unknown, null when no ":]" closes it and the [ is a member.
    private int? ReadClass()
    {
        int close = i + 2;
        while (ClassNameGoesOn(close))
        {
            close++;
        }

        if (p[close] <= 0)
        {
            return null;
        }

        string name = string.Concat(p[(i + 2)..close].Select(c => char.ConvertFromUtf32(c)));
        Func<int, bool>? contains = FtpWideCharacterClass.Named(name);
        i = close + 2;
        if (contains is null)
        {
            return 0;
        }

        Note(contains(test));
        return 1;
    }

    private bool ReadRangeEnd(int first)
    {
        i++;
        if (p[i] == '\\')
        {
            i++;
        }

        int last = p[i++];
        return last > 0 && Note(first <= test && test <= last);
    }

    private bool Note(bool holds)
    {
        found |= holds;
        return true;
    }
}
