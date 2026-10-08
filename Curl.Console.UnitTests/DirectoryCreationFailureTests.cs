using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins curl 8.21.0's <c>--create-dirs</c> messages by <c>errno</c>, upstream
/// <c>src/tool_dirhie.c</c>'s <c>show_dir_errno</c> (BL-1433), on each platform's numbers.
/// </summary>
[TestClass]
public sealed class DirectoryCreationFailureTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("linux", 36, "curl: The directory name d/e is too long")]
    [DataRow("macos", 63, "curl: The directory name d/e is too long")]
    [DataRow("windows", 38, "curl: The directory name d/e is too long")]
    [DataRow("linux", 30, "curl: d/e resides on a read-only file system")]
    [DataRow("macos", 30, "curl: d/e resides on a read-only file system")]
    [DataRow("linux", 28, "curl: No space left on the file system that would contain the directory d/e")]
    [DataRow("windows", 28, "curl: No space left on the file system that would contain the directory d/e")]
    [DataRow("linux", 122, "curl: Cannot create directory d/e because you exceeded your quota")]
    [DataRow("macos", 69, "curl: Cannot create directory d/e because you exceeded your quota")]
    [DataRow("linux", 2, "curl: Error creating directory d/e")]
    [DataRow("macos", 122, "curl: Error creating directory d/e")]
    [DataRow("windows", 122, "curl: Error creating directory d/e")]
    public void Message_GivesCurlsTextForTheErrno(string platform, int errorNumber, string expected)
    {
        Diagnostics.Arrange("platform", platform);
        Diagnostics.Arrange("errno", errorNumber);
        Diagnostics.Arrange("directory", "d/e");

        string message = new DirectoryCreationFailure("d/e", errorNumber).Message(NumbersOf(platform));
        Diagnostics.Act("message", message);

        Diagnostics.Assert("message", expected, message);
        Assert.AreEqual(expected, new DirectoryCreationFailure("d/e", errorNumber).Message(NumbersOf(platform)));
    }

    private static CRuntimeErrorNumbers NumbersOf(string platform) => platform switch
    {
        "windows" => CRuntimeErrorNumbers.Windows,
        "macos" => CRuntimeErrorNumbers.MacOS,
        _ => CRuntimeErrorNumbers.Linux,
    };
}
