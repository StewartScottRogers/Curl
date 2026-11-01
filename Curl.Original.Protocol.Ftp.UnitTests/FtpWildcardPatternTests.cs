namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins <see cref="FtpWildcardPattern" /> on every case of upstream curl 8.22.0's
/// <c>tests/unit/unit1307.c</c>: rows with one answer give it for curl's own <c>Curl_fnmatch</c>
/// (Windows) and the Linux and macOS system <c>fnmatch</c> alike, and the rows marked
/// <c>LINUX_*</c> or <c>MAC_*</c> give each platform's own answer (BL-2476, BL-2480).
/// </summary>
[TestClass]
public sealed class FtpWildcardPatternTests
{
    [TestMethod]
    [DataRow("\\[", "[", true)]
    [DataRow("[][]", "[", true)]
    [DataRow("[][]", "]", true)]
    [DataRow("[[]", "[", true)]
    [DataRow("[[[]", "[", true)]
    [DataRow("[[[[]", "[", true)]
    [DataRow("[][[]", "]", true)]
    [DataRow("[][[[]", "[", true)]
    [DataRow("[[]", "]", false)]
    [DataRow("[a@]", "a", true)]
    [DataRow("[a-z]", "a", true)]
    [DataRow("[a-z]", "A", false)]
    [DataRow("?[a-z]", "?Z", false)]
    [DataRow("[A-Z]", "C", true)]
    [DataRow("[A-Z]", "c", false)]
    [DataRow("[0-9]", "7", true)]
    [DataRow("[7-8]", "7", true)]
    [DataRow("[7-]", "7", true)]
    [DataRow("[7-]", "-", true)]
    [DataRow("[7-]", "[", false)]
    [DataRow("[a-bA-F]", "F", true)]
    [DataRow("[a-bA-B9]", "9", true)]
    [DataRow("[a-bA-B98]", "8", true)]
    [DataRow("[a-bA-B98]", "C", false)]
    [DataRow("[a-bA-Z9]", "F", true)]
    [DataRow("[a-bA-Z9]ero*", "Zero chance.", true)]
    [DataRow("S[a-][x]opho*", "Saxophone", true)]
    [DataRow("S[a-][x]opho*", "SaXophone", false)]
    [DataRow("S[a-][x]*.txt", "S-x.txt", true)]
    [DataRow("[\\a-\\b]", "a", true)]
    [DataRow("[\\a-\\b]", "b", true)]
    [DataRow("[?*[][?*[][?*[]", "?*[", true)]
    [DataRow("[][?*-]", "]", true)]
    [DataRow("[][?*-]", "[", true)]
    [DataRow("[][?*-]", "?", true)]
    [DataRow("[][?*-]", "*", true)]
    [DataRow("[][?*-]", "-", true)]
    [DataRow("[]?*-]", "-", true)]
    [DataRow("?/b/c", "a/b/c", true)]
    [DataRow("^_{}~", "^_{}~", true)]
    [DataRow("!#%+,-./01234567889", "!#%+,-./01234567889", true)]
    [DataRow("PQRSTUVWXYZ]abcdefg", "PQRSTUVWXYZ]abcdefg", true)]
    [DataRow(":;=@ABCDEFGHIJKLMNO", ":;=@ABCDEFGHIJKLMNO", true)]
    public void Matches_BracketSyntax_AnswersAsUnit1307(string pattern, string fileName, bool expected)
    {
        AssertEveryPlatformAnswers(pattern, fileName, expected);
    }

    [TestMethod]
    [DataRow("[!a]", "b", true)]
    [DataRow("[!a]", "a", false)]
    [DataRow("[^a]", "b", true)]
    [DataRow("[^a]", "a", false)]
    [DataRow("[^a-z0-9A-Z]", "a", false)]
    [DataRow("[^a-z0-9A-Z]", "-", true)]
    [DataRow("curl[!a-z]lib", "curl lib", true)]
    [DataRow("curl[! ]lib", "curl lib", false)]
    [DataRow("[! ][ ]", "  ", false)]
    [DataRow("[! ][ ]", "a ", true)]
    [DataRow("*[^a].t?t", "a.txt", false)]
    [DataRow("*[^a].t?t", "ca.txt", false)]
    [DataRow("*[^a].t?t", "ac.txt", true)]
    [DataRow("*[^a]", "", false)]
    [DataRow("[!?*[]", "?", false)]
    [DataRow("[!!]", "!", false)]
    [DataRow("[!!]", "x", true)]
    public void Matches_Negation_AnswersAsUnit1307(string pattern, string fileName, bool expected)
    {
        AssertEveryPlatformAnswers(pattern, fileName, expected);
    }

