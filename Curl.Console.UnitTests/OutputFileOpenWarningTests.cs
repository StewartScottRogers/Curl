using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins the <c>-o</c> open-failure warning against curl 8.21.0 on Windows, measured on
/// 2026-09-26: the line's wording, and the <c>strerror</c> reason for each
/// <see cref="FileAccessStatus" />.
/// </summary>
[TestClass]
public sealed class OutputFileOpenWarningTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void For_MissingParentDirectory_MatchesCurlsLine()
    {
        Diagnostics.Arrange("path", "Z:/nonexist/x");
        Diagnostics.Arrange("status", FileAccessStatus.NotFound);

        string line = OutputFileOpenWarning.For("Z:/nonexist/x", FileAccessStatus.NotFound);
        Diagnostics.Act("warning line", line);

        Diagnostics.Diff("warning line", "Warning: Failed to open the file Z:/nonexist/x: No such file or directory", line);
        Assert.AreEqual("Warning: Failed to open the file Z:/nonexist/x: No such file or directory", line);
    }

    [TestMethod]
    [DataRow(FileAccessStatus.NotFound, "No such file or directory")]
    [DataRow(FileAccessStatus.AccessDenied, "Permission denied")]
    [DataRow(FileAccessStatus.IsDirectory, "Permission denied")]
    [DataRow(FileAccessStatus.IoError, "Invalid argument")]
    [DataRow(FileAccessStatus.AlreadyExists, "File exists")]
    [DataRow(FileAccessStatus.Ok, "Invalid argument")]
    [DataRow((FileAccessStatus)99, "Invalid argument")]
    public void ReasonFor_EachStatus_IsCurlsStrerrorText(FileAccessStatus status, string expected)
    {
        Diagnostics.Arrange("status", (int)status);

        string actual = OutputFileOpenWarning.ReasonFor(status);
        Diagnostics.Act("reason", actual);

        Diagnostics.Diff("reason", expected, actual);
        Assert.AreEqual(expected, OutputFileOpenWarning.ReasonFor(status));
    }
}
