using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamTest971Script"/>: upstream's <c>tests/test971.pl</c> over a matching set of
/// <c>options-in-versions</c>, option files and <c>VERSIONS.md</c>, each kind of difference it reports, and the
/// <c>die</c> on too few past releases.
/// </summary>
[TestClass]
public sealed class UpstreamTest971ScriptTests
{
    private string folder = null!;

    private string OptionsInVersions => Path.Combine(folder, "options-in-versions");

    private string OptionFolder => Path.Combine(folder, "cmdline-opts");

    private string Versions => Path.Combine(folder, "VERSIONS.md");

    [TestInitialize]
    public void CreateFolder()
    {
        folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "log", $"test971-{Guid.NewGuid():N}")).FullName;
        Directory.CreateDirectory(OptionFolder);
    }

    [TestCleanup]
    public void DeleteFolder() => Directory.Delete(folder, recursive: true);

    [TestMethod]
    public void Check_OptionsInVersionsMatchingTheFiles_WritesOk()
    {
        WriteVersions(releaseCount: 100);
        WriteOption("alpha", "Short: a\n", "Added: \n", "Added: 7.1\n", "Added: 7.2\n");
        WriteOption("b", "Long: b\n");
        File.WriteAllText(Path.Combine(OptionFolder, "_A.md"), "not an option file: no lowercase letter or digit\n");
        File.WriteAllText(Path.Combine(OptionFolder, "alpha.txt"), "not an option file: no .md\n");
        WriteOptionsInVersions("  Option                     Introduced\n", "\n", "--alpha (-a)               7.1\n", "b                         7.2\n");

        var (exitCode, standardError) = UpstreamTest971Script.Check(OptionsInVersions, OptionFolder, Versions);

        Assert.AreEqual("ok\n", standardError);
        Assert.AreEqual(0, exitCode);
    }

    [TestMethod]
    public void Check_OptionsInVersionsDifferingFromTheFiles_WritesEachProblem()
    {
        WriteVersions(releaseCount: 100);
        WriteOption("a", "Added: 7.2\n");
        WriteOption("b", "Long: b\n");
        WriteOption("c", "Added: 7.1\n");
        WriteOptionsInVersions("--a (-x)                  7.1\n", "--b                       9.9\n", "--d                       7.3\n");

        var (exitCode, standardError) = UpstreamTest971Script.Check(OptionsInVersions, OptionFolder, Versions);

        Assert.AreEqual(
            "a lists 7.1 in doc but 7.2 in file\n"
            + "b: 9.9 is not a proper release\n"
            + $"--c is in the option directory but not in {OptionsInVersions}!\n"
            + "d is in the doc but NOT as a file!\n",
            standardError);
        Assert.AreEqual(4, exitCode);
    }

    [TestMethod]
    public void Check_FewerThanAHundredPastReleases_DiesWithExitCode255()
    {
        WriteVersions(releaseCount: 99);
        WriteOptionsInVersions();

        var (exitCode, standardError) = UpstreamTest971Script.Check(OptionsInVersions, OptionFolder, Versions);

        Assert.AreEqual($"too few releases (99) found in {Versions}\n", standardError);
        Assert.AreEqual(255, exitCode);
    }

    [TestMethod]
    public async Task RunAsync_TheCommandsThreeFiles_WritesTheCheckToStandardError()
    {
        WriteVersions(releaseCount: 100);
        WriteOption("a", "Added: 7.1\n");
        WriteOptionsInVersions("--a                       7.1\n");
        using MemoryStream standardOutput = new();
        using MemoryStream standardError = new();
        UpstreamCurlInvocation invocation = new([OptionsInVersions, OptionFolder, Versions], standardOutput, standardError, Stream.Null, new UnusedConnector(), new UnreachableDatagramConnector());

        int exitCode = await UpstreamTest971Script.RunAsync(invocation, _ => throw new AssertFailedException("the script runs no curl"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("ok\n", Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_NullInvocation_Throws() =>
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => UpstreamTest971Script.RunAsync(null!, _ => Task.FromResult(0)));

    // allversions.pm counts only "- VERSION: DATE" lines after the "## Past releases" heading.
    private void WriteVersions(int releaseCount) =>
        File.WriteAllText(
            Versions,
            "- 1.0: before the heading\n## Past releases\n- : no version\n- 7.0 no colon\nnot a release\n"
            + string.Concat(Enumerable.Range(1, releaseCount).Select(minor => $"- 7.{minor}: May 1 2000\n")));

    private void WriteOption(string name, params string[] lines) =>
        File.WriteAllText(Path.Combine(OptionFolder, name + ".md"), string.Concat(lines));

    private void WriteOptionsInVersions(params string[] lines) => File.WriteAllText(OptionsInVersions, string.Concat(lines));

    private sealed class UnusedConnector : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            throw new AssertFailedException("the script makes no connection");
    }
}
