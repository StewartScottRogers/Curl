namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamPerlSubstitution"/>: which lines of a <c>&lt;strippart&gt;</c> or
/// <c>&lt;stripfile&gt;</c> it runs, and that it runs them as Perl's <c>s///</c> would.
/// </summary>
[TestClass]
public sealed class UpstreamPerlSubstitutionTests
{
    [TestMethod]
    [DataRow("", true)]
    [DataRow("   ", true)]
    [DataRow("# a comment", true)]
    [DataRow("s/a/b/", false)]
    public void DoesNothing_IsTrueForBlankAndCommentLines(string line, bool expected)
    {
        Assert.AreEqual(expected, UpstreamPerlSubstitution.DoesNothing(line));
    }

    [TestMethod]
    [DataRow("s/b/X/", "abcabc\n", "aXcabc\n")]
    [DataRow("s/b/X/g", "abcabc\n", "aXcaXc\n")]
    [DataRow(" s/B/X/gi; ", "abcabc\n", "aXcaXc\n")]
    [DataRow("s/^Date:.*\\n//", "Date: now\n", "")]
    [DataRow("s/(\\d+)/<$1>/", "a12b\n", "a<12>b\n")]
    [DataRow("s|/path|X|", "a/path\n", "aX\n")]
    [DataRow("s/a\\/b/c\\/d/", "a/b\n", "c/d\n")]
    [DataRow("s/^b$/X/m", "b\n", "X\n")]
    [DataRow("s/a.b/X/s", "a\nb", "X")]
    [DataRow("s/a b/X/x", "ab", "X")]
    public void Parse_RunsTheSubstitutionAsPerlDoes(string code, string line, string expected)
    {
        UpstreamPerlSubstitution? substitution = UpstreamPerlSubstitution.Parse(code);

        Assert.IsNotNull(substitution);
        Assert.AreEqual(expected, substitution.Apply(line));
    }

    [TestMethod]
    [DataRow("$_ = ''")]
    [DataRow("s")]
    [DataRow("sa/b/")]
    [DataRow("y/a/b/")]
    [DataRow("s/a/b")]
    [DataRow("s/a/b/c/d")]
    [DataRow("s/a/b/e")]
    [DataRow("s/(/b/")]
    [DataRow("s/a/b/\\")]
    public void Parse_ReturnsNullForOtherPerl(string code)
    {
        Assert.IsNull(UpstreamPerlSubstitution.Parse(code));
    }
}
