namespace Curl.Protocol.Ftp;

/// <summary>
/// Drives the internal <c>FtpGlibcBracket</c> through <see cref="FtpWildcardPattern.Match" /> with
/// <see cref="FtpWildcardMatcher.Linux" />: glibc's <c>fnmatch</c> bracket expressions, quirks
/// included (BL-2480).
/// </summary>
[TestClass]
public sealed class FtpGlibcBracketTests
{
    [TestMethod]
    [DataRow("[!a]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[^a]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[]a]", "]", FtpNameMatchAnswer.Match)]
    [DataRow("[a[]", "[", FtpNameMatchAnswer.Match)]
    [DataRow("[\\]]", "]", FtpNameMatchAnswer.Match)]
    [DataRow("[a-]", "-", FtpNameMatchAnswer.Match)]
    [DataRow("[a-c]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[a-c]", "d", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a-\\c]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[![:digit:]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[[:A:]]", "A]", FtpNameMatchAnswer.Match)]
    [DataRow("[[:zz:]]", "z]", FtpNameMatchAnswer.Match)]
    [DataRow("[[:a:b]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[x[:a:b]", "x", FtpNameMatchAnswer.Match)]
    public void Match_BracketMembers_AnswerAsGlibc(string pattern, string fileName, FtpNameMatchAnswer expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
    }

    [TestMethod]
    [DataRow("[b", "[b", FtpNameMatchAnswer.Match)]
    [DataRow("[b", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a\\", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a\\]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[b]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[:alpha:]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[:x:]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a[:X:]]", "a]", FtpNameMatchAnswer.Match)]
    [DataRow("[a[:z:]]", "a]", FtpNameMatchAnswer.Match)]
    [DataRow("*[a[:foo:]]", "ba", FtpNameMatchAnswer.Match)]
    public void Match_BracketLeftOpenOrSkippedAfterAMatch_AnswersAsGlibc(string pattern, string fileName, FtpNameMatchAnswer expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
    }

    [TestMethod]
    [DataRow("[\\", "a")]
    [DataRow("[!\\", "a")]
    [DataRow("[a-", "b")]
    [DataRow("[![:foo:]]", "a")]
    public void Match_FatalBracketError_NeverMatchesWhateverTheNegation(string pattern, string fileName)
    {
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
    }

    [TestMethod]
    public void Match_ClassNameOf256Letters_NeverMatches()
    {
        string letters = new('a', 256);

        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match("[[:" + letters + ":]]", "a", FtpWildcardMatcher.Linux));
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, FtpWildcardPattern.Match("[a[:" + letters + ":]]", "a", FtpWildcardMatcher.Linux));
    }
}
