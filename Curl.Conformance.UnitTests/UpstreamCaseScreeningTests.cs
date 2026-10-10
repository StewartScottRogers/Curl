using System.Globalization;
using Curl.Testing;

namespace Curl.Conformance;

/// <summary>
/// Pins every reason <see cref="UpstreamCaseScreening"/> gives for skipping a case, and that a
/// case the harness can run gets none.
/// </summary>
[TestClass]
public sealed class UpstreamCaseScreeningTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string RunnableClient = "<client>\n<server>\nhttp\nfile\nnone\n</server>\n<command>\nhttp://h/1\n</command>\n</client>\n";

    private static readonly HashSet<string> Features = ["http", "SSL"];

    private static readonly UpstreamTestFileExpansion CleanExpansion = new(ReadOnlyMemory<byte>.Empty, [], [], null);

    [TestMethod]
    public void FindSkipReason_RunnableCase_ReturnsNull()
    {
        string sections = RunnableClient
            + "<reply>\n<servercmd>\nswsclose\n</servercmd>\n</reply>\n"
            + "<client>\n<features>\nhttp\n!Debug\n</features>\n</client>\n"
            + $"<verify>\n<file name=\"{Rooted("out")}\">\n</file>\n<strip>\n^Date:\n</strip>\n<strippart>\n# comment\ns/a/b/\n</strippart>\n<errorcode>\n 7 \n</errorcode>\n</verify>\n";

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", Neutral(sections));
        string? reason = Screen(sections);
        diagnostics.Act("skip reason", reason ?? "(none)");
        diagnostics.Assert("skip reason", "(none)", reason ?? "(none)");
        Assert.IsNull(reason);
    }

    [TestMethod]
    public void FindSkipReason_ConditionError_IsTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, [], [], "stray %endif");
        diagnostics.Arrange("expansion", """new(ReadOnlyMemory<byte>.Empty, [], [], "stray %endif")""");
        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "stray %endif", reason);
        Assert.AreEqual("stray %endif", reason);
    }

    [TestMethod]
    public void FindSkipReason_UnknownVariables_AreTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, ["%FTPPORT", "%USER"], [], null);
        diagnostics.Arrange("expansion", """new(ReadOnlyMemory<byte>.Empty, ["%FTPPORT", "%USER"], [], null)""");
        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "the harness has no value for %FTPPORT, %USER", reason);
        Assert.AreEqual("the harness has no value for %FTPPORT, %USER", reason);
    }

    [TestMethod]
    public void FindSkipReason_CertdirGivenAValue_IsNotTheReasonButTheTlsServerIs()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] file = "<testcase>\n<client>\n<server>\nhttps test-localhost.pem\n</server>\n<command>\n--cacert %CERTDIR/certs/test-ca.crt https://localhost/\n</command>\n</client>\n</testcase>\n"u8.ToArray();
        Dictionary<string, string> variables = new(StringComparer.Ordinal) { ["CERTDIR"] = "/curl/tests" };
        diagnostics.Arrange("variables", "CERTDIR=/curl/tests");
        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(file, variables, Features);

        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, expansion.Parse().TestCase!, Features);
        diagnostics.Act("skip reason", reason);

        diagnostics.Assert("skip reason does not name %CERTDIR", false, reason?.Contains("%CERTDIR", StringComparison.Ordinal) ?? false);
        Assert.IsNotNull(reason);
        Assert.DoesNotContain("%CERTDIR", reason);
        Assert.Contains("https", reason);
    }

    [TestMethod]
    public void FindSkipReason_UnsupportedInstructions_AreTheReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, [], ["%include"], null);
        diagnostics.Arrange("expansion", """new(ReadOnlyMemory<byte>.Empty, [], ["%include"], null)""");
        string? reason = UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "the harness does not carry out %include", reason);
        Assert.AreEqual("the harness does not carry out %include", reason);
    }

    [TestMethod]
    [DataRow("<client>\n<tool>\nlib1\n</tool>\n</client>\n", "the harness does not act on <client><tool>")]
    [DataRow("<verify>\n<upload>\nx\n</upload>\n</verify>\n", "the harness does not act on <verify><upload>")]
    [DataRow("<client>\n<features>\nhttp\nDebug\n</features>\n</client>\n", "Curl lacks the feature Debug")]
    [DataRow("<client>\n<features>\n!SSL\n</features>\n</client>\n", "the case needs Curl without the feature SSL")]
    [DataRow("<reply>\n<servercmd>\ndelay: 5\n</servercmd>\n</reply>\n", "the sws emulation does not carry out the server command delay")]
    [DataRow("<client>\n<file name=\"relative.txt\">\n</file>\n</client>\n", "<client><file> does not name a file by an absolute path")]
    [DataRow("<verify>\n<file3>\n</file3>\n</verify>\n", "<verify><file3> does not name a file by an absolute path")]
    [DataRow("<verify>\n<strip>\n(\n</strip>\n</verify>\n", "the strip pattern ( is not a .NET regular expression")]
    [DataRow("<verify>\n<stripfile2>\n$_ = ''\n</stripfile2>\n</verify>\n", "the harness does not run the Perl $_ = ''")]
    [DataRow("<verify>\n<errorcode>\nlots\n</errorcode>\n</verify>\n", "the expected exit code lots is not a number")]
    public void FindSkipReason_NamesWhatTheHarnessCannotDo(string sections, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);
        string? reason = Screen(RunnableClient + sections);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", expected, reason);
        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    [DataRow("<client>\n<server>\nhttp\nftp\n</server>\n<command>\na\n</command>\n</client>\n", "the harness does not emulate the ftp server")]
    [DataRow("<client>\n<name>\nno command\n</name>\n</client>\n", "the case has no <client><command>")]
    [DataRow("<client>\n<command type=\"perl\">\nx\n</command>\n</client>\n", "the harness does not run a perl command")]
    [DataRow("<client>\n<command>\nhttp://h/ | cat\n</command>\n</client>\n", "the command needs a shell for its |")]
    [DataRow("<client>\n<command>\n--ssl-no-revoke -I https://revoked.badssl.com/\n</command>\n</client>\n", "the case reaches revoked.badssl.com on the internet, which the harness does not")]
    [DataRow("<client>\n<command>\n-I http://www.example.com:8080/\n</command>\n</client>\n", "the case reaches www.example.com on the internet, which the harness does not")]
    public void FindSkipReason_ClientTheHarnessCannotRun_IsTheReason(string sections, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);
        string? reason = Screen(sections);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", expected, reason);
        Assert.AreEqual(expected, reason);
    }

    [TestMethod]
    [DataRow("<client>\n<command>\nhttp://127.0.0.1:8990/x file:///dir/x http://localhost/\n</command>\n</client>\n")]
    [DataRow("<client>\n<server>\nhttp\n</server>\n<command>\n-x http://127.0.0.1:8990 http://www.example.com/\n</command>\n</client>\n")]
    [DataRow("<client>\n<command>\n-v%08 http://example.com\n</command>\n</client>\n<verify>\n<errorcode>\n2\n</errorcode>\n</verify>\n")]
    public void FindSkipReason_NoInternetHostOrAServerNamed_ReturnsNull(string sections)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", sections);
        string? reason = Screen(sections);
        diagnostics.Act("skip reason", reason);
        diagnostics.Assert("skip reason", "null", reason ?? "null");
        Assert.IsNull(reason);
    }

    [TestMethod]
    public void FindFileOutsideLogDirectory_EveryFileInside_ReturnsNull()
    {
        string logDirectory = Rooted("log");
        string sections = $"<client>\n<file name=\"{logDirectory}/in\">\n</file>\n</client>\n<verify>\n<file1 name=\"{logDirectory}/sub/out\">\n</file1>\n</verify>\n";

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test sections", Neutral(sections));
        diagnostics.Arrange("log directory", Neutral(logDirectory));
        string? reason = UpstreamCaseScreening.FindFileOutsideLogDirectory(ParsedTestCase.From(sections), logDirectory);
        diagnostics.Act("outside-file reason", reason ?? "(none)");
        diagnostics.Assert("outside-file reason", "(none)", reason ?? "(none)");
        Assert.IsNull(reason);
    }

    [TestMethod]
    [DataRow("<client>\n<file name=\"{0}/in\">\n</file>\n</client>\n", "<client><file> names {0}/in, outside the case's log directory")]
    [DataRow("<verify>\n<file2 name=\"{0}/log/../out\">\n</file2>\n</verify>\n", "<verify><file2> names {0}/log/../out, outside the case's log directory")]
    [DataRow("<verify>\n<file name=\"{0}/logs/out\">\n</file>\n</verify>\n", "<verify><file> names {0}/logs/out, outside the case's log directory")]
    public void FindFileOutsideLogDirectory_FileOutside_IsTheReason(string sections, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string parent = Rooted("parent");

        diagnostics.Arrange("sections template", sections);
        string? reason = UpstreamCaseScreening.FindFileOutsideLogDirectory(ParsedTestCase.From(string.Format(CultureInfo.InvariantCulture, sections, parent)), parent + "/log");

        string expectedReason = string.Format(CultureInfo.InvariantCulture, expected, parent);
        diagnostics.Act("outside-file reason", reason);
        diagnostics.Assert("outside-file reason", Neutral(expectedReason), Neutral(reason));
        Assert.AreEqual(expectedReason, reason);
    }

    [TestMethod]
    [DataRow("<client>\n<precheck>\n%PERL %SRCDIR/libtest/test610.pl mkdir /log/test610.dir\n</precheck>\n</client>\n")]
    [DataRow("<verify>\n<postcheck>\n%PERL %SRCDIR/libtest/test610.pl move /log/a /log/b rmdir /log/c rm /log/d\n</postcheck>\n</verify>\n")]
    public void FindSkipReason_Test610ScriptLines_AreNotPerlTheHarnessDoesNotRun(string sections)
    {
        string? reason = Screen(RunnableClient + sections);

        Assert.DoesNotContain("the harness does not run the Perl", reason ?? "");
    }

    private static string? Screen(string sections) =>
        UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features);

    private static string Neutral(string? text) =>
        (text ?? "(none)").Replace(Path.GetTempPath().Replace('\\', '/'), "<temp>/");

    private static string Rooted(string name) =>
        Path.Combine(Path.GetTempPath(), name).Replace('\\', '/');
}
