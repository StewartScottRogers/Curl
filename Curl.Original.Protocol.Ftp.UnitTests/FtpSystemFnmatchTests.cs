namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Drives the internal <c>FtpSystemFnmatch</c> through <see cref="FtpWildcardPattern.Match" />: the
/// star, question mark, escape and UTF-8 handling of the Linux (glibc) and macOS system <c>fnmatch</c>
/// (BL-2480). Texts are Latin-1, one character per byte.
/// </summary>
[TestClass]
public sealed class FtpSystemFnmatchTests
{
    [TestMethod]
    [DataRow("ab\0c", "ab", FtpNameMatchAnswer.Match)]
    [DataRow("a", "ab", FtpNameMatchAnswer.NoMatch)]
    [DataRow("ab", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("a?c", "abc", FtpNameMatchAnswer.Match)]
    [DataRow("a**b*", "axxbyy", FtpNameMatchAnswer.Match)]
    [DataRow("*a*b*c*d", "xaybzcwd", FtpNameMatchAnswer.Match)]
    [DataRow("*b", "aaa", FtpNameMatchAnswer.NoMatch)]
    [DataRow("a*b", "ab", FtpNameMatchAnswer.Match)]
    [DataRow("\\a", "a", FtpNameMatchAnswer.Match)]
    [DataRow("\\a", "b", FtpNameMatchAnswer.NoMatch)]
    [DataRow("LindmÃ¤tarv", "LindmÃ¤tarv", FtpNameMatchAnswer.Match)]
    public void Match_PlainPatterns_AnswerAlikeOnLinuxAndMacOs(string pattern, string fileName, FtpNameMatchAnswer expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.MacOs));
    }

    [TestMethod]
    [DataRow("ÿ*", "a")]
    [DataRow("*", "ÿ")]
    [DataRow("a", "aÃ")]
    public void Match_TextThatIsNotUtf8OnLinux_Fails(string pattern, string fileName)
    {
        Assert.AreEqual(FtpNameMatchAnswer.Fail, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
    }

    [TestMethod]
    [DataRow("*", "ÿ", FtpNameMatchAnswer.Match)]
    [DataRow("Ã¿", "ÿ", FtpNameMatchAnswer.Match)]
    [DataRow("ÿ", "ÿ", FtpNameMatchAnswer.NoMatch)]
    [DataRow("*ÿ", "aÿ", FtpNameMatchAnswer.NoMatch)]
    [DataRow("\\ÿ", "ÿ", FtpNameMatchAnswer.NoMatch)]
    public void Match_BytesThatAreNotUtf8OnMacOs_ReadAsMacOsLibcReadsThem(string pattern, string fileName, FtpNameMatchAnswer expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.MacOs));
    }

    [TestMethod]
    public void Match_TrailingBackslash_NeverMatchesOnLinuxAndIsItselfOnMacOs()
    {
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match("a\\", "a\\", FtpWildcardMatcher.Linux));
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match("*\\", "a\\", FtpWildcardMatcher.Linux));
        Assert.AreEqual(FtpNameMatchAnswer.Match, FtpWildcardPattern.Match("a\\", "a\\", FtpWildcardMatcher.MacOs));
    }

    [TestMethod]
    public void Match_ManyStarGroups_MatchesWhereCurlsOwnMatcherStopsAtTwo()
    {
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match("*a*b*", "xaybz", FtpWildcardMatcher.Curl));
        Assert.AreEqual(FtpNameMatchAnswer.Match, FtpWildcardPattern.Match("*a*b*", "xaybz", FtpWildcardMatcher.Linux));
        Assert.AreEqual(FtpNameMatchAnswer.Match, FtpWildcardPattern.Match("*a*b*", "xaybz", FtpWildcardMatcher.MacOs));
    }

    [TestMethod]
    public void Match_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => FtpWildcardPattern.Match(null!, "a", FtpWildcardMatcher.Linux));
        Assert.ThrowsExactly<ArgumentNullException>(() => FtpWildcardPattern.Match("a", null!, FtpWildcardMatcher.Linux));
    }

    [TestMethod]
    [DataRow(true, false, FtpWildcardMatcher.Curl)]
    [DataRow(true, true, FtpWildcardMatcher.Curl)]
    [DataRow(false, true, FtpWildcardMatcher.MacOs)]
    [DataRow(false, false, FtpWildcardMatcher.Linux)]
    public void MatcherFor_Platform_IsThatPlatformsCurlBuildsMatcher(bool runsOnWindows, bool runsOnMacOs, FtpWildcardMatcher expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.MatcherFor(runsOnWindows, runsOnMacOs));
    }

    [TestMethod]
    public void MatchName_DownloadEveryMatchForAPlatform_AnswersAsThatPlatform()
    {
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, new FtpDownloadEveryMatch(FtpWildcardMatcher.Curl).MatchName("[", "["));
        Assert.AreEqual(FtpNameMatchAnswer.Match, new FtpDownloadEveryMatch(FtpWildcardMatcher.Linux).MatchName("[", "["));
        Assert.AreEqual(FtpNameMatchAnswer.Fail, new FtpDownloadEveryMatch(FtpWildcardMatcher.MacOs).MatchName("[", "["));
    }
}
