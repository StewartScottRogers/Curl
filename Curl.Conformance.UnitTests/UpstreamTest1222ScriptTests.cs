using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest1222Script"/>: upstream's <c>tests/test1222.pl</c> skipping when the libcurl manual
/// was not built, its <c>die</c> without <c>symbols-in-versions</c>, and the failure it reports while its check over
/// built pages is not interpreted (BL-2996).
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
    public void Check_BuiltManPages_FailsAsNotInterpreted()
    {
        WriteSymbols();
        Directory.CreateDirectory(Path.Combine(BuildRoot, "docs", "libcurl"));
        File.WriteAllText(Path.Combine(BuildRoot, "docs", "libcurl", "curl_easy_setopt.3"), ".TH curl_easy_setopt 3\n");

        (int exitCode, string standardOutput, string standardError) = UpstreamTest1222Script.Check(root, BuildRoot);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(string.Empty, standardOutput);
        Assert.AreEqual("the deprecation check over the generated libcurl man pages is not interpreted (BL-2996)\n", standardError);
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

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
