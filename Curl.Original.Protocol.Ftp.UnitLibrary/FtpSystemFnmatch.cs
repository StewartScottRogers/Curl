using System.Buffers;
using System.Text;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Matches a file name against an FTP wildcard pattern as the system <c>fnmatch(pattern, string, 0)</c>
/// of the Linux (glibc) and macOS builds does in a UTF-8 locale, which curl 8.22.0 calls instead of its
/// own matcher where <c>HAVE_FNMATCH</c> is set. Both texts are bytes, one character per byte (Latin-1),
/// ending at the first NUL, decoded as UTF-8. <c>*</c> matches any run, as many stars as the pattern
/// holds; the brackets are <see cref="FtpGlibcBracket" /> and <see cref="FtpAppleBracket" />. glibc fails
/// when either text is not UTF-8 and a trailing backslash never matches; macOS fails on a malformed
/// bracket, takes a trailing backslash as itself, and reads a string byte that is not UTF-8 as itself.
/// </summary>
internal sealed class FtpSystemFnmatch
{
    private readonly int[] p;
    private readonly int[] s;
    private readonly bool isMacOs;
    private int pi;
    private int si;
    private int starPattern = -1;
    private int starString;

    private FtpSystemFnmatch(int[] pattern, int[] fileName, bool isMacOs)
    {
        p = pattern;
        s = fileName;
        this.isMacOs = isMacOs;
    }

    private enum Step
    {
        Advance,
        Mismatch,
        Match,
        NoMatch,
        Fail,
    }

    /// <summary>Matches <paramref name="fileName" /> against <paramref name="pattern" />.</summary>
    /// <param name="pattern">The wildcard pattern.</param>
    /// <param name="fileName">The file name from the listing.</param>
    /// <param name="isMacOs"><see langword="true" /> for macOS libc's answers, <see langword="false" /> for glibc's.</param>
    /// <returns>What <c>Curl_fnmatch</c> returns on that platform.</returns>
    public static FtpNameMatchAnswer Match(string pattern, string fileName, bool isMacOs)
    {
        int[] p = Decode(pattern);
        int[] s = Decode(fileName);
        if (isMacOs)
        {
            return new FtpSystemFnmatch(p, ReadInvalidBytesAsThemselves(s), isMacOs: true).Run();
        }

        return HasInvalidByte(p) || HasInvalidByte(s) ? FtpNameMatchAnswer.Fail : new FtpSystemFnmatch(p, s, isMacOs: false).Run();
    }

    // glibc's mbsrtowcs refuses the whole text.
    private static bool HasInvalidByte(int[] codes) => Array.Exists(codes, c => c < 0);

    // macOS's mbrtowc failure on the string reads the byte as itself.
    private static int[] ReadInvalidBytesAsThemselves(int[] codes) => Array.ConvertAll(codes, c => c < 0 ? ~c : c);

    // UTF-8 code points up to the first NUL, ending at 0; a byte that is not UTF-8 is its bitwise complement.
    internal static int[] Decode(string text)
    {
        int nul = text.IndexOf('\0');
        ReadOnlySpan<byte> rest = Encoding.Latin1.GetBytes(nul < 0 ? text : text[..nul]);
        var codes = new List<int>();
        while (!rest.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf8(rest, out Rune rune, out int consumed);
            codes.Add(status == OperationStatus.Done ? rune.Value : ~rest[0]);
            rest = rest[(status == OperationStatus.Done ? consumed : 1)..];
        }

        codes.Add(0);
        return [.. codes];
    }

    private FtpNameMatchAnswer Run()
    {
        while (true)
        {
            Step step = Next();
            if (step == Step.Mismatch)
            {
                step = Backtrack();
            }

            if (step != Step.Advance)
            {
                return step == Step.Match ? FtpNameMatchAnswer.Match : step == Step.Fail ? FtpNameMatchAnswer.Fail : FtpNameMatchAnswer.NoMatch;
            }
        }
    }

    private Step Next()
    {
        int pc = p[pi];
        if (pc == '*')
        {
            return Star();
        }

        if (s[si] == 0)
        {
            return pc == 0 ? Step.Match : Step.NoMatch;
        }

        return pc switch
        {
            0 => Step.Mismatch,
            '?' => Literal(s[si], 1),
            '[' => Bracket(),
            '\\' => Escape(),
            < 0 => Step.NoMatch,
            _ => Literal(pc, 1),
        };
    }

    // A run of stars: the shortest match first, widened one character at a time by Backtrack.
    private Step Star()
    {
        while (p[pi] == '*')
        {
            pi++;
        }

        if (p[pi] == 0)
        {
            return Step.Match;
        }

        starPattern = pi;
        starString = si;
        return Step.Advance;
    }

    private Step Backtrack()
    {
        if (starPattern < 0 || s[starString] == 0)
        {
            return Step.NoMatch;
        }

        starString++;
        pi = starPattern;
        si = starString;
        return Step.Advance;
    }

    // Matches c against the string's character and moves past the patternLength pattern characters it took.
    private Step Literal(int c, int patternLength)
    {
        if (c != s[si])
        {
            return Step.Mismatch;
        }

        pi += patternLength;
        si++;
        return Step.Advance;
    }

    private Step Escape()
    {
        int next = p[pi + 1];
        if (next == 0)
        {
            return isMacOs ? Literal('\\', 1) : Step.Mismatch;
        }

        return next < 0 ? Step.NoMatch : Literal(next, 2);
    }

    private Step Bracket() => isMacOs ? AppleBracket() : GlibcBracket();

    private Step GlibcBracket()
    {
        FtpBracketOutcome outcome = FtpGlibcBracket.Evaluate(p, pi + 1, s[si], out int end);
        if (outcome == FtpBracketOutcome.Matched)
        {
            return MoveTo(end);
        }

        return outcome == FtpBracketOutcome.Unterminated ? Literal('[', 1) : Step.Mismatch;
    }

    private Step AppleBracket()
    {
        FtpBracketOutcome outcome = FtpAppleBracket.Evaluate(p, pi + 1, s[si], out int end);
        if (outcome == FtpBracketOutcome.Matched)
        {
            return MoveTo(end);
        }

        return outcome == FtpBracketOutcome.Failed ? Step.Fail : Step.Mismatch;
    }

    private Step MoveTo(int end)
    {
        pi = end;
        si++;
        return Step.Advance;
    }
}
