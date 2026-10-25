using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1173Script"/>: upstream's <c>tests/test1173.pl</c> over libcurl man pages, what it
/// reports and what it counts.
/// </summary>
[TestClass]
public sealed class UpstreamTest1173ScriptTests
{
    private static readonly string[] CleanFunctionPage =
    [
        ".TH curl_demo 3", ".SH NAME", "curl_demo \\- demo", ".SH SYNOPSIS", ".nf", "int x;", ".SH DESCRIPTION", "Uses \\fIcurl_easy_init(3)\\fP.",
        ".SH EXAMPLE", "line one", "line two", ".SH AVAILABILITY", "Added in 7.1", ".SH \"RETURN VALUE\"", "ok", ".SH \"SEE ALSO\"",
        ".BR curl_easy_init (3),", ".BR libcurl (3),", ".BR other,", ".BR last",
    ];

    private string root = null!;

    [TestInitialize]
    public void CreateFolder() =>
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1173-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    private static (int ExitCode, string StandardError) Scan(params (string Name, string[] Lines)[] pages) =>
        UpstreamTest1173Script.Scan(
            "d",
            pages.ToDictionary(page => page.Name, page => (IReadOnlyList<string>)page.Lines, StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["CURLOPT_OLD"] = "1", ["CURLINFO_TEXT"] = "1" });

    [TestMethod]
    public void Scan_CleanPagesAndReferral_WritesOk()
    {
        string[] referral = [".so man3/curl_demo.3", "trailing   "];

        Assert.AreEqual((0, "ok\n"), Scan(("curl_demo.3", CleanFunctionPage), ("curl_easy_init.3", [".so man3/x.3"]), ("plain.3", [".TH x 3"]), ("opts/CURLOPT_X.3", referral), ("libcurl.3", CleanFunctionPage)));
    }

    [TestMethod]
    public void Scan_LongLineMatchingNoPattern_FinishesInsideTheMatchTimeout()
    {
        string[] page = [".SH DESCRIPTION", new string('a', 200_000)];

        Assert.AreEqual((0, "ok\n"), Scan(("opts/CURLOPT_OLD.3", page)));
    }

    [TestMethod]
    public void Scan_TextMistakes_ReportsEachInTheScriptsWords()
    {
        string[] page =
        [
            "'starts with quote", "\\fBno terminator", "\\fIcurl_easy_init(3)\\fP and \\fIcurl_gone(3)\\fP", "\\fBCURLOPT_URL text\\fP", "x \\fXbad", "\\\\fXok",
            ".SH DESCRIPTION", "call curl_multi_init here", "call \\fIcurl_multi_init here", "trailing  ", ".SH EXAMPLE", "printf(\"a\\n\");", "ok\\\\n",
        ];

        (int exitCode, string error) = Scan(("misc.3", page));

        Assert.AreEqual(10, exitCode, error);
        string[] expected =
        [
            "d/misc.3:1 line starts with single quote!", "d/misc.3:2 missing \\fB terminator!", "d/misc.3:3 broken reference to curl_gone(3)",
            "d/misc.3:4 curl ref to CURLOPT_URL without section", "d/misc.3:5 suspicious \\f format!", "d/misc.3:8 unrefed curl call: curl_multi_init",
            "d/misc.3:10 trailing whitespace", "d/misc.3:12 '\\n' need to be '\\\\n'!",
        ];
        foreach (string line in expected)
        {
            StringAssert.Contains(error, line + "\n");
        }

        StringAssert.Contains(error, "d/misc.3:9 missing \\fI terminator!\n");
    }

    [TestMethod]
    public void Scan_SynopsisAndSeeAlso_ReportsFormatAndSeparators()
    {
        string[] page =
        [
            ".TH t 3", ".SH NAME", "n", ".SH SYNOPSIS", "no nf", ".SH EXAMPLE", "a", "b", ".SH DESCRIPTION", ".SH AVAILABILITY", ".SH \"RETURN VALUE\"", ".SH \"SEE ALSO\"",
            ".BR curl_a (3) x", ".BR curl_b (3);", ".BR plain", ".BR last,", ".NH ignored",
        ];

        (int exitCode, string error) = Scan(("curl_a.3", page), ("curl_b.3", page));

        Assert.AreNotEqual(0, exitCode);
        StringAssert.Contains(error, "d/curl_a.3:5:1:ERROR: be .nf for proper formatting\n");
        StringAssert.Contains(error, "d/curl_a.3:13 bad SEE ALSO format\n");
        StringAssert.Contains(error, "d/curl_a.3:14: bad not-last SEE ALSO separator: ''\n");
        StringAssert.Contains(error, "d/curl_a.3:15: bad not-last SEE ALSO separator: ' '\n");
        StringAssert.Contains(error, "d/curl_a.3:16: superfluous comma separator\n");
        StringAssert.Contains(error, "d/curl_a.3:6 Got EXAMPLE, when DESCRIPTION was expected\n");
    }

    [TestMethod]
    public void Scan_ExampleFollowsSynopsis_ChecksExampleLinesNotSynopsisLines()
    {
        string[] page = [".TH t 3", ".SH NAME", "n", ".SH SYNOPSIS", ".nf", ".SH EXAMPLE", "printf(\"a\\n\");"];

        (_, string error) = Scan(("curl_a.3", page));

        StringAssert.Contains(error, "d/curl_a.3:7 '\\n' need to be '\\\\n'!\n");
    }

