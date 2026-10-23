using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1140Script"/>: upstream's <c>tests/test1140.pl</c> over built man pages and over pages
/// converted from Markdown, with each syntax problem it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1140ScriptTests
{
    private string root = null!;

    private string Docs => root + "/docs";

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1140-{Guid.NewGuid():N}")).FullName.Replace('\\', '/');
        Directory.CreateDirectory(Docs + "/libcurl/opts");
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Scan_UsageRoot_WritesUsage() =>
        Assert.AreEqual((0, "Usage: test1140.pl <docs root dir> [manpages]\n"), UpstreamTest1140Script.Scan(["-g"]));

    [TestMethod]
    public void Scan_NoArguments_ScansNothingAndIsOk() =>
        Assert.AreEqual((0, "OK\n"), UpstreamTest1140Script.Scan([]));

    [TestMethod]
    public void Scan_ZeroOrEmptyRoot_TakesTheCurrentFolder()
    {
        Assert.AreEqual((0, "OK\n"), UpstreamTest1140Script.Scan(["0"]));
        Assert.AreEqual((0, "OK\n"), UpstreamTest1140Script.Scan([string.Empty]));
    }

    [TestMethod]
    public void Scan_CleanBuiltPage_IsOk()
    {
        Write("libcurl/curl_a.3", ".SH SEE ALSO\n\\fIcurl_b(3)\\fP, \\fBplain\\fP\n.BR curl_b \"(3),\" curl_a \"(3)\"\n");
        Write("libcurl/curl_b.3", ".TH curl_b 3\n");

        Assert.AreEqual((0, "OK\n"), UpstreamTest1140Script.Scan([Docs + "/", Docs + "/libcurl/*.3"]));
    }

    [TestMethod]
    public void Scan_EachSyntaxProblem_IsReportedWithItsLine()
    {
        Write("libcurl/curl_a.3", "\\fB\\fIx\\fP\n\\fIcurl_missing(3)\\fX\n\\fBcurl_a(3)\\fP\nsee curl_a(3) here\n.BR curl_nope \"(3)\" libcurl-x \"(3),\"\n");

        (int exitCode, string output) = UpstreamTest1140Script.Scan([Docs, Docs + "/libcurl/*.3"]);

        string page = Docs + "/libcurl/curl_a.3";
        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(
            $"error: {page}:1: double-highlight\nerror: {page}:2: missing \\fP after curl_missing(3)\nerror: {page}:2: referring to non-existing man page curl_missing.3\n"
            + $"error: {page}:3: use \\fI before curl_a(3)\nerror: {page}:4: non-referencing curl_a(3)\n"
            + $"error: {page}:5: referring to non-existing man page curl_nope.3\nerror: {page}:5: referring to non-existing man page libcurl-x.3\n",
            output);
    }

    [TestMethod]
    public void Scan_ManPagesInEachFolder_AreFound()
    {
        Write("curl.3", string.Empty);
        Write("libcurl/curl_in_libcurl.3", string.Empty);
        Write("libcurl/opts/CURLOPT_X.3", string.Empty);
        Write("libcurl/curl_a.3", "\\fIcurl(3)\\fP \\fIcurl_in_libcurl(3)\\fP \\fICURLOPT_X(3)\\fP \\fIcurl\\-x(3)\\fP\n");

        (int exitCode, string output) = UpstreamTest1140Script.Scan([Docs, Docs + "/libcurl/curl_a.3"]);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual($"error: {Docs}/libcurl/curl_a.3:1: referring to non-existing man page curl-x.3\n", output);
    }

    [TestMethod]
    public void Scan_GlobWithoutAFolderThatMatchesNothing_ScansNothing() =>
        Assert.AreEqual((0, "OK\n"), UpstreamTest1140Script.Scan([Docs, "*.nomatch"]));

    [TestMethod]
    public void Scan_NamedFileMissing_Throws() =>
        Assert.ThrowsExactly<FileNotFoundException>(() => UpstreamTest1140Script.Scan([Docs, Docs + "/libcurl/none.3"]));

    [TestMethod]
    public void Scan_NoBuiltPages_ScansTheConvertedMarkdownAndSeesItsPages()
    {
        Write("libcurl/curl_a.md", "---\nTitle: curl_a\nSection: 3\nSee-also:\n  - curl_b (3)\nAdded-in: 7.1.0\n---\n\n# NAME\n\ncurl_a - x\n");
        Write("libcurl/curl_b.md", "---\nTitle: curl_b\nSection: 3\nSee-also:\n  - curl_a (3)\nAdded-in: 7.1.0\n---\n\n# NAME\n\ncurl_b - y\n");
        Write("libcurl/opts/CURLOPT_X.md", "---\nTitle: CURLOPT_X\nSection: 3\nSee-also:\n  - curl_a (3)\nAdded-in: 7.1.0\n---\n\n# NAME\n\nCURLOPT_X - z\n");

        Assert.AreEqual((0, "OK\n"), UpstreamTest1140Script.Scan([Docs, Docs + "/libcurl/*.3", Docs + "/libcurl/opts/*.3", Docs + "/*.1", Docs + "/gone/*.1"]));
    }

    [TestMethod]
    public void Scan_ConvertedPageWithAnUnknownReference_IsReported()
    {
        Write("libcurl/curl_a.md", "---\nTitle: curl_a\nSection: 3\nSee-also:\n  - curl_a (3)\nAdded-in: 7.1.0\n---\n\n# NAME\n\ncurl_a - x\n\nSee curl_zzz(3) now.\n");

        (int exitCode, string output) = UpstreamTest1140Script.Scan([Docs, Docs + "/libcurl/*.3"]);

        Assert.AreEqual(1, exitCode);
        StringAssert.Contains(output, "referring to non-existing man page curl_zzz.3");
    }

    [TestMethod]
    public async Task RunAsync_ScansAndReturnsTheExitCode()
    {
        Write("libcurl/curl_a.3", "\\fIcurl_b(3)\\fP\n");
        using MemoryStream standardOutput = new();
        UpstreamCurlInvocation invocation = new([Docs, Docs + "/libcurl/*.3"], standardOutput, new MemoryStream(), Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1140Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(1, exitCode);
        StringAssert.Contains(Encoding.UTF8.GetString(standardOutput.ToArray()), "non-existing man page curl_b.3");
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1140Script.RunAsync(null!, _ => Task.FromResult(0)));

    private void Write(string name, string text)
    {
        string path = Path.Combine(Docs, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
