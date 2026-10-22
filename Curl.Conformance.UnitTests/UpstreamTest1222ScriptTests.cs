using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1222Script"/>: upstream's <c>tests/test1222.pl</c> skipping when the libcurl manual
/// was not built, its <c>die</c>s without <c>symbols-in-versions</c> or the include folder, and its deprecation check
/// over built pages and over the vendored release's converted manual (BL-3000).
/// </summary>
[TestClass]
public sealed class UpstreamTest1222ScriptTests
{
    private string root = null!;

    private string BuildRoot => Path.Combine(root, "build");

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1222-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "docs", "libcurl"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_NoBuildDocsFolder_SkipsAsWithoutTheManual()
    {
        WriteSymbols();

        (int exitCode, string standardOutput, string standardError) = UpstreamTest1222Script.Check(root, BuildRoot);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("curl built without the libcurl manual. Skipping test 1222.\n", standardOutput);
        Assert.AreEqual(string.Empty, standardError);
    }

    [TestMethod]
    public void Check_BuildDocsWithoutManPages_SkipsAsWithoutTheManual()
    {
        WriteSymbols();
        Directory.CreateDirectory(Path.Combine(BuildRoot, "docs", "libcurl"));
        File.WriteAllText(Path.Combine(BuildRoot, "docs", "libcurl", "curl_easy_setopt.md"), "Markdown, not a built page\n");

        (int exitCode, string standardOutput, _) = UpstreamTest1222Script.Check(root, BuildRoot);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("curl built without the libcurl manual. Skipping test 1222.\n", standardOutput);
    }