    [TestMethod]
    public void Scan_RequiredSections_ReportsMissingExampleAndSections()
    {
        string[] few = [".TH t 3", ".SH NAME", "n", ".SH SYNOPSIS"];
        string[] unfinished = [".TH t 3", ".SH NAME", "n", ".SH SYNOPSIS", ".SH DESCRIPTION", ".SH EXAMPLE", "a", "b", ".SH OTHER"];
        string[] option = [".TH t 3", ".SH NAME", ".SH SYNOPSIS", ".SH DESCRIPTION", ".SH EXAMPLE", "a", "b", ".SH AVAILABILITY", ".SH \"RETURN VALUE\"", ".SH \"SEE ALSO\""];

        (int fewCode, string fewError) = Scan(("curl_few.3", few));
        (int unfinishedCode, string unfinishedError) = Scan(("curl_unfinished.3", unfinished));
        (int optionCode, string optionError) = Scan(("opts/CURLOPT_URL.3", option), ("opts/CURLOPT_OLD.3", [".SH NAME"]));

        Assert.AreEqual(2, fewCode);
        StringAssert.Contains(fewError, "d/curl_few.3:5 missing EXAMPLE section\n");
        StringAssert.Contains(fewError, "d/curl_few.3:5 too few man page sections!\n");
        Assert.AreEqual(1, unfinishedCode);
        StringAssert.Contains(unfinishedError, "d/curl_unfinished.3:10 missing mandatory section: AVAILABILITY\n");
        StringAssert.Contains(unfinishedError, "section found at index 4: 'OTHER'\n");
        StringAssert.Contains(unfinishedError, " Found 5 used sections\n");
        Assert.AreEqual(1, optionCode);
        StringAssert.Contains(optionError, "d/opts/CURLOPT_URL.3:5 Got EXAMPLE, when PROTOCOLS was expected\n");
    }

    [TestMethod]
    public void Scan_OptionPage_RequiresFontTaggingOfOptionsExceptDeprecated()
    {
        string[] page = [".SH NAME", ".SH SYNOPSIS", ".SH DESCRIPTION", "uses \\fICURLOPT_A(3)\\fP and CURLOPT_OLD and CURLOPT_B", ".SH PROTOCOLS", ".SH EXAMPLE", "a", "b", ".SH AVAILABILITY", ".SH \"RETURN VALUE\"", ".SH \"SEE ALSO\"", "CURLOPT_C"];

        (int exitCode, string error) = Scan(("opts/CURLOPT_X.3", page), ("opts/CURLOPT_A.3", [".so man3/x.3"]));

        Assert.AreNotEqual(0, exitCode);
        StringAssert.Contains(error, "d/opts/CURLOPT_X.3:4 option CURLOPT_B missing \\fI tagging\n");
        Assert.IsFalse(error.Contains("CURLOPT_A missing", StringComparison.Ordinal));
        Assert.IsFalse(error.Contains(":12 option", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Scan_ReferencesAndEdgeLines_FollowTheScriptsRules()
    {
        string[] page =
        [
            "before any heading CURLOPT_B", ".SH NAME", "\\fIcurl(1)\\fP \\fIcurl_other(1)\\fP \\fBCURLINFO_TEXT x\\fP", ".SH SYNOPSIS", ".nf", ".SH DESCRIPTION", "CURLOPT_OLD",
            "\\fICURLOPT_X(3)\\fP", ".SH PROTOCOLS", ".SH EXAMPLE", "a", "b", ".SH AVAILABILITY", ".SH \"RETURN VALUE\"", ".SH EXTRA", ".SH \"SEE ALSO\"", ".BR libcurl", ".BR curl_x (3),", ".SH ",
        ];

        (int exitCode, string error) = Scan(("opts/CURLOPT_X.3", page));

        Assert.AreEqual(5, exitCode, error);
        StringAssert.Contains(error, "d/opts/CURLOPT_X.3:3 broken reference to curl_other(1)\n");
        StringAssert.Contains(error, "d/opts/CURLOPT_X.3:17 bad SEE ALSO format\n");
        StringAssert.Contains(error, "d/opts/CURLOPT_X.3:18 broken reference to curl_x(3)\n");
        StringAssert.Contains(error, "d/opts/CURLOPT_X.3:19 trailing whitespace\n");
    }

    [TestMethod]
    public void Check_NoAltSvcSymbol_WritesTheComplaintAndExits0()
    {
        File.WriteAllText(root + "/symbols-in-versions", "CURLOPT_X                       7.1\n");

        Assert.AreEqual((0, "did not get the symbols-in-version!\n"), UpstreamTest1173Script.Check(root + "/symbols-in-versions"));
    }

    [TestMethod]
    public void Check_PagesWrittenFromMarkdown_ScansThem()
    {
        File.WriteAllText(root + "/symbols-in-versions", " 1\nCURLALTSVC_H1                  7.64.1\nCURLOPT_OLD 7.1 7.2\nCURLOPT_X 7.1\n");
        File.WriteAllText(
            root + "/curl_demo.md",
            "---\nTitle: curl_demo\nSection: 3\nSee-also:\n  - curl_demo (3)\nAdded-in: 7.1\n---\n# NAME\n\ncurl_demo - demo\n\n# SYNOPSIS\n\n~~~c\nint x;\n~~~\n\n# DESCRIPTION\n\nSome text.\n\n# EXAMPLE\n\n~~~c\nint main(void)\n{\n}\n~~~\n\n# AVAILABILITY\n\n# RETURN VALUE\n\nok\n");

        Assert.AreEqual((0, "ok\n"), UpstreamTest1173Script.Check(root + "/symbols-in-versions"));
    }

    [TestMethod]
    public async Task RunAsync_SymbolsPath_WritesTheResultToStandardError()
    {
        File.WriteAllText(root + "/symbols-in-versions", "CURLOPT_X 7.1\n");
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root + "/symbols-in-versions", "unexpanded/*.3"], Stream.Null, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1173Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("did not get the symbols-in-version!\n", Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1173Script.RunAsync(null!, _ => Task.FromResult(0)));

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
