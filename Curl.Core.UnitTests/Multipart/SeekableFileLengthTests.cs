namespace Curl.Core.Multipart;

/// <summary>
/// Pins the length curl 8.21.0's <c>stat</c> declares for a <c>-F</c> file that can seek: its
/// length on Windows, and none for a device such as <c>/dev/null</c> on Linux and macOS, which
/// the OpenSSL build sent chunked (BL-445 Notes).
/// </summary>
[TestClass]
public sealed class SeekableFileLengthTests
{
    [TestMethod]
    public void ForPlatform_OnWindows_DeclaresTheLength() =>
        Assert.AreEqual(0, SeekableFileLength.ForPlatform(runsOnWindows: true)("/dev/null", 0));

    [TestMethod]
    public void ForPlatform_OffWindows_DeclaresNoLengthForADevice() =>
        Assert.IsNull(SeekableFileLength.ForPlatform(runsOnWindows: false)("/dev/null", 0));

    [TestMethod]
    public void AsWindowsStatReportsIt_IsTheLength() =>
        Assert.AreEqual(7, SeekableFileLength.AsWindowsStatReportsIt("f.txt", 7));

    [TestMethod]
    [DataRow("/dev/null")]
    [DataRow("/dev/zero")]
    [DataRow("/dev/sda")]
    public void AsPosixStatReportsIt_Device_IsNone(string path) =>
        Assert.IsNull(SeekableFileLength.AsPosixStatReportsIt(path, 0));

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
    public void AsPosixStatReportsIt_RegularFile_IsTheLength(string path) =>
        Assert.AreEqual(5, SeekableFileLength.AsPosixStatReportsIt(path, 5));
}
