using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1119Script"/>: upstream's <c>tests/test1119.pl</c> over the headers' defines and enum
/// members, the man pages' symbol names and <c>symbols-in-versions</c>, what each side counts, and every kind of
/// difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1119ScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1119-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "include", "curl"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "libcurl", "opts"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_HeadersAndManPagesMatchingTheList_WritesOk()
    {
        Write(
            "include/curl/curl.h",
            "#define CURLOPT(na,t,nu) na = t + nu\n"
            + "#define CURL_EXTERN extern\n"
            + "#define LIBCURL_VERSION \"8\"\n"
            + "#  define CURLINDENTED_DEFINE 1\n"
            + "#if defined(X) && \\\n"
            + "  CURLINSIDE_DIRECTIVE\n"
            + "#endif\n"
            + "/* enum { CURLE_IN_COMMENT,\n"
            + "   CURLE_STILL_IN_COMMENT } */\n"
            + "typedef enum {\n"
            + "  CURLE_OK, /* CURLE_COMMENTED */\n"
            + "  CURLE_OBSOLETE4, // CURLE_LINE_COMMENT\n"
            + "  CURLOPT(CURLOPT_URL, 10000, 2),\n"
            + "  OTHER(CURLE_UNKNOWN_MACRO_CALL),\n"
            + "  notcurl,\n"
            + "  CURLE_LAST\n"
            + "} CURLcode;\n"
            + "  CURLE_OUTSIDE_ENUM,\n");
        Write("include/curl/notes.txt", "#define CURLNOT_A_HEADER 1\n");
        WriteSymbolsInVersions(" Name  Introduced\n", "\n", "CURLOPT  7.1\n", "CURLE_OK  7.1\n", "CURLOPT_URL  7.1\n", "LIBCURL_VERSION  7.1\n", "CURLE_GONE  7.1  7.2  7.3\n", "CURLOPT_PRIVATE  7.1  -  7.9\n", "CURLINDENTED_DEFINE  7.1  -  7.2\n");
        Write("docs/libcurl/a.md", "Use *CURLOPT_URL* with CURLE_OBSOLETE4, CURLOPT_TEMPLATE_X, CURLAUTH_X_ and CURLE_GONE.\n");
        Write("docs/libcurl/notes.txt", "CURLOPT_NOT_A_MAN_PAGE\n");
        Write("docs/libcurl/opts/CURLOPT_PRIVATE.md", "# CURLOPT_PRIVATE\n");

        Assert.AreEqual("OK\n", UpstreamTest1119Script.Check(root));
    }

    [TestMethod]
    public void Check_EveryKindOfDifference_WritesEachAndNoOk()
    {
        Write("include/curl/curl.h", "#define CURLNEW 1\n");
        WriteSymbolsInVersions("CURLDUP  7.1\n", "CURLDUP  7.1\n", "CURLMISSING  7.1\n", "CURLREMOVED  7.1  -  7.2\n");
        Write("docs/libcurl/m.md", "see CURLOPT_PRIVATEX and CURLOPT_PRIVATEX\nand CURLOPT_PRIVATEX\n");

        Assert.AreEqual(
            "Detected duplicate symbol: CURLDUP\n"
            + "CURLNEW\n"
            + "Missing symbols mentioned in symbols-in-versions\nAdd them to a header, or mark them as removed.\n"
            + "CURLDUP\nCURLMISSING\n"
            + $"{root}/docs/libcurl/m.md:1: CURLOPT_PRIVATEX is not a public symbol\n"
            + $"{root}/docs/libcurl/m.md:2: CURLOPT_PRIVATEX is not a public symbol\n",
            UpstreamTest1119Script.Check(root));
    }

    [TestMethod]
    public async Task RunAsync_TheSourceRoot_WritesTheCheckToStandardOutput()
    {
        WriteSymbolsInVersions();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root, "../include/curl"], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1119Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("OK\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1119Script.RunAsync(null!, _ => Task.FromResult(0)));

    private void WriteSymbolsInVersions(params string[] lines) => Write("docs/libcurl/symbols-in-versions", string.Concat(lines));

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
