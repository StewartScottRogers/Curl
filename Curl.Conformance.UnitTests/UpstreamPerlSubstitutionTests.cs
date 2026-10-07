using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamPerlSubstitution"/>: which lines of a <c>&lt;strippart&gt;</c> or
/// <c>&lt;stripfile&gt;</c> it runs, and that it runs them as Perl's <c>s///</c> would.
/// </summary>
[TestClass]
public sealed class UpstreamPerlSubstitutionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("", true)]
    [DataRow("   ", true)]
    [DataRow("# a comment", true)]
    [DataRow("s/a/b/", false)]
    public void DoesNothing_IsTrueForBlankAndCommentLines(string line, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", line);

        bool actual = UpstreamPerlSubstitution.DoesNothing(line);

        diagnostics.Act("does nothing", actual);
        diagnostics.Assert("does nothing", expected, actual);
        Assert.AreEqual(expected, actual);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code", code);
        diagnostics.Arrange("line", line);

        UpstreamPerlSubstitution? substitution = UpstreamPerlSubstitution.Parse(code);

        diagnostics.Act("parsed", substitution is not null);
        diagnostics.Assert("parsed", true, substitution is not null);
        Assert.IsNotNull(substitution);
        string actual = substitution.Apply(line);
        diagnostics.Act("applied", actual);
        diagnostics.Diff("applied", expected, actual);
        Assert.AreEqual(expected, actual);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code", code);

        UpstreamPerlSubstitution? substitution = UpstreamPerlSubstitution.Parse(code);

        diagnostics.Act("parsed", substitution is not null);
        diagnostics.Assert("parsed", false, substitution is not null);
        Assert.IsNull(substitution);
    }
}
