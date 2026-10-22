using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamPerlConditionalEmptying"/>: that it reads test1506's and test1542's strip lines and
/// empties a line as Perl's <c>$_ = '' if(...)</c> would, and which Perl it does not read.
/// </summary>
[TestClass]
public sealed class UpstreamPerlConditionalEmptyingTests
{
    private const string Test1506Line = "$_ = '' if(($_ !~ /left intact/) and ($_ !~ /Closing connection/))";
    private const string Test1542Line = @"$_ = '' if(($_ !~ /left intact/) and ($_ !~ /(closing|shutting down) connection #\d+/))";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("* Connection #0 to host server1.example.com:8990 left intact\n", "* Connection #0 to host server1.example.com:8990 left intact\n")]
    [DataRow("* Closing connection 3\n", "* Closing connection 3\n")]
    [DataRow("* Connected to server1.example.com (127.0.0.1) port 8990\n", "")]
    [DataRow("> GET /1506 HTTP/1.1\r\n", "")]
    public void Apply_Test1506Line_KeepsOnlyTheConnectionLines(string line, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", line);

        UpstreamPerlConditionalEmptying? edit = UpstreamPerlConditionalEmptying.Parse(Test1506Line);

        diagnostics.Act("parsed", edit is not null);
        Assert.IsNotNull(edit);
        string actual = edit.Apply(line);
        diagnostics.Act("applied", actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("$_ = '' if(($_ =~ /drop/));", "drop me\n", "")]
    [DataRow("$_ = '' if(($_ =~ /drop/));", "keep me\n", "keep me\n")]
    [DataRow("$_ = '' if(($_ !~ /left intact/) and ($_ !~ /(closing|shutting down) connection #\\d+/))", "* shutting down connection #2\n", "* shutting down connection #2\n")]
    [DataRow("$_ = '' if(($_ !~ /left intact/) and ($_ !~ /(closing|shutting down) connection #\\d+/))", "* closing connection\n", "")]
    public void Apply_OtherTerms_EmptiesTheLineOnlyWhenEveryTermHolds(string code, string line, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code", code);

        string? actual = UpstreamPerlConditionalEmptying.Parse(code)?.Apply(line);

        diagnostics.Act("applied", actual);
        Assert.AreEqual(expected, actual);
    }

    // The --libcurl cases' one-term form (BL-2862): test1400's, and test1404's with escaped slashes.
    [TestMethod]
    [DataRow("$_ = '' if /CURLOPT_SSL_VERIFYPEER/", "  curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);\n", "")]
    [DataRow("$_ = '' if /CURLOPT_SSL_VERIFYPEER/", "  curl_easy_setopt(curl, CURLOPT_URL, \"x\");\n", "  curl_easy_setopt(curl, CURLOPT_URL, \"x\");\n")]
    [DataRow(@"$_ = '' if /\/\* ""value"" \*\//", "  /* \"value\" */\n", "")]
    [DataRow(@"$_ = '' if /\/\* ""value"" \*\//", "  \"value\"\n", "  \"value\"\n")]
    public void Apply_BareMatchLine_EmptiesTheLinesThePatternMatches(string code, string line, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code", code);

        string? actual = IUpstreamPerlLineEdit.Parse(code)?.Apply(line);

        diagnostics.Act("applied", actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("$_ = ''")]
    [DataRow("$_ = '' if /(/")]
    [DataRow("$_ = '' if /a/b/")]
    [DataRow("$_ = '' if(($_ !~ /a/) or ($_ !~ /b/))")]
    [DataRow("$_ = '' if((($_ !~ /a/) and ($_ !~ /b/)) || ($_ =~ /c/))")]
    [DataRow("$_ = '' if(($_ !~ /(/))")]
    public void Parse_OtherPerl_ReturnsNull(string code)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("code", code);

        UpstreamPerlConditionalEmptying? edit = UpstreamPerlConditionalEmptying.Parse(code);

        diagnostics.Act("parsed", edit is not null);
        Assert.IsNull(edit);
    }

    [TestMethod]
    public void LineEditParse_Test1506Line_RunsIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        string? actual = IUpstreamPerlLineEdit.Parse(Test1506Line)?.Apply("< HTTP/1.1 200 OK\r\n");

        diagnostics.Act("applied", actual);
        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    [DataRow("== Info: Connection #0 to host 127.0.0.1:8990 left intact\n", "== Info: Connection #0 to host 127.0.0.1:8990 left intact\n")]
    [DataRow("== Info: shutting down connection #0\n", "== Info: shutting down connection #0\n")]
    [DataRow("== Info: Connected to 127.0.0.1 (127.0.0.1) port 8990\n", "")]
    [DataRow("== Info: shutting down connection\n", "")]
    public void LineEditParse_Test1542Line_KeepsOnlyTheConnectionLines(string line, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("line", line);

        string? actual = IUpstreamPerlLineEdit.Parse(Test1542Line)?.Apply(line);

        diagnostics.Act("applied", actual);
        Assert.AreEqual(expected, actual);
    }
}
