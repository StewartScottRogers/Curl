using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1477Script"/>: upstream's <c>tests/test1477.pl</c> over the headers and the
/// <c>libcurl-errors</c> man page, or its Markdown source when the build root has no man page, which names count,
/// and each difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1477ScriptTests
{
    private string root = null!;

    private string buildRoot = null!;

    [TestInitialize]
    public void CreateFolders()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "log", $"test1477-{Guid.NewGuid():N}");
        root = Directory.CreateDirectory(Path.Combine(folder, "source")).FullName;
        buildRoot = Directory.CreateDirectory(Path.Combine(folder, "build")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "include", "curl"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "libcurl"));
    }

    [TestCleanup]
    public void DeleteFolders() => Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);

    [TestMethod]
    public void Check_NoManPageInTheBuildRoot_ReadsTheMarkdownHeadingsAndWritesEachDifference()
    {
        Write(root, "include/curl/a.h", "typedef enum {\n  CURLE_OK = 0,\n  CURLE_OBSOLETE1,\n  CURLE_RESERVED115,\n  CURLE_LAST\n   CURLE_THREE_BLANKS,\n CURLE_ONE_BLANK,\n  CURLUE_UNDOCUMENTED\n} CURLcode;\n");
        Write(root, "include/curl/b.h", "  CURLE_OK\n  CURLM_BOTH\n");
        Write(root, "include/curl/notes.txt", "  CURLE_NOT_A_HEADER\n");
        Write(root, "docs/libcurl/libcurl-errors.md", "## CURLE_OK (0)\n## CURLM_CALL_MULTI_SOCKET (-1)\n## CURLHE_NO_HEADER (1)\n## CURLM_BOTH\n");

        var (standardOutput, standardError) = UpstreamTest1477Script.Check(root, buildRoot);

        Assert.AreEqual("double: CURLE_OK\n", standardError);
        Assert.AreEqual(
            "Result\n"
            + $"CURLUE_UNDOCUMENTED from {root}/include/curl/a.h:8, not in man page\n"
            + $"CURLHE_NO_HEADER from {root}/docs/libcurl/libcurl-errors.md:3, not in any header\n",
            standardOutput);
    }

    [TestMethod]
    public void Check_AManPageInTheBuildRoot_ReadsItsIpLines()
    {
        Write(root, "include/curl/a.h", "  CURLE_OK\n");
        Write(root, "docs/libcurl/libcurl-errors.md", "## CURLE_ONLY_IN_MARKDOWN\n");
        Write(buildRoot, "docs/libcurl/libcurl-errors.3", ".IP \"CURLE_OK (0)\"\n.IP \"CURLM_CALL_MULTI_SOCKET (-1)\"\n## CURLE_NOT_AN_IP_LINE\n");

        var (standardOutput, standardError) = UpstreamTest1477Script.Check(root, buildRoot);

        Assert.AreEqual("Result\n", standardOutput);
        Assert.AreEqual(string.Empty, standardError);
    }

    [TestMethod]
    public async Task RunAsync_TheTwoRoots_WritesTheCheckToStandardOutputAndError()
    {
        Write(root, "include/curl/a.h", "  CURLE_OK\n  CURLE_OK\n");
        Write(root, "docs/libcurl/libcurl-errors.md", "## CURLE_OK (0)\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root, buildRoot], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1477Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("Result\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual("double: CURLE_OK\n", Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1477Script.RunAsync(null!, _ => Task.FromResult(0)));

    private static void Write(string folder, string relativePath, string text)
    {
        string path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
