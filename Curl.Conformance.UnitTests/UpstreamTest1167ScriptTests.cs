using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1167Script"/>: upstream's <c>tests/test1167.pl</c> over the headers' enum members and
/// defines, what it counts and what it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1167ScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1167-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "include", "curl"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_EveryPublicSymbolHasThePrefix_WritesTheCount()
    {
        Write(
            "include/curl/curl.h",
            "#define CURLOPT(na,t,nu) na = t + nu\n"
            + "#  define LIBCURL_VERSION \"8\"\n"
            + "#define Curl_Mixed 1\n"
            + "#if defined(X) && \\\n"
            + "  BAD_DIRECTIVE\n"
            + "#endif\n"
            + "/* enum { BAD_IN_COMMENT,\n"
            + "   BAD_STILL_IN_COMMENT } */\n"
            + "typedef enum {\n"
            + "  CURLE_OK = 0, /* BAD_COMMENTED */\n"
            + "  CURLE_NEXT, // BAD_LINE_COMMENT\n"
            + "\n"
            + "  =\n"
            + "  12\n"
            + "  CURLOPT(CURLOPT_URL, 10000, 2),\n"
            + "  CURLE_LAST\n"
            + "} CURLcode;\n"
            + "enum {\n"
            + "  CURLINFO_OLD\n"
            + "                       CURL_DEPRECATED(7.55.0, \"Use curl_new()\")\n"
            + "                            = 8,\n"
            + "  CURLINFO_NEW CURL_DEPRECATED(8.3.0, \"\") = 3\n"
            + "};\n"
            + "enum {\n  CURLOPT(CURLOPT_TWO, 1,\n    7.11.1, \"Use it\"),\n  CURLOPT_THREE\n};\n"
            + "enum curl_x { CURLX_ONE };\n"
            + "  BAD_OUTSIDE_ENUM,\n");
        Write("include/curl/notes.txt", "#define BAD_NOT_A_HEADER 1\n");

        Assert.AreEqual(("11 fine symbols found\n", 0), UpstreamTest1167Script.Check(root));
    }

    [TestMethod]
    public void Check_SymbolsWithoutThePrefix_ListsThemAndExits1()
    {
        Write("include/curl/a.h", "#define CURLA 1\n#define BAD_DEFINE 1\nenum {\n  BAD_MEMBER,\n  CURLB\n};\n");

        Assert.AreEqual(("Bad symbols in public header files:\n  BAD_MEMBER\n  BAD_DEFINE\n", 1), UpstreamTest1167Script.Check(root));
    }

    [TestMethod]
    public async Task RunAsync_TheSourceRoot_WritesTheCheckToStandardOutput()
    {
        Write("include/curl/a.h", "#define BAD 1\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1167Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual("Bad symbols in public header files:\n  BAD\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1167Script.RunAsync(null!, _ => Task.FromResult(0)));

    private void Write(string relativePath, string text)
    {
        string path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
