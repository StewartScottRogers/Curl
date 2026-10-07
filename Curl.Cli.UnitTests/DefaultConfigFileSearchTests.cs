using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins where curl 8.21.0 looks for <c>.curlrc</c>. The Windows order was measured on 2026-09-27 with
/// the local curl 8.21.0 (mingw, Schannel) in Git Bash: every directory below was given its own file
/// holding one unknown option (<c>bogus-&lt;tag&gt;</c>), the variables were set with <c>env</c> and
/// <c>curl -V</c> was run; curl names the file it read in its error line
/// (<c>curl: &lt;path&gt;:1 config file option 'bogus-&lt;tag&gt;' is unknown</c>), and each file was then
/// deleted to reveal the next. The Linux and macOS order follows <c>src/tool_findfile.c</c> and the manual.
/// </summary>
[TestClass]
public sealed class DefaultConfigFileSearchTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void CandidatePaths_OnWindowsWithEveryVariable_FollowCurlsOrder()
    {
        DefaultConfigFileSearch search = Search(
            isWindows: true,
            executableDirectory: @"C:\exe",
            ("CURL_HOME", @"C:\ch"),
            ("HOME", @"C:\home"),
            ("USERPROFILE", @"C:\up"),
            ("APPDATA", @"C:\ad"));

        string[] expected = new[]
            {
                @"C:\ch\.curlrc",
                @"C:\ch\_curlrc",
                @"C:\home\.curlrc",
                @"C:\home\_curlrc",
                @"C:\up\.curlrc",
                @"C:\up\_curlrc",
                @"C:\ad\.curlrc",
                @"C:\ad\_curlrc",
                @"C:\up\Application Data\.curlrc",
                @"C:\up\Application Data\_curlrc",
                @"C:\ch/.config\curlrc",
                @"C:\exe\.curlrc",
                @"C:\exe\_curlrc",
            };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void CandidatePaths_OnWindowsWithXdgConfigHome_TryItsCurlrcThenOnlyDottedNames()
    {
        // Measured: with XDG_CONFIG_HOME set to an empty directory, HOME\_curlrc was passed over for
        // USERPROFILE\.curlrc, and HOME/.config\curlrc was never read.
        DefaultConfigFileSearch search = Search(
            isWindows: true,
            executableDirectory: @"C:\exe",
            ("XDG_CONFIG_HOME", @"C:\xdg"),
            ("HOME", @"C:\home"),
            ("USERPROFILE", @"C:\up"));

        string[] expected = new[]
            {
                @"C:\xdg\curlrc",
                @"C:\home\.curlrc",
                @"C:\up\.curlrc",
                @"C:\up\Application Data\.curlrc",
                @"C:\exe\.curlrc",
                @"C:\exe\_curlrc",
            };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void CandidatePaths_OnWindowsWithCurlHome_NeverReachHomeConfigDirectory()
    {
        // Measured: CURL_HOME and HOME set, only HOME/.config\curlrc present: no file was read.
        DefaultConfigFileSearch search = Search(isWindows: true, executableDirectory: null, ("CURL_HOME", @"C:\ch"), ("HOME", @"C:\home"));

        string[] expected = new[] { @"C:\ch\.curlrc", @"C:\ch\_curlrc", @"C:\home\.curlrc", @"C:\home\_curlrc", @"C:\ch/.config\curlrc" };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void CandidatePaths_OnWindowsWithEmptyCurlHome_SkipIt()
    {
        // Measured: CURL_HOME= (empty) and HOME set: HOME/.config\curlrc was read.
        DefaultConfigFileSearch search = Search(isWindows: true, executableDirectory: string.Empty, ("CURL_HOME", string.Empty), ("HOME", @"C:\home"));

        string[] expected = new[] { @"C:\home\.curlrc", @"C:\home\_curlrc", @"C:\home/.config\curlrc" };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void CandidatePaths_OnWindowsWithNoVariables_TryOnlyTheExecutableDirectory()
    {
        DefaultConfigFileSearch search = Search(isWindows: true, executableDirectory: @"C:\exe");

        string[] expected = new[] { @"C:\exe\.curlrc", @"C:\exe\_curlrc" };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void CandidatePaths_ElsewhereWithEveryVariable_FollowTheManualsOrder()
    {
        DefaultConfigFileSearch search = Search(
            isWindows: false,
            executableDirectory: "/usr/bin",
            ("CURL_HOME", "/ch"),
            ("HOME", "/home/u"),
            ("USERPROFILE", "/up"),
            ("APPDATA", "/ad"));

        string[] expected = new[] { "/ch/.curlrc", "/home/u/.curlrc", "/ch/.config/curlrc", "/account/.curlrc" };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void CandidatePaths_ElsewhereWithXdgConfigHome_SkipTheConfigDirectories()
    {
        DefaultConfigFileSearch search = Search(isWindows: false, executableDirectory: null, ("XDG_CONFIG_HOME", "/xdg"), ("HOME", "/home/u"));

        string[] expected = new[] { "/xdg/curlrc", "/home/u/.curlrc", "/account/.curlrc" };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void CandidatePaths_ElsewhereWithHomeOnly_TryHomeThenItsConfigDirectory()
    {
        Diagnostics.Arrange("isWindows", false);
        Diagnostics.Arrange("environment", "HOME=/home/u");
        DefaultConfigFileSearch search = new(name => name == "HOME" ? "/home/u" : null, false, null, null);

        string[] expected = new[] { "/home/u/.curlrc", "/home/u/.config/curlrc" };

        CollectionAssert.AreEqual(expected, WrittenCandidatePaths(expected, search));
    }

    [TestMethod]
    public void ForProcess_ListsAtLeastTheLastDirectory()
    {
        Diagnostics.Arrange("search", "DefaultConfigFileSearch.ForProcess");
        IReadOnlyList<string> paths = DefaultConfigFileSearch.ForProcess.CandidatePaths();
        Diagnostics.Act("candidate path count is positive", paths.Count > 0);

        Diagnostics.Assert("candidate paths are listed", true, paths.Count > 0);
        Assert.IsNotEmpty(paths);
    }

    /// <summary>Returns the search's candidate paths, writing them (ACT) and their comparison with <paramref name="expected"/> as diagnostics.</summary>
    private string[] WrittenCandidatePaths(string[] expected, DefaultConfigFileSearch search)
    {
        IReadOnlyList<string> actual = search.CandidatePaths();
        Diagnostics.Act("candidate paths", string.Join(" | ", actual));

        Diagnostics.Diff("candidate paths", string.Join('\n', expected), string.Join('\n', actual));
        Diagnostics.Assert("candidate path count", expected.Length, actual.Count);
        return actual.ToArray();
    }

    /// <summary>Builds a search over the given variables and writes each input as an ARRANGE line.</summary>
    private DefaultConfigFileSearch Search(bool isWindows, string? executableDirectory, params (string Name, string Value)[] variables)
    {
        Diagnostics.Arrange("isWindows", isWindows);
        Diagnostics.Arrange("executableDirectory", executableDirectory ?? "<null>");
        Diagnostics.Arrange("environment", variables.Length == 0 ? "<none>" : string.Join(" ", variables.Select(variable => variable.Name + "=" + variable.Value)));
        Dictionary<string, string> environment = variables.ToDictionary(variable => variable.Name, variable => variable.Value, StringComparer.Ordinal);
        return new(environment.GetValueOrDefault, isWindows, executableDirectory, "/account");
    }
}
