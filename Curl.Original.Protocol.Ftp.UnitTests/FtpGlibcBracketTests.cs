namespace Curl.Original.Protocol.Ftp;

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

    // Measured from glibc 2.43 in C.UTF-8 through find -name (BL-2482).
    [TestMethod]
    [DataRow("[[=a=]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[[=a=]]", "b", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[![=a=]]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[![=a=]]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[[=-=]]", "-", FtpNameMatchAnswer.Match)]
    [DataRow("[[=]=]]", "]", FtpNameMatchAnswer.Match)]
    [DataRow("[[=Ã©=]]", "Ã©", FtpNameMatchAnswer.Match)]
    [DataRow("[[=ab=]]", "a]", FtpNameMatchAnswer.Match)]
    [DataRow("[[=a]", "=", FtpNameMatchAnswer.Match)]
    [DataRow("[[=", "[[=", FtpNameMatchAnswer.Match)]
    [DataRow("[[=a=]-c]", "b", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[[=a=]-c]", "-", FtpNameMatchAnswer.Match)]
    [DataRow("[[.a.]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[![.a.]]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[[.-.]]", "-", FtpNameMatchAnswer.Match)]
    [DataRow("[[.].]]", "]", FtpNameMatchAnswer.Match)]
    [DataRow("[[.=.]]", "=", FtpNameMatchAnswer.Match)]
    [DataRow("[[.Ã©.]]", "Ã©", FtpNameMatchAnswer.Match)]
    [DataRow("[\\[.a.]]", "a]", FtpNameMatchAnswer.Match)]
    [DataRow("[[.a.]-c]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[[.a.]-]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[[.a.]-]", "-", FtpNameMatchAnswer.Match)]
    [DataRow("[a-[.c.]]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[[.a.]-[.c.]]", "c", FtpNameMatchAnswer.Match)]
    [DataRow("[[.a.]-\\c]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[a-\\[.c.]]", ".]", FtpNameMatchAnswer.Match)]
    [DataRow("[b-[=]", "=", FtpNameMatchAnswer.Match)]
    public void Match_EquivalenceClassOrCollatingSymbol_AnswersAsGlibcInCUtf8(string pattern, string fileName, FtpNameMatchAnswer expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
    }

    // Measured from glibc 2.43 in C.UTF-8 through find -name (BL-2482).
    [TestMethod]
    [DataRow("[a[=x=]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[=x=]]", "x", FtpNameMatchAnswer.Match)]
    [DataRow("[[=a=][.b.]]", "b", FtpNameMatchAnswer.Match)]
    [DataRow("[a[.x.]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[.xy.]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[.].]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[.\\].]]", "a", FtpNameMatchAnswer.Match)]
    [DataRow("[a[=xy=]]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a[=xy=]]", "x]", FtpNameMatchAnswer.Match)]
    [DataRow("[a[=xy=]]", "[]", FtpNameMatchAnswer.Match)]
    [DataRow("[x[=xy=]]", "x]", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a[=]]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a[=]]", "[]", FtpNameMatchAnswer.Match)]
    [DataRow("[a[=", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a[=", "[a[=", FtpNameMatchAnswer.Match)]
    [DataRow("[a[.]", "a", FtpNameMatchAnswer.NoMatch)]
    [DataRow("[a[.x]]", "a", FtpNameMatchAnswer.NoMatch)]
    public void Match_EquivalenceClassOrCollatingSymbolSkippedAfterAMatch_AnswersAsGlibc(string pattern, string fileName, FtpNameMatchAnswer expected)
    {
        Assert.AreEqual(expected, FtpWildcardPattern.Match(pattern, fileName, FtpWildcardMatcher.Linux));
    }

    [TestMethod]
    [DataRow("[[.ab.]]", "a")]
    [DataRow("[![.ab.]]", "x")]
    [DataRow("[[..]]", "a")]
    [DataRow("[![..]]", "a")]
    [DataRow("[[.a]", "[")]
    [DataRow("[x[.", "x")]
    [DataRow("[a-[.cd.]]", "b")]
    [DataRow("[a-[.c]", "b")]
    [DataRow("[[.a.]-", "-")]
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
