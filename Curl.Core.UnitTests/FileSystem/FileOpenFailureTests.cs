using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.FileSystem;

/// <summary>
/// Pins which exceptions <see cref="PhysicalFileSystem" /> absorbs and the
/// <see cref="FileAccessStatus" /> each becomes. Nothing here touches a disk.
/// </summary>
[TestClass]
public sealed class FileOpenFailureTests
{
    public TestContext TestContext { get; set; } = null!;

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
        var isOpenFailure = RunIsOpenFailure(exception);

        TestDiagnostics.For(TestContext).Assert("is open failure", true, isOpenFailure);
        Assert.IsTrue(isOpenFailure);
    }

    [TestMethod]
    public void IsOpenFailure_OperationCanceledException_IsFalse()
    {
        var isOpenFailure = RunIsOpenFailure(new OperationCanceledException());

        TestDiagnostics.For(TestContext).Assert("is open failure", false, isOpenFailure);
        Assert.IsFalse(isOpenFailure);
    }

    [TestMethod]
    public void IsOpenFailure_InvalidOperationException_IsFalse()
    {
        var isOpenFailure = RunIsOpenFailure(new InvalidOperationException());

        TestDiagnostics.For(TestContext).Assert("is open failure", false, isOpenFailure);
        Assert.IsFalse(isOpenFailure);
    }

    [TestMethod]
    public void StatusFor_PathIsDirectory_IsIsDirectoryWhateverTheException()
    {
        var status = RunStatusFor(new UnauthorizedAccessException(), pathIsDirectory: true);

        TestDiagnostics.For(TestContext).Assert("status", FileAccessStatus.IsDirectory, status);
        Assert.AreEqual(FileAccessStatus.IsDirectory, status);
    }

    [TestMethod]
    public void StatusFor_FileNotFoundException_IsNotFound()
    {
        var status = RunStatusFor(new FileNotFoundException(), pathIsDirectory: false);

        TestDiagnostics.For(TestContext).Assert("status", FileAccessStatus.NotFound, status);
        Assert.AreEqual(FileAccessStatus.NotFound, status);
    }

    [TestMethod]
    public void StatusFor_DirectoryNotFoundException_IsNotFound()
    {
        var status = RunStatusFor(new DirectoryNotFoundException(), pathIsDirectory: false);

        TestDiagnostics.For(TestContext).Assert("status", FileAccessStatus.NotFound, status);
        Assert.AreEqual(FileAccessStatus.NotFound, status);
    }

    [TestMethod]
    public void StatusFor_UnauthorizedAccessException_IsAccessDenied()
    {
        var status = RunStatusFor(new UnauthorizedAccessException(), pathIsDirectory: false);

        TestDiagnostics.For(TestContext).Assert("status", FileAccessStatus.AccessDenied, status);
        Assert.AreEqual(FileAccessStatus.AccessDenied, status);
    }

    [TestMethod]
    public void StatusFor_PathTooLongException_IsIoError()
    {
        var status = RunStatusFor(new PathTooLongException(), pathIsDirectory: false);

        TestDiagnostics.For(TestContext).Assert("status", FileAccessStatus.IoError, status);
        Assert.AreEqual(FileAccessStatus.IoError, status);
    }

    [TestMethod]
    public void StatusFor_ArgumentException_IsIoError()
    {
        var status = RunStatusFor(new ArgumentException(), pathIsDirectory: false);

        TestDiagnostics.For(TestContext).Assert("status", FileAccessStatus.IoError, status);
        Assert.AreEqual(FileAccessStatus.IoError, status);
    }

    [TestMethod]
    public void StatusFor_FileExistsIOException_IsAlreadyExists()
    {
        var status = RunStatusFor(new IOException("exists", unchecked((int)0x80070050)), pathIsDirectory: false);

        TestDiagnostics.For(TestContext).Assert("status", FileAccessStatus.AlreadyExists, status);
        Assert.AreEqual(FileAccessStatus.AlreadyExists, status);
    }

    [TestMethod]
    [DataRow(unchecked((int)0x80070050))]
    [DataRow(unchecked((int)0x800700B7))]
    [DataRow(17)]
    public void IsFileExists_IOExceptionWithAnExistsCode_IsTrue(int hresult)
    {
        var isFileExists = RunIsFileExists(new IOException("exists", hresult));

        TestDiagnostics.For(TestContext).Assert("is file exists", true, isFileExists);
        Assert.IsTrue(isFileExists);
    }

    [TestMethod]
    public void IsFileExists_IOExceptionWithAnotherCode_IsFalse()
    {
        var isFileExists = RunIsFileExists(new IOException("other", unchecked((int)0x80070020)));

        TestDiagnostics.For(TestContext).Assert("is file exists", false, isFileExists);
        Assert.IsFalse(isFileExists);
    }

    [TestMethod]
    public void IsFileExists_OtherExceptionWithAnExistsCode_IsFalse()
    {
        var isFileExists = RunIsFileExists(new InvalidOperationException("exists") { HResult = 17 });

        TestDiagnostics.For(TestContext).Assert("is file exists", false, isFileExists);
        Assert.IsFalse(isFileExists);
    }

    [TestMethod]
    public void Win32ErrorCodeOf_FileNotFoundException_IsErrorFileNotFound()
    {
        var code = RunWin32ErrorCodeOf(new FileNotFoundException());

        TestDiagnostics.For(TestContext).Assert("win32 error code", 2, code);
        Assert.AreEqual(2, code);
    }

    [TestMethod]
    public void Win32ErrorCodeOf_UnauthorizedAccessException_IsErrorAccessDenied()
    {
        var code = RunWin32ErrorCodeOf(new UnauthorizedAccessException());

        TestDiagnostics.For(TestContext).Assert("win32 error code", 5, code);
        Assert.AreEqual(5, code);
    }

    [TestMethod]
    public void Win32ErrorCodeOf_ExceptionWithoutWin32HResult_IsItsHResult()
    {
        var exception = new NotSupportedException();

        var code = RunWin32ErrorCodeOf(exception);

        TestDiagnostics.For(TestContext).Assert("win32 error code", exception.HResult, code);
        Assert.AreEqual(exception.HResult, code);
    }

    private bool RunIsOpenFailure(Exception exception)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ArrangeException(diagnostics, exception);
        var isOpenFailure = FileOpenFailure.IsOpenFailure(exception);
        diagnostics.Act("is open failure", isOpenFailure);
        return isOpenFailure;
    }

    private FileAccessStatus RunStatusFor(Exception exception, bool pathIsDirectory)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ArrangeException(diagnostics, exception);
        diagnostics.Arrange("path is directory", pathIsDirectory);
        var status = FileOpenFailure.StatusFor(exception, pathIsDirectory);
        diagnostics.Act("status", status);
        return status;
    }

    private bool RunIsFileExists(Exception exception)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ArrangeException(diagnostics, exception);
        var isFileExists = FileOpenFailure.IsFileExists(exception);
        diagnostics.Act("is file exists", isFileExists);
        return isFileExists;
    }

    private int RunWin32ErrorCodeOf(Exception exception)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ArrangeException(diagnostics, exception);
        var code = FileOpenFailure.Win32ErrorCodeOf(exception);
        diagnostics.Act("win32 error code", $"0x{code:X8}");
        return code;
    }

    private static void ArrangeException(TestDiagnostics diagnostics, Exception exception) =>
        diagnostics.Arrange("exception", $"{exception.GetType().Name} (HResult 0x{exception.HResult:X8})");
}
