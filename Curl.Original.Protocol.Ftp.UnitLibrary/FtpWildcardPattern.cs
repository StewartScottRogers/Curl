using System.Text;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Matches a file name against an FTP wildcard pattern exactly as upstream curl 8.22.0's own
/// <c>Curl_fnmatch</c> (<c>lib/curl_fnmatch.c</c>) does: <c>*</c>, <c>?</c>, <c>[...]</c> sets
/// with ranges, <c>!</c> or <c>^</c> negation and <c>[:alpha:]</c>-style classes, and backslash
/// escapes. Its quirks are kept: at most two groups of stars match, a malformed set never
/// matches, and only the first class named in a set counts. Both texts are bytes, one character
/// per byte (Latin-1), ending at the first NUL as C strings do.
/// </summary>
public static class FtpWildcardPattern
{
    private const int MaxStarGroups = 2;

    /// <summary>Tells whether <paramref name="fileName" /> matches <paramref name="pattern" />.</summary>
    /// <param name="pattern">The wildcard pattern, the last part of the URL's path.</param>
    /// <param name="fileName">The file name from the listing.</param>
    /// <returns><see langword="true" /> where curl's <c>Curl_fnmatch</c> returns <c>CURL_FNMATCH_MATCH</c>.</returns>
    public static bool Matches(string pattern, string fileName)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(fileName);
        return MatchFrom(ToCString(pattern), 0, ToCString(fileName), 0, MaxStarGroups);
    }

    /// <summary>
    /// Matches <paramref name="fileName" /> against <paramref name="pattern" /> as the given platform's
    /// curl does: curl's own matcher (<see cref="Matches" />) or the system <c>fnmatch</c> of the Linux
    /// or macOS build (BL-2480).
    /// </summary>
    /// <param name="pattern">The wildcard pattern, the last part of the URL's path.</param>
    /// <param name="fileName">The file name from the listing.</param>
    /// <param name="matcher">Which platform's <c>Curl_fnmatch</c> to answer as.</param>
    /// <returns>What that platform's <c>Curl_fnmatch</c> returns.</returns>
    public static FtpNameMatchAnswer Match(string pattern, string fileName, FtpWildcardMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(fileName);
        if (matcher == FtpWildcardMatcher.Curl)
        {
            return Matches(pattern, fileName) ? FtpNameMatchAnswer.Match : FtpNameMatchAnswer.NoMatch;
        }

        return FtpSystemFnmatch.Match(pattern, fileName, matcher == FtpWildcardMatcher.MacOs);
    }

    /// <summary>The matcher the curl build of a platform uses: its own on Windows, the system <c>fnmatch</c> elsewhere.</summary>
    /// <param name="runsOnWindows">Whether Curl runs on Windows.</param>
    /// <param name="runsOnMacOs">Whether Curl runs on macOS.</param>
    /// <returns>The platform's matcher.</returns>
    public static FtpWildcardMatcher MatcherFor(bool runsOnWindows, bool runsOnMacOs) =>
        runsOnWindows ? FtpWildcardMatcher.Curl : runsOnMacOs ? FtpWildcardMatcher.MacOs : FtpWildcardMatcher.Linux;

    private static byte[] ToCString(string text) => Encoding.Latin1.GetBytes(text + '\0');

    // curl's loop(): p walks the pattern and s the string, both ending at a NUL.
    private static bool MatchFrom(byte[] p, int pi, byte[] s, int si, int starGroupsLeft)
    {
        while (true)
        {
            byte c = p[pi];
            if (c == '*')
            {
                return MatchStars(p, pi, s, si, starGroupsLeft);
            }

            if (c == 0)
            {
                return s[si] == 0;
            }

            if (!MatchOne(p, ref pi, s, ref si))
            {
                return false;
            }
        }
    }

    // One pattern element that is not a star: ?, a backslash escape, a set or a plain byte.
    private static bool MatchOne(byte[] p, ref int pi, byte[] s, ref int si)
    {
        byte c = p[pi];
        if (c == '[')
        {
            return MatchSet(p, ref pi, s, ref si);
        }

        if (c == '?')
        {
            pi++;
            return s[si++] != 0;
        }

        if (c == '\\' && p[pi + 1] != 0)
        {
            pi++;
        }

        return p[pi++] == s[si++];
    }

    // Consecutive stars and question marks are one group: '*?*?*' is '??*'.
    private static bool MatchStars(byte[] p, int pi, byte[] s, int si, int starGroupsLeft)
    {
        if (starGroupsLeft == 0 || !SkipStarGroup(p, ref pi, s, ref si))
        {
            return false;
        }

        return p[pi] == 0 || MatchAfterStars(p, pi, s, si, starGroupsLeft - 1);
    }

    // Moves past the group; false when one of its question marks finds the string ended.
    private static bool SkipStarGroup(byte[] p, ref int pi, byte[] s, ref int si)
    {
        for (pi++; IsStarOrQuestionMark(p[pi]); pi++)
        {
            if (p[pi] == '?' && s[si++] == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsStarOrQuestionMark(byte c) => c == '*' || c == '?';

    // Tries the rest of the pattern at every remaining position of the string.
    private static bool MatchAfterStars(byte[] p, int pi, byte[] s, int si, int starGroupsLeft)
    {
        for (; s[si] != 0; si++)
        {
            if (MatchFrom(p, pi, s, si, starGroupsLeft))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchSet(byte[] p, ref int pi, byte[] s, ref int si)
    {
        FtpWildcardSet? set = FtpWildcardSet.Read(p, pi + 1, out int end);
        if (set is null || s[si] == 0)
        {
            return false;
        }

        pi = end + 1;
        return set.Contains(s[si++]);
    }
}
