using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1139Script"/>: upstream's <c>tests/test1139.pl</c> over a release tree whose libcurl
/// option pages and command-line option lists agree, with <c>curl.1</c> built in either root or stood in for by the
/// option files (ADR-0750), and each kind of difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1139ScriptTests
{
    // The script's options pretended to be in tool_getparam.c alone, which curl.1 and tool_listhelp.c must name.
    private static readonly string[] PretendedInGetParam =
        ["--no-alpn", "--no-npn", "-N, --no-buffer", "--no-sessionid", "--no-keepalive", "--no-progress-meter", "--no-clobber"];

    // The script's options pretended to be in curl.1 and tool_listhelp.c alone, which tool_getparam.c must name.
    private static readonly (string Name, char Short)[] PretendedInDocs =
    [
        ("alpn", ' '), ("npn", ' '), ("eprt", ' '), ("epsv", ' '), ("keepalive", ' '), ("buffer", 'N'), ("sessionid", ' '), ("progress-meter", ' '), ("clobber", ' '),
        ("krb4", ' '), ("ftp-ssl", ' '), ("ftp-ssl-reqd", ' '), ("include", ' '), ("test-duphandle", ' '), ("test-event", ' '), ("wdebug", ' '),
    ];

    private string root = null!;

    private string BuildRoot => Path.Combine(root, "build");

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1139-{Guid.NewGuid():N}")).FullName;
        foreach (string folder in new[] { "include/curl", "docs/libcurl/opts", "docs/cmdline-opts", "src", "build/docs/cmdline-opts" })
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_AgreeingTreeWithCurl1InTheBuildRoot_WritesZero()
    {
        WriteAgreeingLibcurlDocs();
        WriteGetParam([]);
        WriteListHelp(["-a, --append", "--verbose", .. PretendedInGetParam]);
        WriteManPage(Path.Combine(BuildRoot, "docs", "cmdline-opts", "curl.1"), ["-a, --append", "--verbose", .. PretendedInGetParam]);
        WriteManPage(Path.Combine(root, "docs", "cmdline-opts", "curl.1"), ["--ignored-when-the-build-root-has-one"]);

        string standardError = UpstreamTest1139Script.Check(root, BuildRoot);

        Assert.AreEqual("0\n", standardError);
    }

    [TestMethod]
    public void Check_AgreeingTreeWithCurl1InTheRoot_WritesZero()
    {
        WriteAgreeingLibcurlDocs();
        WriteGetParam([]);
        WriteListHelp(["-a, --append", "--verbose", .. PretendedInGetParam]);
        WriteManPage(Path.Combine(root, "docs", "cmdline-opts", "curl.1"), ["-a, --append", "--verbose", .. PretendedInGetParam]);

        string standardError = UpstreamTest1139Script.Check(root, BuildRoot);

        Assert.AreEqual("0\n", standardError);
    }

    [TestMethod]
    public void Check_AgreeingTreeWithoutCurl1_StandsInWithTheOptionFiles()
    {
        WriteAgreeingLibcurlDocs();
        WriteGetParam([]);
        WriteListHelp(["-a, --append", "--verbose", .. PretendedInGetParam]);
        WriteOptionFile("append", "Short: a\n", "Long: append\n");
        WriteOptionFile("verbose", "Long: verbose\n");
        Array.ForEach(PretendedInGetParam, option => WriteOptionFile(option, option.StartsWith("-N", StringComparison.Ordinal) ? "Short: N\n" : string.Empty, $"Long: {option[(option.IndexOf("--", StringComparison.Ordinal) + 2)..]}\n"));
        File.WriteAllText(Path.Combine(root, "docs", "cmdline-opts", "_NAME.md"), "# NAME\nLong: not front matter\n");
        File.WriteAllText(Path.Combine(root, "docs", "cmdline-opts", "MANPAGE.md"), "---\nc: no Long line\n---\n");
        File.WriteAllText(Path.Combine(root, "docs", "cmdline-opts", "_EMPTY.md"), string.Empty);

        string standardError = UpstreamTest1139Script.Check(root, BuildRoot);

        Assert.AreEqual("0\n", standardError);
    }

    [TestMethod]
    public void Check_EveryKindOfDifference_WritesEachAndCountsThem()
    {
        WriteAgreeingLibcurlDocs();
        File.Delete(Path.Combine(root, "docs", "libcurl", "opts", "CURLINFO_SIZE.md"));
        WriteLibcurlPage("curl_easy_setopt.md", "## CURLOPT_ZERO\n## CURLOPT_NEW\n");
        WriteLibcurlPage("curl_easy_getinfo.md", "## CURLINFO_SIZEX\n");
        WriteGetParam(["  {\"aaa\",          ARG_BOOL, ' ', C_AAA},\n"]);
        WriteListHelp(["-a, --append", "--verbose", "--helponly", .. PretendedInGetParam]);
        WriteManPage(Path.Combine(root, "docs", "cmdline-opts", "curl.1"), ["-a, --append", "--verbose", "--manonly", .. PretendedInGetParam]);

        string standardError = UpstreamTest1139Script.Check(root, BuildRoot);

        string libcurl = $"{root}/docs/libcurl";
        string[] lines = standardError.Split('\n');
        CollectionAssert.AreEqual(
            new[]
            {
                "Missing CURLINFO_SIZE.md",
                $"{libcurl}/curl_easy_setopt.md:1:ERROR: CURLOPT_ZERO is not alphabetical (expected CURLOPT_NEW)",
                $"Missing mention of CURLINFO_SIZE in {libcurl}/curl_easy_getinfo.md",
                $"tool_getparam.c:25: '\"aaa\"' is NOT placed in alpha-order",
                "--aaa is not in curl.1 tool_listhelp.c (but in tool_getparam.c)",
                "--helponly is not in tool_getparam.c curl.1 (but in tool_listhelp.c)",
                "--manonly is not in tool_getparam.c tool_listhelp.c (but in curl.1)",
                "6",
                string.Empty,
            },
            lines);
    }

    [TestMethod]
    public async Task RunAsync_TheCommandsRoots_WritesTheCountToStandardErrorAndExitsZero()
    {
        WriteAgreeingLibcurlDocs();
        File.Delete(Path.Combine(root, "docs", "libcurl", "opts", "CURLMOPT_PIPE.md"));
        WriteGetParam([]);
        WriteListHelp(["-a, --append", "--verbose", .. PretendedInGetParam]);
        WriteManPage(Path.Combine(root, "docs", "cmdline-opts", "curl.1"), ["-a, --append", "--verbose", .. PretendedInGetParam]);
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root, BuildRoot], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1139Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("Missing CURLMOPT_PIPE.md\n1\n", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1139Script.RunAsync(null!, _ => Task.FromResult(0)));

    // curl.h's aliases, symbols-in-versions and the option and index pages, all agreeing.
    private void WriteAgreeingLibcurlDocs()
    {
        File.WriteAllText(
            Path.Combine(root, "include", "curl", "curl.h"),
            "#define CURLOPT_OLD CURLOPT_NEW\n#define CURLOPT_ZERO 0\n#define CURLOPT_FUNC(x) x\n#define CURLOPT_BARE\n#define OTHER 1\nint x;\n");
        WriteLibcurlPage(
            "symbols-in-versions",
            " Name                Introduced  Deprecated  Last\n\nCURLOPT_NEW 7.1\nCURLOPT_OLD 7.1\nCURLOPT_ZERO 7.1 - 0\nCURLOPT_GONE 7.1 7.2 7.3\nCURLINFO_TEXT 7.1\n"
            + "see CURLINFO_SIZE 7.1\nCURLMOPT_PIPE   7.1\nCURLOPT_ 7.1\nCURLOPT_\nCURLSSLOPT_X 7.1\n");
        foreach (string option in new[] { "CURLOPT_NEW", "CURLOPT_ZERO", "CURLINFO_SIZE", "CURLMOPT_PIPE" })
        {
            WriteLibcurlPage(Path.Combine("opts", option + ".md"), "# option\n");
        }

        WriteLibcurlPage("curl_easy_setopt.md", "# curl_easy_setopt\n## CURLOPT\\-NEW alias\n## CURLOPT_NEW\n## CURLOPT_ZERO\ntext\n");
        WriteLibcurlPage("curl_easy_getinfo.md", "## CURLINFO_SIZE\n");
        WriteLibcurlPage("curl_multi_setopt.md", "## CURLMOPT_PIPE and more\n");
    }

    private void WriteLibcurlPage(string name, string text) => File.WriteAllText(Path.Combine(root, "docs", "libcurl", name), text);

    // The aliases table: the documented options, in order, then any extra lines, around lines it ignores.
    private void WriteGetParam(string[] extraLines)
    {
        IEnumerable<string> entries = PretendedInDocs.Append((Name: "append", Short: 'a')).Append((Name: "verbose", Short: ' '))
            .OrderBy(option => $"\"{option.Name}\"", StringComparer.Ordinal)
            .Select(option => $"  {{\"{option.Name}\", ARG_BOOL, '{option.Short}', C_X}},\n");
        File.WriteAllText(
            Path.Combine(root, "src", "tool_getparam.c"),
            "  {\"before-the-table\", ARG_BOOL, 'z', C_X},\nstatic const struct LongShort aliases[] = {\n  {\",\n  {\"nocomma\"\n  {\"x\", a',\nnot an entry\n"
            + string.Concat(entries) + string.Concat(extraLines) + "};\n");
    }

    private void WriteListHelp(string[] options) =>
        File.WriteAllText(
            Path.Combine(root, "src", "tool_listhelp.c"),
            "const struct helptxt helptext[] = {\n" + string.Concat(options.Select(option => $"  {{ \"    {option} <arg>\",\n    \"help\",\n")) + "  {\"not an option\",\n};\n");

    // curl.1's .IP lines, with roff's escaped minuses, around lines that name no option.
    private static void WriteManPage(string path, string[] options) =>
        File.WriteAllText(
            path,
            ".TH curl 1\n.IP \"-unterminated\n.IP \"-x\"\n.IP \"-abcdef\"\n"
            + string.Concat(options.Select(option => $".IP \"{option.Replace("-", "\\-", StringComparison.Ordinal)} <arg>\"\ntext\n")));

    private void WriteOptionFile(string name, params string[] frontMatter) =>
        File.WriteAllText(Path.Combine(root, "docs", "cmdline-opts", name.TrimStart('-').Replace(", ", "_", StringComparison.Ordinal) + ".md"), "---\n" + string.Concat(frontMatter) + "---\n\nLong: after the front matter\n");

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
