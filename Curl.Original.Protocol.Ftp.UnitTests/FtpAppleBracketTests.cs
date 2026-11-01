namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Drives the internal <c>FtpAppleBracket</c> through <see cref="FtpWildcardPattern.Match" /> with
/// <see cref="FtpWildcardMatcher.MacOs" />: macOS libc's <c>fnmatch</c> bracket expressions, where a
/// malformed bracket fails the whole match (BL-2480).
/// </summary>
[TestClass]
public sealed class FtpAppleBracketTests
{
    [TestMethod]
    [DataRow("[!a]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[^a]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[]a]", "]", FtpNameMatchAnswer.Match)]
    [DataRow("[\\]]", "]", FtpNameMatchAnswer.Match)]
    [DataRow("[a-]", "-", FtpNameMatchAnswer.Match)]
    [DataRow("[a-c]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[a-c]", "d", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a-\\c]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[[:alpha:]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[[:digit:]]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[[:ab]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[[:a:x]", "x", FtpNameMatchAnswer.Match)]
    public void Match_BracketMembers_AnswerAsMacOsLibc(string pattern, string fileName, FtpNameMatchAnswer expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.MacOs));
    }

    [TestMethod]
    [DataRow("[ab", "a")]
    [DataRow("[a-", "a")]
    [DataRow("[\\", "a")]
    [DataRow("[a-ÿ]", "b")]
    [DataRow("[a-\\", "b")]
    [DataRow("[[:aÿ:]]", "a")]
    [DataRow("[[:foo:]]", "a")]
    public void Match_MalformedBracket_Fails(string pattern, string fileName)
    {
        Assert.AreEqual(FtpNameMatchAnswer.Fail, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.MacOs));
    }
}
