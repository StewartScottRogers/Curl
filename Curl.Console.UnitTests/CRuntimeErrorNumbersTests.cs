namespace Curl.Console;

/// <summary>
/// Pins the C runtime numbers and <c>strerror</c> texts of glibc, macOS and the Windows C
/// runtime that <see cref="CRuntimeErrorNumbers" /> holds (BL-1433).
/// </summary>
[TestClass]
public sealed class CRuntimeErrorNumbersTests
{
    [TestMethod]
    public void For_PicksWindowsThenMacOSThenLinux()
    {
        Assert.AreSame(CRuntimeErrorNumbers.Windows, CRuntimeErrorNumbers.For(runsOnWindows: true, runsOnMacOS: false));
        Assert.AreSame(CRuntimeErrorNumbers.MacOS, CRuntimeErrorNumbers.For(runsOnWindows: false, runsOnMacOS: true));
        Assert.AreSame(CRuntimeErrorNumbers.Linux, CRuntimeErrorNumbers.For(runsOnWindows: false, runsOnMacOS: false));
    }

    [TestMethod]
    [DataRow(5, "Input/output error")]
    [DataRow(17, "File exists")]
    [DataRow(20, "Not a directory")]
    [DataRow(28, "No space left on device")]
    [DataRow(36, "File name too long")]
    [DataRow(40, "Too many levels of symbolic links")]
    [DataRow(63, "Unknown error 63")]
    public void Describe_OnLinux_GivesGlibcText(int errorNumber, string expected) =>
        Assert.AreEqual(expected, CRuntimeErrorNumbers.Linux.Describe(errorNumber));

    [TestMethod]
    [DataRow(63, "File name too long")]
    [DataRow(62, "Too many levels of symbolic links")]
    [DataRow(36, "Unknown error: 36")]
    public void Describe_OnMacOS_GivesItsText(int errorNumber, string expected) =>
        Assert.AreEqual(expected, CRuntimeErrorNumbers.MacOS.Describe(errorNumber));
}