    [TestMethod]
    [DataRow("[[:alpha:]]", "a", true)]
    [DataRow("[[:alpha:]]", "9", false)]
    [DataRow("[[:alnum:]]", "a", true)]
    [DataRow("[[:alnum:]]", "[", false)]
    [DataRow("[[:alnum:]]", "]", false)]
    [DataRow("[[:alnum:]]", "9", true)]
    [DataRow("[[:digit:]]", "9", true)]
    [DataRow("[[:xdigit:]]", "9", true)]
    [DataRow("[[:xdigit:]]", "F", true)]
    [DataRow("[[:xdigit:]]", "G", false)]
    [DataRow("[[:upper:]]", "U", true)]
    [DataRow("[[:upper:]]", "u", false)]
    [DataRow("[[:lower:]]", "l", true)]
    [DataRow("[[:lower:]]", "L", false)]
    [DataRow("[[:print:]]", "L", true)]
    [DataRow("[[:print:]]", "\b", false)]
    [DataRow("[[:print:]]", "\t", false)]
    [DataRow("[[:print:]]", "\n", false)]
    [DataRow("[[:print:]]", "\r", false)]
    [DataRow("[[:space:]]", " ", true)]
    [DataRow("[[:space:]]", "x", false)]
    [DataRow("[[:graph:]]", " ", false)]
    [DataRow("[[:graph:]]", "x", true)]
    [DataRow("[[:graph:]]", "\t", false)]
    [DataRow("[[:graph:]]", "\r", false)]
    [DataRow("[[:blank:]]", "\t", true)]
    [DataRow("[[:blank:]]", " ", true)]
    [DataRow("[[:blank:]]", "\r", false)]
    [DataRow("[^[:blank:]]", "\t", false)]
    [DataRow("[^[:print:]]", "\b", true)]
    [DataRow("[[:lower:]][[:lower:]]", "ll", true)]
    [DataRow("curl[[:blank:]];-)", "curl ;-)", true)]
    [DataRow("*[[:blank:]]*", " ", true)]
    [DataRow("*[[:blank:]]*", "", false)]
    [DataRow("*[[:blank:]]*", "hi, im_Pavel", true)]
    public void Matches_CharacterClasses_AnswersAsUnit1307(string pattern, string fileName, bool expected)
    {
        AssertEveryPlatformAnswers(pattern, fileName, expected);
    }

    [TestMethod]
    [DataRow("Filename.dat", "Filename.dat", true)]
    [DataRow("*curl*", "lets use curl!!", true)]
    [DataRow("filename.txt", "filename.dat", false)]
    [DataRow("*.txt", "text.txt", true)]
    [DataRow("*.txt", "a.txt", true)]
    [DataRow("*.txt", ".txt", true)]
    [DataRow("*.txt", "txt", false)]
    [DataRow("??.txt", "99.txt", true)]
    [DataRow("??.txt", "a99.txt", false)]
    [DataRow("?.???", "a.txt", true)]
    [DataRow("*.???", "somefile.dat", true)]
    [DataRow("*.???", "photo.jpeg", false)]
    [DataRow(".*", ".htaccess", true)]
    [DataRow(".*", ".", true)]
    [DataRow(".*", "..", true)]
    [DataRow("**.txt", "text.txt", true)]
    [DataRow("***.txt", "t.txt", true)]
    [DataRow("****.txt", ".txt", true)]
    [DataRow("", "", true)]
    [DataRow("", "hello", false)]
    [DataRow("file", "", false)]
    [DataRow("?", "", false)]
    [DataRow("*", "", true)]
    [DataRow("x", "", false)]
    public void Matches_CommonPatterns_AnswersAsUnit1307(string pattern, string fileName, bool expected)
    {
        AssertEveryPlatformAnswers(pattern, fileName, expected);
    }

