using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the C runtime numbers and <c>strerror</c> texts of glibc, macOS and the Windows C
/// runtime that <see cref="CRuntimeErrorNumbers" /> holds (BL-1433).
/// </summary>
[TestClass]
public sealed class CRuntimeErrorNumbersTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void For_PicksWindowsThenMacOSThenLinux()
    {
        Diagnostics.Arrange("platform flags", "windows, macOS, then neither");
        bool windowsPicked = ReferenceEquals(CRuntimeErrorNumbers.Windows, CRuntimeErrorNumbers.For(runsOnWindows: true, runsOnMacOS: false));
        bool macOSPicked = ReferenceEquals(CRuntimeErrorNumbers.MacOS, CRuntimeErrorNumbers.For(runsOnWindows: false, runsOnMacOS: true));
        bool linuxPicked = ReferenceEquals(CRuntimeErrorNumbers.Linux, CRuntimeErrorNumbers.For(runsOnWindows: false, runsOnMacOS: false));
        Diagnostics.Act("picked Windows table", windowsPicked);
        Diagnostics.Act("picked macOS table", macOSPicked);
        Diagnostics.Act("picked Linux table", linuxPicked);

        Diagnostics.Assert("Windows table picked", true, windowsPicked);
        Diagnostics.Assert("macOS table picked", true, macOSPicked);
        Diagnostics.Assert("Linux table picked", true, linuxPicked);
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
    public void Describe_OnLinux_GivesGlibcText(int errorNumber, string expected)
    {
        Diagnostics.Arrange("error number", errorNumber);
        Diagnostics.Arrange("expected text", expected);
        string actual = CRuntimeErrorNumbers.Linux.Describe(errorNumber);
        Diagnostics.Act("described text", actual);

        Diagnostics.Assert("Linux description", expected, actual);
        Assert.AreEqual(expected, CRuntimeErrorNumbers.Linux.Describe(errorNumber));
    }

    [TestMethod]
    [DataRow(63, "File name too long")]
    [DataRow(62, "Too many levels of symbolic links")]
    [DataRow(36, "Unknown error: 36")]
    public void Describe_OnMacOS_GivesItsText(int errorNumber, string expected)
    {
        Diagnostics.Arrange("error number", errorNumber);
        Diagnostics.Arrange("expected text", expected);
        string actual = CRuntimeErrorNumbers.MacOS.Describe(errorNumber);
        Diagnostics.Act("described text", actual);

        Diagnostics.Assert("macOS description", expected, actual);
        Assert.AreEqual(expected, CRuntimeErrorNumbers.MacOS.Describe(errorNumber));
    }
}