    [TestMethod]
    public void Check_VendoredRelease_FindsTheSourcesAgreeOverItsConvertedManual()
    {
        string release = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");

        (int exitCode, string standardOutput, string standardError) = UpstreamTest1222Script.Check(release, BuildRoot);

        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void Check_OnlyOptionPagesInMarkdown_SkipsAsWithoutTheManual()
    {
        WriteSymbols();
        Directory.CreateDirectory(Path.Combine(root, "docs", "libcurl", "opts"));
        File.WriteAllText(Path.Combine(root, "docs", "libcurl", "opts", "CURLOPT_URL.md"), "---\nTitle: CURLOPT_URL\nSection: 3\n---\n");

        (int exitCode, string standardOutput, _) = UpstreamTest1222Script.Check(root, BuildRoot);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("curl built without the libcurl manual. Skipping test 1222.\n", standardOutput);
    }

    [TestMethod]
    public void Check_ManualWithoutIncludeFolder_DiesNamingIt()
    {
        WriteSymbols();
        WritePage("curl_easy_setopt.3", ".TH curl_easy_setopt 3");

        (int exitCode, _, string standardError) = UpstreamTest1222Script.Check(root, BuildRoot);

        Assert.AreEqual(255, exitCode);
        Assert.AreEqual($"Cannot opendir {root}/include/curl\n", standardError);
    }

    [TestMethod]
    public void Check_SourcesDisagree_WritesTheConflictsAndTheTableAndExitsWithTheirCount()
    {
        File.WriteAllText(Path.Combine(root, "docs", "libcurl", "symbols-in-versions"), string.Join('\n',
            "CURLOPT_AAA 7.1", "CURLOPT_BBB 7.1 7.5", "CURLOPT_GONE 7.1 7.2 7.3", "CURLINFO_CCC 7.1 .", "curl_old 7.1 7.9", "curl_new 7.1", "CURLE_X 7.1", "not a symbol line"));
        Directory.CreateDirectory(Path.Combine(root, "include", "curl"));
        File.WriteAllText(Path.Combine(root, "include", "curl", "a.h"), string.Join('\n',
            "#ifndef X", "/* a comment", "   still comment", "   end */ CURL_EXTERN void curl_new(void);",
            "CURL_EXTERN void curl_old(void) CURL_DEPRECATED(7.8,", "                                \"use curl_new\");",
            "CURL_EXTERN void /* inline */ curl_plain(void);", "int unused; /* trailing open comment", "*/", "int folded",
            "  ;", "typedef enum {", "  CURLOPT(CURLOPT_AAA, CURLOPTTYPE_LONG, 1),", "  CURLOPT(CURLOPT_BBB, CURLOPTTYPE_LONG,", "          2),",
            "  CURLOPTDEPRECATED(CURLOPT_DDD, CURLOPTTYPE_LONG, 3,", "                    7.5, \"gone\"),",
            "  CURLOPT(CURLOPT_EEE, CURLOPTTYPE_LONG, 4),", "  CURLOPTDEPRECATED(CURLOPT_FFF, CURLOPTTYPE_LONG, 5, 7.6, \"x\"),",
            "  CURLOPTDEPRECATED(CURLOPT_GGG, CURLOPTTYPE_LONG, 6, 7.7, \"x\"),", "  ;", "  CURLE_X,",
            "  CURLE_DEP CURL_DEPRECATED(7.4, \"y\") = 5,", "  CURLE_LAST}", "struct after;"));
        WritePage("curl_new.3", ".TH curl_new 3", ".SH DESCRIPTION", "New \\fBfunction\\fP.", ".SH SEE ALSO");
        WritePage("curl_old.3", ".SH NAME", "curl_old - deprecated in 7.1 but not described.", ".SH DESCRIPTION", "This function is deprecated since 7.8.",
            ".nf", "Also obsoleted in version 7.9 and deprecated. Deprecated in 7.8 too.", ".SH DEPRECATED", "Deprecated since 7.8.", ".so man3/curl_old_more.3");
        WritePage("curl_easy_setopt.3", ".SH OPTIONS", ".IP CURLOPT_AAA", "Plain option.", ".IP CURLOPT_BBB", "\\fBDeprecated option\\fP since 7.5.",
            ".IP CURLOPT_EEE", "Deprecated.", ".IP CURLOPT_FFF", "Obsolete.", ".IP CURLOPT_GGG", "Current.", ".IP CURLOPT_HHH", "Deprecated since 7.3.", ".IP OTHER", "text", ".SH SEE ALSO");
        WritePage("opts/CURLOPT_AAA.3", ".SH DESCRIPTION", "Nothing.", ".SH SEE ALSO");
        WritePage("opts/CURLOPT_BBB.3", ".so man3/CURLOPT_BBB_real.3");
        WritePage("opts/CURLOPT_BBB_real.3", ".SH DESCRIPTION", "Deprecated since 7.5.", ".SH AVAILABILITY");

        (int exitCode, string standardOutput, string standardError) = UpstreamTest1222Script.Check(root, BuildRoot);

        string expected = "error: curl_old man page lists unmatching deprecation versions 7.8 and 7.9\n"
            + "Legend:\n<empty> Not listed\nX       Not deprecated\n?       Deprecated in unknown version\nx.yy.z  Deprecated in version x.yy.z\n\n"
            + "Symbol                                 symbols-in  func man  opt man   .h\n                                       -versions\n"
            + "CURLOPT_BBB                            7.5         ?         7.5       X\n"
            + "CURLOPT_EEE                                        ?                   X\n"
            + "CURLOPT_GGG                                        X                   7.7\n";
        Assert.AreEqual(expected, standardOutput);
        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(4, exitCode);
    }

    [TestMethod]
    public void Check_NoSymbolsInVersions_DiesWithExitCode255()
    {
        (int exitCode, _, string standardError) = UpstreamTest1222Script.Check(root, BuildRoot);

        Assert.AreEqual(255, exitCode);
        Assert.AreEqual($"{root}/docs/libcurl/symbols-in-versions\n", standardError);
    }

    [TestMethod]
    public async Task RunAsync_TheCommandsRoots_WritesTheSkipToStandardOutput()
    {
        WriteSymbols();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root, BuildRoot], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1222Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("curl built without the libcurl manual. Skipping test 1222.\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1222Script.RunAsync(null!, _ => Task.FromResult(0)));

    private void WriteSymbols() =>
        File.WriteAllText(Path.Combine(root, "docs", "libcurl", "symbols-in-versions"), "CURLOPT_URL 7.1\n");

    private void WritePage(string page, params string[] lines)
    {
        string path = Path.Combine(BuildRoot, "docs", "libcurl", page);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
