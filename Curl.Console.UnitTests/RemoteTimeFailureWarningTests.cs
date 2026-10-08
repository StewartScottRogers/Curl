using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.21.0's <c>Failed to set filetime</c> lines: the Windows build's
/// <c>CreateFile</c> and <c>SetFileTime</c> forms (<c>src/tool_filetime.c</c> lines 107-126) and
/// the POSIX build's <c>strerror</c> form (lines 128-148), each platform's <c>strerror</c> text
/// taken from an injected table so every row runs on every OS (BL-1433).
/// </summary>
[TestClass]
public sealed class RemoteTimeFailureWarningTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ForWindowsOpen_GivesTheCreateFileLine()
    {
        Diagnostics.Arrange("remote time / GetLastError", "1700000000 / 2");
        string line = RemoteTimeFailureWarning.ForWindowsOpen(1700000000, 2);
        Diagnostics.Act("warning", line);

        const string Expected = "Warning: Failed to set filetime 1700000000 on outfile: CreateFile failed: GetLastError 0x00000002";
        Diagnostics.Diff("warning", Expected, line);
        Assert.AreEqual(Expected, line);
    }

    [TestMethod]
    public void ForWindowsStamp_GivesTheSetFileTimeLine()
    {
        Diagnostics.Arrange("remote time / GetLastError", "1700000000 / 87");
        string line = RemoteTimeFailureWarning.ForWindowsStamp(1700000000, 87);
        Diagnostics.Act("warning", line);

        const string Expected = "Warning: Failed to set filetime 1700000000 on outfile: SetFileTime failed: GetLastError 0x00000057";
        Diagnostics.Diff("warning", Expected, line);
        Assert.AreEqual(Expected, line);
    }

    [TestMethod]
    [DataRow(false, 2, "No such file or directory")]
    [DataRow(true, 2, "No such file or directory")]
    [DataRow(false, 13, "Permission denied")]
    [DataRow(true, 13, "Permission denied")]
    [DataRow(false, 1, "Operation not permitted")]
    [DataRow(true, 1, "Operation not permitted")]
    [DataRow(false, 30, "Read-only file system")]
    [DataRow(true, 30, "Read-only file system")]
    [DataRow(false, 9999, "Unknown error 9999")]
    [DataRow(true, 9999, "Unknown error: 9999")]
    public void ForPosix_GivesTheFileAndTheErrnoInWords(bool runsOnMacOS, int errorNumber, string description)
    {
        Diagnostics.Arrange("runs on macOS / errno", $"{runsOnMacOS} / {errorNumber}");
        string line = RemoteTimeFailureWarning.ForPosix(1700000000, "out.txt", errorNumber, CRuntimeErrorNumbers.For(runsOnWindows: false, runsOnMacOS));
        Diagnostics.Act("warning", line);

        string expected = $"Warning: Failed to set filetime 1700000000 on 'out.txt': {description}";
        Diagnostics.Diff("warning", expected, line);
        Assert.AreEqual(expected, line);
    }
}
