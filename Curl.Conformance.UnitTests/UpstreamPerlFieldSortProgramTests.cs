using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamPerlFieldSortProgram"/>: that it reads test1286's strip program and
/// runs it on matching and non-matching lines as Perl would, and which Perl it does not read.
/// </summary>
[TestClass]
public sealed class UpstreamPerlFieldSortProgramTests
{
    private const string Test1286Program =
        "if(s/^(Authorization: Digest )([^\\r\\n]+)(\\r?\\n)$//) { $_ = $1 . join(', ', map { s/^(cnonce=)\"[a-zA-Z0-9+\\/=]+\"$/$1REMOVED/; s/^(response=)\"[a-f0-9]{32}\"$/$1REMOVED/; s/^qop=\"auth\"$/qop=auth/; $_ } sort split(/, */, $2)) . $3; }";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(
        "Authorization: Digest username=\"auser\", realm=\"testrealm\", nonce=\"1053604144\", uri=\"/1286\", cnonce=\"MTIzNDU2Nzg5MA==\", nc=00000001, qop=auth, response=\"0123456789abcdef0123456789abcdef\"\r\n",
        "Authorization: Digest cnonce=REMOVED, nc=00000001, nonce=\"1053604144\", qop=auth, realm=\"testrealm\", response=REMOVED, uri=\"/1286\", username=\"auser\"\r\n")]
    [DataRow("Authorization: Digest qop=\"auth\",b=1,  a=2\n", "Authorization: Digest a=2, b=1, qop=auth\n")]
    [DataRow("Authorization: Digest b, a, , \r\n", "Authorization: Digest a, b\r\n")]
    [DataRow("Authorization: Digest , , \r\n", "Authorization: Digest \r\n")]
    [DataRow("Authorization: Digest , b, a\r\n", "Authorization: Digest , a, b\r\n")]
    [DataRow("Authorization: Digest a=1, B=2, _=3\r\n", "Authorization: Digest B=2, _=3, a=1\r\n")]
    [DataRow("Authorization: Digest response=\"XYZ\", cnonce=\"a b\"\r\n", "Authorization: Digest cnonce=\"a b\", response=\"XYZ\"\r\n")]
    [DataRow("Authorization: Basic b, a\r\n", "Authorization: Basic b, a\r\n")]
    [DataRow("Authorization: Digest b, a", "Authorization: Digest b, a")]
    [DataRow("Host: 127.0.0.1\r\n", "Host: 127.0.0.1\r\n")]
    public void Apply_Test1286Program_SortsTheDigestFieldsAsPerlDoes(string line, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", line);

        UpstreamPerlFieldSortProgram? program = UpstreamPerlFieldSortProgram.Parse(Test1286Program);

        diagnostics.Act("parsed", program is not null);
        Assert.IsNotNull(program);
        string actual = program.Apply(line);
        diagnostics.Act("applied", actual);
        diagnostics.Diff("applied", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Parse_GroupNumbersOtherThanOneTwoThree_TakesThemFromTheProgram()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        const string code = "if(s/^(x)(y)(.*)(\\n)$//) { $_ = $1 . join('|', map { s/^b$/B/; $_ } sort split(/,/, $3)) . $4; }";
        diagnostics.Arrange("code", code);

        string? actual = UpstreamPerlFieldSortProgram.Parse(code)?.Apply("xyc,b,a\n");

        diagnostics.Act("applied", actual);
        Assert.AreEqual("xa|B|c\n", actual);
    }

    [TestMethod]
    [DataRow("s/a/b/")]
    [DataRow("if(s/a//) { $_ = $1; }")]
    [DataRow("if(y/a//) { $_ = $1 . join(', ', map { s/a/b/; $_ } sort split(/, */, $2)) . $3; }")]
    [DataRow("if(s/(//) { $_ = $1 . join(', ', map { s/a/b/; $_ } sort split(/, */, $2)) . $3; }")]
    [DataRow("if(s/a//) { $_ = $1 . join(', ', map { s/a/b/; y/a/b/; $_ } sort split(/, */, $2)) . $3; }")]
    [DataRow("if(s/a//) { $_ = $1 . join(', ', map { s/a/b/; $_ } sort split(/(/, $2)) . $3; }")]
    public void Parse_OtherPerl_ReturnsNull(string code)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code", code);

        UpstreamPerlFieldSortProgram? program = UpstreamPerlFieldSortProgram.Parse(code);

        diagnostics.Act("parsed", program is not null);
        Assert.IsNull(program);
    }

    [TestMethod]
    [DataRow("s/a/b/", "a\n", "b\n")]
    [DataRow(Test1286Program, "Authorization: Digest b, a\r\n", "Authorization: Digest a, b\r\n")]
    public void LineEditParse_SubstitutionOrFieldSortProgram_RunsIt(string code, string line, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code", code);

        string? actual = IUpstreamPerlLineEdit.Parse(code)?.Apply(line);

        diagnostics.Act("applied", actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void LineEditParse_OtherPerl_ReturnsNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        IUpstreamPerlLineEdit? edit = IUpstreamPerlLineEdit.Parse("$_ = ''");

        diagnostics.Act("parsed", edit is not null);
        Assert.IsNull(edit);
    }
}
