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
    [TestMethod]
    public void ForWindowsOpen_GivesTheCreateFileLine() =>
        Assert.AreEqual(
            "Warning: Failed to set filetime 1700000000 on outfile: CreateFile failed: GetLastError 0x00000002",
            RemoteTimeFailureWarning.ForWindowsOpen(1700000000, 2));

    [TestMethod]
    public void ForWindowsStamp_GivesTheSetFileTimeLine() =>
        Assert.AreEqual(
            "Warning: Failed to set filetime 1700000000 on outfile: SetFileTime failed: GetLastError 0x00000057",
            RemoteTimeFailureWarning.ForWindowsStamp(1700000000, 87));

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
    public void ForPosix_GivesTheFileAndTheErrnoInWords(bool runsOnMacOS, int errorNumber, string description) =>
        Assert.AreEqual(
            $"Warning: Failed to set filetime 1700000000 on 'out.txt': {description}",
            RemoteTimeFailureWarning.ForPosix(1700000000, "out.txt", errorNumber, CRuntimeErrorNumbers.For(runsOnWindows: false, runsOnMacOS)));
}
