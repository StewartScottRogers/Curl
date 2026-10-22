using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Cli.UnitTests.ManualPages;

[TestClass]
public sealed class ManualPageGeneratorBranchTests
{
    private const string Usage = "Usage: managen [-d dir] <mainpage/ascii/listhelp/single FILE/protos/listcats> [files]\n";

    [TestMethod]
    public void Run_NullArguments_Throws()
    {
        ManualPageGenerator generator = new(_ => null, "2026-10-21");

        Assert.ThrowsExactly<ArgumentNullException>(() => generator.Run(null!, "8.22.0", new StringBuilder(), new StringBuilder()));
    }

    [TestMethod]
    [DataRow("listhelp")]
    [DataRow("single")]
    [DataRow("protos")]
    [DataRow("listcats")]
    [DataRow("")]
    public void Run_CommandOtherThanMainpageOrAscii_WritesTheUsageLine(string command)
    {
        (int exitCode, string output, string error) = Run(new Dictionary<string, string>(StringComparer.Ordinal), [command], "8.22.0");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Usage, output);
        Assert.AreEqual("", error);
    }

    [TestMethod]
    public void Run_NoArguments_WritesTheUsageLine()
    {
        (int exitCode, string output, _) = Run(new Dictionary<string, string>(StringComparer.Ordinal), [], "8.22.0");

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(Usage, output);
    }

    [TestMethod]
    public void Run_MakeTgzVersionGiven_NamesItWithoutReadingTheHeader()
    {
        Dictionary<string, string> files = Page("Text.\n");

        (int exitCode, string output, _) = Run(files, ["-d", "/d", "mainpage"], "8.99.0");

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(output, ".TH curl 1 \"2026-10-21\" \"curl 8.99.0\" \"curl Manual\"\n");
    }

    [TestMethod]
    public void Run_MakeTgzVersionFalse_ReadsTheVersionFromCurlverH()
    {
        Dictionary<string, string> files = Page("Text.\n");
        files["/inc/curl/curlver.h"] = "#define LIBCURL_VERSION_MAJOR 8\n#define LIBCURL_VERSION \"8.22.0-DEV\"\n";

        (_, string output, _) = Run(files, ["-d", "/d", "-I", "/inc", "mainpage"], "0");

        StringAssert.Contains(output, "\"curl 8.22.0\"");
    }

    [TestMethod]
    public void Run_CurlverHWithoutTheVersion_NamesVersionUnknown()
    {
        Dictionary<string, string> files = Page("Text.\n");
        files["/inc/curl/curlver.h"] = "/* no version here */\n";

        (_, string output, _) = Run(files, ["-d", "/d", "-I", "/inc", "mainpage"], null);

        StringAssert.Contains(output, "\"curl unknown\"");
    }

    [TestMethod]
    public void Run_ColumnsOption_CountsQuotedLinesWiderThanIt()
    {
        Dictionary<string, string> files = Page("```\n0123456789abcdef\n```\n");

        (int narrow, _, string error) = Run(files, ["-d", "/d", "-c", "10", "ascii"], "8.22.0");
        (int wide, _, _) = Run(files, ["-d", "/d", "ascii"], "8.22.0");

        Assert.AreEqual(1, narrow);
        StringAssert.Contains(error, "columns is too long\n0123456789abcdef\n");
        Assert.AreEqual(0, wide);
    }

    [TestMethod]
    public void Run_GlobalsPlaceholder_ListsTheGlobalOptionsInFileNameOrder()
    {
        Dictionary<string, string> files = Page("Globals: `%GLOBALS`\n");
        files["/d/d.md"] = "---\nLong: delta\nScope: global\n---\n";
        files["/d/a.md"] = "---\nLong: alpha\nScope: global\n---\n";
        files["/d/b.md"] = "---\nLong: beta\n---\n";
        files["/d/c.md"] = "---\nLong: gamma\nScope: global\nLong: ignored\n---\n";
        files["/d/e.md"] = "No header block.\n";

        (_, string output, _) = Run(files, ["-d", "/d", "ascii", "d.md", "e.md", "b.md", "c.md", "a.md"], "8.22.0");

        StringAssert.Contains(output, "Globals: --alpha, --gamma and --delta");
    }

    [TestMethod]
    public void Run_GlobalOptionWithoutALongName_ListsAnEmptyNameJoinedToTheOneBeforeWithAnd()
    {
        Dictionary<string, string> files = Page("Globals: `%GLOBALS`\n");
        files["/d/a.md"] = "---\nLong: alpha\nScope: global\n---\n";
        files["/d/b.md"] = "---\nLong: beta\nScope: global\n---\n";
        files["/d/c.md"] = "---\nScope: global\n---\n";
        files["/d/d.md"] = "---\nLong: delta\nScope: global\n---\n";

        (_, string output, _) = Run(files, ["-d", "/d", "ascii", "a.md", "b.md", "c.md", "d.md"], "8.22.0");

        // As the script, a name before an empty one is joined with " and ", not ", ".
        StringAssert.Contains(output, "Globals: --alpha and --beta, -- and --delta");
    }

    [TestMethod]
    public void Run_IndexCommentLine_IsSkipped()
    {
        Dictionary<string, string> files = Page("Text.\n");
        files["/d/mainpage.idx"] = "# a comment naming no file\n_page.md\n";

        (int exitCode, string output, string error) = Run(files, ["-d", "/d", "ascii"], "8.22.0");

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(output, "Text.");
        Assert.AreEqual("", error);
    }

    [TestMethod]
    public void Run_MissingIndex_DiesWithExitCode2()
    {
        (int exitCode, _, string error) = Run(new Dictionary<string, string>(StringComparer.Ordinal), ["-d", "/d", "ascii"], "8.22.0");

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("no /d/mainpage.idx file\n", error);
    }

    [TestMethod]
    public void Run_MissingTopLevelPage_DiesWithExitCode2()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal) { ["/d/mainpage.idx"] = "_missing.md\n" };

        (int exitCode, _, string error) = Run(files, ["-d", "/d", "ascii"], "8.22.0");

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("could not find /d/_missing.md\n", error);
    }

    [TestMethod]
    public void Run_MissingOptionDocument_DiesWithExitCode2()
    {
        (int exitCode, _, string error) = Run(Page("Text.\n"), ["-d", "/d", "ascii", "gone.md"], "8.22.0");

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual("could not find /d/gone.md\n", error);
    }

    [TestMethod]
    public void Run_OptionDocumentRefused_ExitsWithTheRefusalsCode()
    {
        Dictionary<string, string> files = Page("Text.\n");
        files["/d/mainpage.idx"] = "%options\n";
        files["/d/bad.md"] = "---\n---\n";

        (int exitCode, _, string error) = Run(files, ["-d", "/d", "ascii", "bad.md"], "8.22.0");

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("ERROR: no 'Long:' in bad.md\n", error);
    }

    private static Dictionary<string, string> Page(string text) => new(StringComparer.Ordinal)
    {
        ["/d/mainpage.idx"] = "_page.md\n",
        ["/d/_page.md"] = text,
    };

    private static (int ExitCode, string Output, string Error) Run(Dictionary<string, string> files, string[] arguments, string? version)
    {
        StringBuilder output = new();
        StringBuilder error = new();
        int exitCode = new ManualPageGenerator(path => files.GetValueOrDefault(path), "2026-10-21").Run(arguments, version, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