    [TestMethod]
    [DataRow("\\\\", "\\", true)]
    [DataRow("\\\\", "\\\\", false)]
    [DataRow("\\?", "?", true)]
    [DataRow("\\*", "*", true)]
    [DataRow("?.txt", "?.txt", true)]
    [DataRow("*.txt", "*.txt", true)]
    [DataRow("\\?.txt", "?.txt", true)]
    [DataRow("\\*.txt", "*.txt", true)]
    [DataRow("\\?.txt", "x.txt", false)]
    [DataRow("\\*.txt", "x.txt", false)]
    [DataRow("\\*\\\\.txt", "*\\.txt", true)]
    [DataRow("*\\**\\?*\\\\*", "cc*cc?cccc", false)]
    [DataRow("*\\?*\\**", "cc?cc", false)]
    [DataRow("\\\"\\$\\&\\'\\(\\)", "\"$&'()", true)]
    [DataRow("\\*\\?\\[\\\\\\`\\|", "*?[\\`|", true)]
    [DataRow("[\\a\\b]c", "ac", true)]
    [DataRow("[\\a\\b]c", "bc", true)]
    [DataRow("[\\a\\b]d", "bc", false)]
    [DataRow("[a-bA-B\\?]", "?", true)]
    [DataRow("cu[a-ab-b\\r]l", "curl", true)]
    [DataRow("[\\a-z]", "c", true)]
    [DataRow("?*?*?.*?*", "abc.c", true)]
    [DataRow("?*?*?.*?*", "abcc", false)]
    [DataRow("?*?*?.*?*", "abc.", false)]
    [DataRow("?*?*?.*?*", "abc.c++", true)]
    [DataRow("?*?*?.*?*", "abcdef.c++", true)]
    [DataRow("?*?*?.?", "abcdef.c", true)]
    [DataRow("?*?*?.?", "abcdef.cd", false)]
    [DataRow("LindmÃ¤tarv", "LindmÃ¤tarv", true)]
    public void Matches_BackslashesAndStars_AnswersAsUnit1307(string pattern, string fileName, bool expected)
    {
        AssertEveryPlatformAnswers(pattern, fileName, expected);
    }

    [TestMethod]
    public void Matches_LongRunOfLeftBrackets_DoesNotMatch()
    {
        string pattern = "*[*" + new string('[', 177) + "\u0001\u007F" + new string('[', 21);
        string fileName = new('[', 200);

        Assert.IsFalse(FtpWildcardPattern.Matches(pattern, fileName));
    }

    [TestMethod]
    public void Matches_FuzzedPatternOfStarsAndSets_DoesNotMatch()
    {
        string pattern = "**]*[*[\u0013]**[*\u0013)]*]*[**[*\u0013~r-]*]**[.*]*[" + new string('ã', 29)
            + "*[\u0013]**[*\u0013)]*]*[*[\u0013]*[~r]*]*º\u0013¦~b-]*";

        Assert.IsFalse(FtpWildcardPattern.Matches(pattern, "a"));
    }

