using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1177Script"/>: upstream's <c>tests/test1177.pl</c> over a matching man page, header and
/// feature list, each kind of difference it reports, the <c>.3</c> man page read as the <c>.md</c> beside it, and the
/// vendored curl 8.22.0 files against Libcurl's feature names.
/// </summary>
[TestClass]
public sealed class UpstreamTest1177ScriptTests
{
    private const string Source = "lib/version.c";

    private static readonly string[] SourceNames = ["alt-svc", "ECH"];

    private string folder = null!;

    private string Header => Path.Combine(folder, "curl.h");

    [TestInitialize]
    public void CreateFolder() =>
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1177-{Guid.NewGuid():N}")).FullName;

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void Check_MarkdownAgreeingWithHeaderAndSource_WritesNothing()
    {
        string manPage = WriteFile(
            "curl_version_info.md",
            "# FEATURES\n",
            "## `alt-svc`\n",
            "*features* mask bit: CURL_VERSION_ALTSVC\n",
            "## `ECH`\n",
            "*features* mask bit: non-existent\n",
            "## no name\n",
            "*features* mask bit: curl_version_conv\n",
            "## \n");
        WriteHeader("#define CURL_VERSION_ALTSVC (1<<24)\n", "#DEFINE curl_version_conv (1<<12)\n", "#define CURL_VERSION_ 0\n", "#define CURLVERSION_NOW 11\n");

        var (exitCode, standardError) = UpstreamTest1177Script.Check(manPage, Header, Source, SourceNames);

        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void Check_ManPageDifferingFromHeaderAndSource_WritesEachProblem()
    {
        string manPage = WriteFile(
            "curl_version_info.md",
            "## `alt-svc`\n",
            "*features* mask bit: CURL_VERSION_ALTSVC\n",
            "## `Gone`\n",
            "*features* mask bit: CURL_VERSION_GONE\n");
        WriteHeader("#define CURL_VERSION_ALTSVC (1<<24)\n", "#define CURL_VERSION_NEW (1<<31)\n");

        var (exitCode, standardError) = UpstreamTest1177Script.Check(manPage, Header, Source, SourceNames);

        Assert.AreEqual(
            $"{manPage}: missing CURL_VERSION_NEW\n"
            + $"{manPage}: CURL_VERSION_GONE is not in the header!\n"
            + $"{manPage}: missing feature name ECH\n"
            + $"{Source}: missing feature name Gone\n",
            standardError);
        Assert.AreEqual(4, exitCode);
    }

    [TestMethod]
    public void Check_ManPageNamedAsAbsentDot3_ReadsTheMarkdownBesideIt()
    {
        WriteFile("curl_version_info.md", "## `alt-svc`\n", "## `ECH`\n");
        WriteHeader();
        string manPage = Path.Combine(folder, "curl_version_info.3");

        var (exitCode, standardError) = UpstreamTest1177Script.Check(manPage, Header, Source, SourceNames);

        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void Check_ManPageNamedAsPresentDot3_ReadsItsIpLines()
    {
        string manPage = WriteFile("curl_version_info.3", ".IP alt-svc\n", ".ip \"ECH\"\n", ".IP \"no name\"\n", ".SH FEATURES\n");
        WriteHeader();

        var (exitCode, standardError) = UpstreamTest1177Script.Check(manPage, Header, Source, SourceNames);

        Assert.AreEqual(string.Empty, standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void Check_AbsentManPageNotADot3_Throws()
    {
        WriteHeader();

        Assert.ThrowsExactly<FileNotFoundException>(() => UpstreamTest1177Script.Check(Path.Combine(folder, "absent.md"), Header, Source, SourceNames));
    }

    [TestMethod]
    public void Check_ThreeHundredProblems_ExitsWithTheirCountModulo256()
    {
        string manPage = WriteFile("curl_version_info.md");
        WriteHeader([.. Enumerable.Range(0, 300).Select(bit => $"#define CURL_VERSION_B{bit} 0\n")]);

        var (exitCode, _) = UpstreamTest1177Script.Check(manPage, Header, Source, []);

        Assert.AreEqual(300 - 256, exitCode);
    }

    // Case 1177's command: the vendored curl 8.22.0 man page and curl.h, with Libcurl standing for version.c.
    [TestMethod]
    public async Task RunAsync_VendoredFilesAndLibcurlFeatureNames_AgreeAndExitZero()
    {
        string release = Path.Combine(AppContext.BaseDirectory, "UpstreamTestData");
        string manPage = Path.Combine(release, "docs", "libcurl", "curl_version_info.3");
        string header = Path.Combine(release, "include", "curl", "curl.h");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([manPage, header, Source], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1177Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(string.Empty, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1177Script.RunAsync(null!, _ => Task.FromResult(0)));

    private string WriteFile(string name, params string[] lines)
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, string.Concat(lines));
        return path;
    }

    private void WriteHeader(params string[] lines) => WriteFile("curl.h", lines);

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
