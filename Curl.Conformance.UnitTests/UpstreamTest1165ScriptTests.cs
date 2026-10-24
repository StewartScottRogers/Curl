using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1165Script"/>: upstream's <c>tests/test1165.pl</c> over a source tree's build files,
/// C sources and <c>CURL-DISABLE.md</c>, which options count and each difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1165ScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateTree()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1165-{Guid.NewGuid():N}")).FullName;
        foreach (string folder in new[] { "m4", "docs", "include/curl", "src", "lib/vtls", "lib/vauth" })
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }
    }

    [TestCleanup]
    public void DeleteTree() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_EveryOptionAgreed_WritesNothing()
    {
        Write("configure.ac", "AC CURL_DISABLE_ALPHA\n");
        Write("m4/x.m4", "m4 CURL_DISABLE_BETA\n");
        Write("CMakeLists.txt", "CURL_DISABLE_ALPHA\nCURL_DISABLE_BETA\nCURL_DISABLE_INSTALL\n");
        Write("lib/curl_config-cmake.h.in", "CURL_DISABLE_ALPHA\nCURL_DISABLE_BETA\nCURL_DISABLE_TYPECHECK\n");
        Write("lib/a.c", "x CURL_DISABLE_ALPHA CURL_DISABLE_DEPRECATION CURL_DISABLE_BETA\n");
        Write("docs/CURL-DISABLE.md", "## `CURL_DISABLE_ALPHA`\n## `CURL_DISABLE_BETA`\n## `CURL_DISABLE_TYPECHECK`\n# `CURL_DISABLE_OTHER`\n");

        (string output, int exitCode) = UpstreamTest1165Script.Check(root);

        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void Check_OptionsOnlyInBuildFiles_ReportsUseAndDocumentation()
    {
        Write("configure.ac", "CURL_DISABLE_ONE CURL_DISABLE_TWO\nCURL_DISABLE_TYPECHECK\n");
        Write("m4/x.m4", "CURL_DISABLE_TYPECHECK\n");
        Write("m4/readme.txt", "CURL_DISABLE_IGNORED\n");
        Write("CMakeLists.txt", "CURL_DISABLE_CM\nCURL_DISABLE_INSTALL\nCURL_DISABLE_TYPECHECK\n");

        (string output, int exitCode) = UpstreamTest1165Script.Check(root);

        Assert.AreEqual(
            "Present in configure.ac, not used by code: CURL_DISABLE_ONE\n"
            + "Present in configure.ac, not documented in CURL-DISABLE.md: CURL_DISABLE_ONE\n"
            + "Present in CMakeLists.txt, not used by code: CURL_DISABLE_CM\n"
            + "Present in CMakeLists.txt, not documented in CURL-DISABLE.md: CURL_DISABLE_CM\n"
            + "Present in CMakeLists.txt, not propagated via curl_config-cmake.h.in: CURL_DISABLE_CM\n",
            output);
        Assert.AreEqual(5, exitCode);
    }

    [TestMethod]
    public void Check_CodeAndDocumentationOnly_ReportsTheLastFileAndEachMissingPlace()
    {
        Write("include/curl/a.h", "CURL_DISABLE_CODE\n");
        Write("src/b.c", "CURL_DISABLE_CODE\n");
        Write("lib/vtls/c.h", "CURL_DISABLE_CODE\n");
        Write("lib/vauth/notes.txt", "CURL_DISABLE_SKIPPED\n");
        Write("docs/CURL-DISABLE.md", "## `CURL_DISABLE_DOC`\n");

        (string output, int exitCode) = UpstreamTest1165Script.Check(root);

        Assert.AreEqual(
            $"Not set by configure: CURL_DISABLE_CODE ({root}/lib/vtls/c.h)\n"
            + $"Not set by CMakeLists.txt: CURL_DISABLE_CODE ({root}/lib/vtls/c.h)\n"
            + "Used in code, not documented in CURL-DISABLE.md: CURL_DISABLE_CODE\n"
            + "Documented but not in configure: CURL_DISABLE_DOC\n"
            + "Documented but not in CMakeLists.txt: CURL_DISABLE_DOC\n"
            + "Documented, but not used by code: CURL_DISABLE_DOC\n",
            output);
        Assert.AreEqual(6, exitCode);
    }

    [TestMethod]
    public void Check_FirstOptionOnALineOnly_CountsOneInBuildFilesAndAllInSources()
    {
        Write("configure.ac", "CURL_DISABLE_FIRST CURL_DISABLE_SECOND\n");
        Write("lib/a.c", "CURL_DISABLE_FIRST CURL_DISABLE_SECOND\n");

        (string output, _) = UpstreamTest1165Script.Check(root);

        StringAssert.Contains(output, "Not set by configure: CURL_DISABLE_SECOND");
        Assert.DoesNotContain("Not set by configure: CURL_DISABLE_FIRST", output);
    }

    [TestMethod]
    public void Check_MoreThan255Differences_ExitsWithTheCountModulo256()
    {
        Write("lib/a.c", string.Concat(Enumerable.Range(0, 86).Select(number => $"CURL_DISABLE_N{number}\n")));

        (string output, int exitCode) = UpstreamTest1165Script.Check(root);

        Assert.AreEqual(258, output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.AreEqual(2, exitCode);
    }

    [TestMethod]
    public void Check_MissingSourceFolder_Throws()
    {
        Directory.Delete(Path.Combine(root, "src"));

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => UpstreamTest1165Script.Check(root));
    }

    [TestMethod]
    public async Task RunAsync_TheSourceRoot_WritesTheCheckToStandardOutput()
    {
        Write("lib/a.c", "CURL_DISABLE_ONLY\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1165Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(3, exitCode);
        Assert.StartsWith("Not set by configure: CURL_DISABLE_ONLY (", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1165Script.RunAsync(null!, _ => Task.FromResult(0)));

    private void Write(string relativePath, string text) => File.WriteAllText(Path.Combine(root, relativePath), text, Encoding.Latin1);

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