    [TestMethod]
    public void Match_LongRunOfLeftBrackets_FailsOnlyOnMacOs()
    {
        string pattern = "*[*" + new string('[', 177) + "\u0001\u007F" + new string('[', 21);
        string fileName = new('[', 200);

        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Curl));
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
        Assert.AreEqual(FtpNameMatchAnswer.Fail, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.MacOs));
    }

    [TestMethod]
    public void Match_FuzzedPatternOfStarsAndSets_FailsOnlyOnLinux()
    {
        string pattern = "**]*[*[\u0013]**[*\u0013)]*]*[**[*\u0013~r-]*]**[.*]*[" + new string('ã', 29)
            + "*[\u0013]**[*\u0013)]*]*[*[\u0013]*[~r]*]*º\u0013¦~b-]*";

        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match(pattern, "a", FtpWildcardMatcher.Curl));
        Assert.AreEqual(FtpNameMatchAnswer.Fail, FtpWildcardPattern.Match(pattern, "a", FtpWildcardMatcher.Linux));
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match(pattern, "a", FtpWildcardMatcher.MacOs));
    }

    // The rows unit1307.c marks LINUX_* or MAC_*: curl's own answer, then glibc's, then macOS libc's.
    [TestMethod]
    [DataRow("[", "[", FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.Match, FtpNameMatchAnswer.Fail)]
    [DataRow("[]", "[]", FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.Match, FtpNameMatchAnswer.Fail)]
    [DataRow("[ÿ]", "ÿ", FtpNameMatchAnswer.Match, FtpNameMatchAnswer.Fail, FtpNameMatchAnswer.Fail)]
    [DataRow("[!ÿ]", "", FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.Fail, FtpNameMatchAnswer.NoMatch)]
    [DataRow("[!ÿ]", "ÿ", FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.Fail, FtpNameMatchAnswer.Fail)]
    [DataRow("[!ÿ]", "a", FtpNameMatchAnswer.Match, FtpNameMatchAnswer.Fail, FtpNameMatchAnswer.Fail)]
    [DataRow("[[:foo:]]", "bar", FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.Fail)]
    [DataRow("[[:foo:]]", "f]", FtpNameMatchAnswer.Match, FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.Fail)]
    [DataRow("\\", "\\", FtpNameMatchAnswer.Match, FtpNameMatchAnswer.NoMatch, FtpNameMatchAnswer.Match)]
    public void Match_RowsWherePlatformsDiffer_AnswersEachPlatformAsUnit1307(
        string pattern, string fileName, FtpNameMatchAnswer curl, FtpNameMatchAnswer linux, FtpNameMatchAnswer macOs)
    {
        Assert.AreEqual(curl, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Curl));
        Assert.AreEqual(linux, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
        Assert.AreEqual(macOs, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.MacOs));
    }

    [TestMethod]
    [DataRow("[[:alpha:][:digit:]]", "5", false)]
    [DataRow("[[:digit:][:alpha:]]", "5", false)]
    [DataRow("[[:alpha:][:digit:]]", "q", true)]
    public void Matches_TwoClassesInOneSet_OnlyTheFirstInCurlsOrderCounts(string pattern, string fileName, bool expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Matches(pattern, fileName));
    }

    [TestMethod]
    [DataRow("[[:abcdefghi:]]", "a]", true)]
    [DataRow("[[:abcdefghi:]]", "[", false)]
    [DataRow("[[:alpha]x]", "[x]", true)]
    [DataRow("[[:alpha]x]", "xx]", false)]
    [DataRow("[a\\", "a", false)]
    [DataRow("[a-\\", "a", false)]
    [DataRow("[a-\\c]", "b", true)]
    [DataRow("[c-a]", "b", false)]
    [DataRow("[]\u0001]", "\u0001", false)]
    [DataRow("[]a]", "a", true)]
    [DataRow("a\0b", "a", true)]
    [DataRow("[a^]", "^", true)]
    [DataRow("[a^]", "b", false)]
    public void Matches_EdgesOfSetSyntax_AnswersAsCurlFnmatch(string pattern, string fileName, bool expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Matches(pattern, fileName));
    }

    [TestMethod]
    public void Matches_ThreeStarGroups_NeverMatches()
    {
        Assert.IsFalse(FtpWildcardPattern.Matches("*a*b*", "xaybz"));
        Assert.IsTrue(FtpWildcardPattern.Matches("*a*b", "xayb"));
    }

    // A row unit1307.c gives one answer for: curl's own matcher, glibc's and macOS libc's all give it.
    private static void AssertEveryPlatformAnswers(string pattern, string fileName, bool matches)
    {
        Assert.AreEqual(matches, FtpWildcardPattern.Matches(pattern, fileName));
        FtpNameMatchAnswer expected = matches ? FtpNameMatchAnswer.Match : FtpNameMatchAnswer.NoMatch;
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Curl));
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux), "Linux");
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.MacOs), "macOS");
    }
}
