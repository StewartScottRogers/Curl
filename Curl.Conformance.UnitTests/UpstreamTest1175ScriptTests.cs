using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1175Script"/>: upstream's <c>tests/test1175.pl</c> over <c>symbols-in-versions</c>
/// and <c>libcurl-errors.md</c>, which codes count as present and documented, and each difference it reports.
/// </summary>
[TestClass]
public sealed class UpstreamTest1175ScriptTests
{
    private string root = null!;

    [TestInitialize]
    public void CreateFolder()
    {
        root = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test1175-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "docs", "libcurl"));
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Check_RemovedAndObsoleteCodesLeftOut_WritesNothing()
    {
        WriteSymbolsInVersions(" Name  Introduced\n", "CURLE_OK  7.1\n", "CURLE_GONE  7.1  7.2\n", "CURLM_GONE  7.1  .9\n", "CURLM_KEPT  7.1  -  7.4\n", "CURLOPT_URL  7.1\n");
        WriteErrors("# libcurl-errors\n", "## CURLE_OK (0)\n", "## CURLE_OBSOLETE4 (4)\n", "## CURLM_KEPT (1)\n", "## CURLOPT_URL\n");

        Assert.AreEqual(string.Empty, UpstreamTest1175Script.Check(root));
    }

    [TestMethod]
    public void Check_CodesOnOneSideOnly_WritesEachInSortedOrder()
    {
        WriteSymbolsInVersions("CURLE_ZETA  7.1\n", "CURLE_ALPHA  7.1\n", "CURLE_BOTH  7.1\n");
        WriteErrors("## CURLM_DOCUMENTED (9)\n", "## CURLE_BOTH (1)\n");

        Assert.AreEqual(
            "CURLE_ALPHA is not in libcurl-errors.md\nCURLE_ZETA is not in libcurl-errors.md\nCURLM_DOCUMENTED is not in symbols-in-versions\n",
            UpstreamTest1175Script.Check(root));
    }

    [TestMethod]
    public async Task RunAsync_TheSourceRoot_WritesTheCheckToStandardOutput()
    {
        WriteSymbolsInVersions("CURLE_ALPHA  7.1\n");
        WriteErrors();
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([root], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest1175Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("CURLE_ALPHA is not in libcurl-errors.md\n", Encoding.UTF8.GetString(standardOutput.ToArray()));
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest1175Script.RunAsync(null!, _ => Task.FromResult(0)));

    private void WriteSymbolsInVersions(params string[] lines) =>
        File.WriteAllText(Path.Combine(root, "docs", "libcurl", "symbols-in-versions"), string.Concat(lines));

    private void WriteErrors(params string[] lines) =>
        File.WriteAllText(Path.Combine(root, "docs", "libcurl", "libcurl-errors.md"), string.Concat(lines));

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
