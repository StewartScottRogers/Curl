using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1488Script"/>: upstream's <c>tests/test1488.pl</c> over man pages and a
/// <c>symbols-in-versions</c> that agree, each kind of difference it reports, and the <c>die</c> on too few past
/// releases.
/// </summary>
[TestClass]
public sealed class UpstreamTest1488ScriptTests
{
    private string root = null!;

    private string Versions => Path.Combine(root, "docs", "VERSIONS.md");

    private string LibcurlFolder => $"{root}/docs/libcurl";

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1488-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "docs", "libcurl", "opts"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_ManPagesAgreeingWithSymbolsInVersions_WritesOk()
    {
        WriteVersions(releaseCount: 100);
        WritePage("curl_a.md", "Title: curl_a\n", "Added-in: 7.1\n", "Title: ignored once both are seen\n");
        WritePage("opts/CURLOPT_B.md", "title: CURLOPT_B\n", "ADDED-IN: 7.2\n");
        WritePage("curl_none.md", "Title: curl_none\n", "Added-in: n/a\n");
        WritePage("no-added-in.md", "Title: curl_c\n");
        WritePage("md", "Title: not a page: nothing before md\n", "Added-in: 9.9\n");
        WritePage("other.txt", "Title: not a page\n", "Added-in: 9.9\n");
        WriteSymbols(" Name   Introduced\n", "\n", "curl_a          7.1\n", "CURLOPT_B 7.2\n", "curl_c   7.3\n", "NOVERSION\n");

        (int exitCode, string standardOutput, string standardError) = UpstreamTest1488Script.Check(root, Versions);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("OK\n", standardOutput);
        Assert.AreEqual(string.Empty, standardError);
    }

    [TestMethod]
    public void Check_EveryKindOfDifference_WritesEachAndCountsThem()
    {
        WriteVersions(releaseCount: 100);
        WritePage("a-first.md", "Title: curl_dup\n", "Added-in: 7.1\n");
        WritePage("b-second.md", "Added-in: 7.2\n", "Title: curl_dup\n");
        WritePage("c-never.md", "Title: curl_never\n", "Added-in: 9.9\n");
        WritePage("d-empty.md", "Title: curl_empty\n", "Added-in: \n");
        WriteSymbols("curl_dup 7.1\n", "curl_never 9.9\n", "curl_gone\n", "curl_late 8.8\n");

        (int exitCode, string standardOutput, _) = UpstreamTest1488Script.Check(root, Versions);

        string expected =
            $"curl_dup is a duplicate symbol in file {LibcurlFolder}/b-second.md\n"
            + $"{LibcurlFolder}/c-never.md: was added in a never released version: 9.9\n"
            + $"{LibcurlFolder}/d-empty.md: was added in a never released version: \n"
            + "SIV: says curl_never was added in non-existing 9.9\n"
            + "SIV: says curl_late was added in non-existing 8.8\n"
            + "curl_dup.md says version 7.2, but SIV says 7.1\n";
        Assert.AreEqual(expected, standardOutput);
        Assert.AreEqual(6, exitCode);
    }

    [TestMethod]
    public void Check_FewerThanAHundredReleases_DiesWithExitCode255()
    {
        WriteVersions(releaseCount: 99);

        (int exitCode, string standardOutput, string standardError) = UpstreamTest1488Script.Check(root, Versions);

        Assert.AreEqual(255, exitCode);
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual($"too few releases (99) found in {Versions}\n", standardError);
    }

    [TestMethod]
    public async Task RunAsync_TheCommandsRootAndVersions_WritesTheCheckToStandardOutput()
    {
        WriteVersions(releaseCount: 100);
        WriteSymbols();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root, "../include/curl", Versions], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1488Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("OK\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1488Script.RunAsync(null!, _ => Task.FromResult(0)));

    // allversions.pm counts only "- VERSION: DATE" lines after the "## Past releases" heading.
    private void WriteVersions(int releaseCount) =>
        File.WriteAllText(
            Versions,
            "- 1.0: before the heading\n## Past releases\n- : no version\n- 7.0 no colon\nnot a release\n"
            + string.Concat(Enumerable.Range(1, releaseCount).Select(minor => $"- 7.{minor}: May 1 2000\n")));

    private void WritePage(string name, params string[] lines) =>
        File.WriteAllText(Path.Combine(root, "docs", "libcurl", name), string.Concat(lines));

    private void WriteSymbols(params string[] lines) =>
        File.WriteAllText(Path.Combine(root, "docs", "libcurl", "symbols-in-versions"), string.Concat(lines));

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
