using Curl.Protocol.Abstractions;

namespace Curl.Core.FileSystem;

/// <summary>
/// Pins which exceptions <see cref="PhysicalFileSystem" /> absorbs and the
/// <see cref="FileAccessStatus" /> each becomes. Nothing here touches a disk.
/// </summary>
[TestClass]
public sealed class FileOpenFailureTests
{
    public static IEnumerable<object[]> AbsorbedExceptions =>
    [
        [new ArgumentException()],
        [new ArgumentNullException()],
        [new NotSupportedException()],
        [new PathTooLongException()],
        [new DirectoryNotFoundException()],
        [new FileNotFoundException()],
        [new UnauthorizedAccessException()],
        [new IOException()],
    ];

    [TestMethod]
    [DynamicData(nameof(AbsorbedExceptions))]
    public void IsOpenFailure_ExceptionAdr0002Names_IsTrue(Exception exception)
    {
        Assert.IsTrue(FileOpenFailure.IsOpenFailure(exception));
    }

    [TestMethod]
    public void IsOpenFailure_OperationCanceledException_IsFalse()
    {
        Assert.IsFalse(FileOpenFailure.IsOpenFailure(new OperationCanceledException()));
    }

    [TestMethod]
    public void IsOpenFailure_InvalidOperationException_IsFalse()
    {
        Assert.IsFalse(FileOpenFailure.IsOpenFailure(new InvalidOperationException()));
    }

    [TestMethod]
    public void StatusFor_PathIsDirectory_IsIsDirectoryWhateverTheException()
    {
        var status = FileOpenFailure.StatusFor(new UnauthorizedAccessException(), pathIsDirectory: true);

        Assert.AreEqual(FileAccessStatus.IsDirectory, status);
    }

    [TestMethod]
    public void StatusFor_FileNotFoundException_IsNotFound()
    {
        var status = FileOpenFailure.StatusFor(new FileNotFoundException(), pathIsDirectory: false);

        Assert.AreEqual(FileAccessStatus.NotFound, status);
    }

    [TestMethod]
    public void StatusFor_DirectoryNotFoundException_IsNotFound()
    {
        var status = FileOpenFailure.StatusFor(new DirectoryNotFoundException(), pathIsDirectory: false);

        Assert.AreEqual(FileAccessStatus.NotFound, status);
    }

    [TestMethod]
    public void StatusFor_UnauthorizedAccessException_IsAccessDenied()
    {
        var status = FileOpenFailure.StatusFor(new UnauthorizedAccessException(), pathIsDirectory: false);

        Assert.AreEqual(FileAccessStatus.AccessDenied, status);
    }

    [TestMethod]
    public void StatusFor_PathTooLongException_IsIoError()
    {
        var status = FileOpenFailure.StatusFor(new PathTooLongException(), pathIsDirectory: false);

        Assert.AreEqual(FileAccessStatus.IoError, status);
    }

    [TestMethod]
    public void StatusFor_ArgumentException_IsIoError()
    {
        var status = FileOpenFailure.StatusFor(new ArgumentException(), pathIsDirectory: false);

        Assert.AreEqual(FileAccessStatus.IoError, status);
    }
}
