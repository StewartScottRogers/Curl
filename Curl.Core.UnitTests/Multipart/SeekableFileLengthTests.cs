using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the length curl 8.21.0's <c>stat</c> declares for a <c>-F</c> file that can seek: its
/// length on Windows, and none for a device such as <c>/dev/null</c> on Linux and macOS, which
/// the OpenSSL build sent chunked (BL-445 Notes).
/// </summary>
[TestClass]
public sealed class SeekableFileLengthTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ForPlatform_OnWindows_DeclaresTheLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "Windows");
        diagnostics.Arrange("path and length", "/dev/null, 0");

        long? length = SeekableFileLength.ForPlatform(runsOnWindows: true)("/dev/null", 0);

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", 0, length);
        Assert.AreEqual(0, SeekableFileLength.ForPlatform(runsOnWindows: true)("/dev/null", 0));
    }

    [TestMethod]
    public void ForPlatform_OffWindows_DeclaresNoLengthForADevice()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", "not Windows");
        diagnostics.Arrange("path and length", "/dev/null, 0");

        long? length = SeekableFileLength.ForPlatform(runsOnWindows: false)("/dev/null", 0);

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", null, length);
        Assert.IsNull(SeekableFileLength.ForPlatform(runsOnWindows: false)("/dev/null", 0));
    }

    [TestMethod]
    public void AsWindowsStatReportsIt_IsTheLength()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path and length", "f.txt, 7");

        long? length = SeekableFileLength.AsWindowsStatReportsIt("f.txt", 7);

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", 7, length);
        Assert.AreEqual(7, SeekableFileLength.AsWindowsStatReportsIt("f.txt", 7));
    }

    [TestMethod]
    [DataRow("/dev/null")]
    [DataRow("/dev/zero")]
    [DataRow("/dev/sda")]
    public void AsPosixStatReportsIt_Device_IsNone(string path)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);

        long? length = SeekableFileLength.AsPosixStatReportsIt(path, 0);

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", null, length);
        Assert.IsNull(SeekableFileLength.AsPosixStatReportsIt(path, 0));
    }

    [TestMethod]
    [DataRow("f.txt")]
    [DataRow("/tmp/f")]
    [DataRow("/dev")]
    [DataRow("/devices/f")]
    [DataRow("/dev/shm/f")]
    [DataRow("/dev/fd/0")]
    [DataRow("/dev/stdin")]
    [DataRow("/dev/stdout")]
    [DataRow("/dev/stderr")]
    public void AsPosixStatReportsIt_RegularFile_IsTheLength(string path)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", path);
        diagnostics.Arrange("length", 5);

        long? length = SeekableFileLength.AsPosixStatReportsIt(path, 5);

        diagnostics.Act("declared length", length?.ToString() ?? "null");
        diagnostics.Assert("declared length", 5, length);
        Assert.AreEqual(5, SeekableFileLength.AsPosixStatReportsIt(path, 5));
    }
}
