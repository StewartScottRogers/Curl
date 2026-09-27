using System.Globalization;

namespace Curl.Conformance;

/// <summary>
/// Pins every reason <see cref="UpstreamCaseScreening"/> gives for skipping a case, and that a
/// case the harness can run gets none.
/// </summary>
[TestClass]
public sealed class UpstreamCaseScreeningTests
{
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

        Assert.IsNull(Screen(sections));
    }

    [TestMethod]
    public void FindSkipReason_ConditionError_IsTheReason()
    {
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, [], [], "stray %endif");

        Assert.AreEqual("stray %endif", UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features));
    }

    [TestMethod]
    public void FindSkipReason_UnknownVariables_AreTheReason()
    {
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, ["%FTPPORT", "%USER"], [], null);

        Assert.AreEqual("the harness has no value for %FTPPORT, %USER", UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features));
    }

    [TestMethod]
    public void FindSkipReason_UnsupportedInstructions_AreTheReason()
    {
        UpstreamTestFileExpansion expansion = new(ReadOnlyMemory<byte>.Empty, [], ["%include"], null);

        Assert.AreEqual("the harness does not carry out %include", UpstreamCaseScreening.FindSkipReason(expansion, ParsedTestCase.From(RunnableClient), Features));
    }

    [TestMethod]
    [DataRow("<client>\n<tool>\nlib1\n</tool>\n</client>\n", "the harness does not act on <client><tool>")]
    [DataRow("<verify>\n<upload>\nx\n</upload>\n</verify>\n", "the harness does not act on <verify><upload>")]
    [DataRow("<client>\n<features>\nhttp\nDebug\n</features>\n</client>\n", "Curl lacks the feature Debug")]
    [DataRow("<client>\n<features>\n!SSL\n</features>\n</client>\n", "the case needs Curl without the feature SSL")]
    [DataRow("<reply>\n<servercmd>\nidle\n</servercmd>\n</reply>\n", "the sws emulation does not carry out the server command idle")]
    [DataRow("<client>\n<file name=\"relative.txt\">\n</file>\n</client>\n", "<client><file> does not name a file by an absolute path")]
    [DataRow("<verify>\n<file3>\n</file3>\n</verify>\n", "<verify><file3> does not name a file by an absolute path")]
    [DataRow("<verify>\n<strip>\n(\n</strip>\n</verify>\n", "the strip pattern ( is not a .NET regular expression")]
    [DataRow("<verify>\n<stripfile2>\n$_ = ''\n</stripfile2>\n</verify>\n", "the harness does not run the Perl $_ = ''")]
    [DataRow("<verify>\n<errorcode>\nlots\n</errorcode>\n</verify>\n", "the expected exit code lots is not a number")]
    public void FindSkipReason_NamesWhatTheHarnessCannotDo(string sections, string expected)
    {
        Assert.AreEqual(expected, Screen(RunnableClient + sections));
    }

    [TestMethod]
    [DataRow("<client>\n<server>\nhttp\nftp\n</server>\n<command>\na\n</command>\n</client>\n", "the harness does not emulate the ftp server")]
    [DataRow("<client>\n<name>\nno command\n</name>\n</client>\n", "the case has no <client><command>")]
    [DataRow("<client>\n<command type=\"perl\">\nx\n</command>\n</client>\n", "the harness does not run a perl command")]
    [DataRow("<client>\n<command>\nhttp://h/ | cat\n</command>\n</client>\n", "the command needs a shell for its |")]
    public void FindSkipReason_ClientTheHarnessCannotRun_IsTheReason(string sections, string expected)
    {
        Assert.AreEqual(expected, Screen(sections));
    }

    [TestMethod]
    public void FindFileOutsideLogDirectory_EveryFileInside_ReturnsNull()
    {
        string logDirectory = Rooted("log");
        string sections = $"<client>\n<file name=\"{logDirectory}/in\">\n</file>\n</client>\n<verify>\n<file1 name=\"{logDirectory}/sub/out\">\n</file1>\n</verify>\n";

        Assert.IsNull(UpstreamCaseScreening.FindFileOutsideLogDirectory(ParsedTestCase.From(sections), logDirectory));
    }

    [TestMethod]
    [DataRow("<client>\n<file name=\"{0}/in\">\n</file>\n</client>\n", "<client><file> names {0}/in, outside the case's log directory")]
    [DataRow("<verify>\n<file2 name=\"{0}/log/../out\">\n</file2>\n</verify>\n", "<verify><file2> names {0}/log/../out, outside the case's log directory")]
    [DataRow("<verify>\n<file name=\"{0}/logs/out\">\n</file>\n</verify>\n", "<verify><file> names {0}/logs/out, outside the case's log directory")]
    public void FindFileOutsideLogDirectory_FileOutside_IsTheReason(string sections, string expected)
    {
        string parent = Rooted("parent");

        string? reason = UpstreamCaseScreening.FindFileOutsideLogDirectory(ParsedTestCase.From(string.Format(CultureInfo.InvariantCulture, sections, parent)), parent + "/log");

        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, expected, parent), reason);
    }

    private static string? Screen(string sections) =>
        UpstreamCaseScreening.FindSkipReason(CleanExpansion, ParsedTestCase.From(sections), Features);

    private static string Rooted(string name) =>
        Path.Combine(Path.GetTempPath(), name).Replace('\\', '/');
}
