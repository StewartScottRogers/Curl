namespace Curl.Core;

/// <summary>
/// Pins the directories <see cref="OutputDirectoryHierarchy" /> gives, row by row as upstream
/// <c>tests/tunit/tool1720.c</c> (curl 8.22.0) checks <c>create_dir_hierarchy</c>'s
/// <c>mkdir</c> trace, joined with <c>|</c> after each directory as the C trace is; both rule
/// sets run on every operating system (ADR-0482).
/// </summary>
[TestClass]
public sealed class OutputDirectoryHierarchyTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("filename", "")]
    [DataRow("foo/bar/", "foo|foo/bar|")]
    [DataRow("foo/bar/filename", "foo|foo/bar|")]
    [DataRow("/foo/bar/filename", "/foo|/foo/bar|")]
    public void DirectoriesToCreate_SharedRows_GiveTool1720sTraceUnderBothRuleSets(string outputFile, string expectedTrace)
    {
        Assert.AreEqual(expectedTrace, Trace(outputFile, runsOnWindows: true), "Windows rule set");
        Assert.AreEqual(expectedTrace, Trace(outputFile, runsOnWindows: false), "POSIX rule set");
    }

    [TestMethod]
    [DataRow("C:/foo/bar/filename", "C:/foo|C:/foo/bar|")]
    [DataRow("C:foo/bar/filename", "C:foo|C:foo/bar|")]
    [DataRow("foo\\bar\\filename", "foo|foo\\bar|")]
    [DataRow("\\foo\\bar\\filename", "\\foo|\\foo\\bar|")]
    [DataRow("C:\\foo\\bar\\filename", "C:\\foo|C:\\foo\\bar|")]
    [DataRow("C:foo\\bar\\filename", "C:foo|C:foo\\bar|")]
    public void DirectoriesToCreate_WindowsRows_GiveTool1720sTraceUnderTheWindowsRuleSet(string outputFile, string expectedTrace)
    {
        Assert.AreEqual(expectedTrace, Trace(outputFile, runsOnWindows: true));
    }

    [TestMethod]
    [DataRow("C:/foo/bar/filename", "C:|C:/foo|C:/foo/bar|")]
    [DataRow("C:foo/bar/filename", "C:foo|C:foo/bar|")]
    [DataRow("foo\\bar\\filename", "")]
    [DataRow("C:\\foo/bar\\filename", "C:\\foo|")]
    public void DirectoriesToCreate_WindowsRowsUnderThePosixRuleSet_KeepDrivesAndBackslashes(string outputFile, string expectedTrace)
    {
        Assert.AreEqual(expectedTrace, Trace(outputFile, runsOnWindows: false));
    }

    [TestMethod]
    [DataRow("/a//b/c.txt", "/a|/a//b|")]
    [DataRow("::/x/f", "::/x|")]
    [DataRow("/C:/x/f", "/C:|/C:/x|")]
    public void DirectoriesToCreate_DoubledSeparatorsAndDriveLikeParts_FollowCurlsLoop(string outputFile, string expectedTrace)
    {
        Assert.AreEqual(expectedTrace, Trace(outputFile, runsOnWindows: true));
    }

    [TestMethod]
    public void DirectoriesToCreate_StoppedEarly_ReadsNoFurther()
    {
        string first = OutputDirectoryHierarchy.DirectoriesToCreate("a/b/c/d", runsOnWindows: false).First();

        Assert.AreEqual("a", first);
    }

    private static string Trace(string outputFile, bool runsOnWindows) =>
        string.Concat(OutputDirectoryHierarchy.DirectoriesToCreate(outputFile, runsOnWindows).Select(directory => directory + "|"));
}
