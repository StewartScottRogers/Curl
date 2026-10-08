using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Core.FileSystem;

/// <summary>
/// Drives <see cref="PhysicalFileSystem" /> against the real disk, in a fresh temporary
/// directory per test, or against the null device. None is <c>[TestCategory("Integration")]</c>:
/// the pin of curl 8.21.0's measured <c>file:///NUL</c> header date lives in
/// <c>Curl.Core.IntegrationTests</c>' <c>PhysicalFileSystemIntegrationTests</c>, and the fast
/// run must reach every line of <see cref="PhysicalFileSystem" /> for its coverage gate.
/// </summary>
[TestClass]
public sealed partial class PhysicalFileSystemTests
{
    private const UnixFileMode DefaultCreateMode = TransferContext.DefaultCreateFileMode;

    private const UnixFileMode Mode0600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public TestContext TestContext { get; set; } = null!;

    private static byte[] Content => Encoding.ASCII.GetBytes("Hello file");

    [TestMethod]
    public async Task OpenForReadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fileSystem = new PhysicalFileSystem();
        diagnostics.Arrange("path", "unused");
        diagnostics.Arrange("token", "cancelled");

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => fileSystem.OpenForReadAsync("unused", new CancellationToken(canceled: true)).AsTask());

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(OperationCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fileSystem = new PhysicalFileSystem();
        diagnostics.Arrange("path", "unused");
        diagnostics.Arrange("token", "cancelled");

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => fileSystem
                .OpenForWriteAsync("unused", FileWriteMode.Truncate, DefaultCreateMode, new CancellationToken(canceled: true))
                .AsTask());

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(OperationCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task OpenForReadAsync_ExistingFile_OpensASeekableHandleWithItsLengthAndTimestamp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("source.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        var expectedLastWriteTimeUtc = new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        diagnostics.Arrange("path", "source.txt");
        diagnostics.Bytes("file content", Content);

        var result = await new PhysicalFileSystem().OpenForReadAsync(path, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsNotNull(result.Content);
        await using var content = result.Content;
        diagnostics.Assert("can seek", true, content.CanSeek);
        Assert.IsTrue(content.CanSeek);
        diagnostics.Assert("length", (long)Content.Length, result.Length);
        Assert.AreEqual((long)Content.Length, result.Length);
        diagnostics.Assert("last write time", expectedLastWriteTimeUtc, result.LastWriteTimeUtc);
        Assert.AreEqual(expectedLastWriteTimeUtc, result.LastWriteTimeUtc);
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        diagnostics.Diff("content", Content, copy.ToArray());
        CollectionAssert.AreEqual(Content, copy.ToArray());
    }

    [TestMethod]
    public async Task OpenForReadAsync_MissingFile_IsNotFound()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "missing.txt");

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("missing.txt"), CancellationToken.None);

        ActResult(diagnostics, result);
        AssertFailed(FileAccessStatus.NotFound, result);
    }

    [TestMethod]
    public async Task OpenForReadAsync_MissingFile_CarriesTheFileNotFoundException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "missing.txt");

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("missing.txt"), CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("failure exception", nameof(FileNotFoundException), result.FailureException?.GetType().Name);
        Assert.IsInstanceOfType<FileNotFoundException>(result.FailureException);
    }

    [TestMethod]
    public async Task OpenForReadAsync_Directory_IsIsDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "the temporary directory itself");

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Path, CancellationToken.None);

        ActResult(diagnostics, result);
        AssertFailed(FileAccessStatus.IsDirectory, result);
    }

    [TestMethod]
    public async Task OpenForReadAsync_Directory_ReportsDirectoryLastWriteTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        var expected = new DateTimeOffset(Directory.GetLastWriteTimeUtc(directory.Path), TimeSpan.Zero);
        diagnostics.Arrange("path", "the temporary directory itself");
        diagnostics.Arrange("directory last write time", expected);

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Path, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.IsDirectory, result.Status);
        Assert.AreEqual(FileAccessStatus.IsDirectory, result.Status);
        diagnostics.Assert("last write time", expected, result.LastWriteTimeUtc);
        Assert.AreEqual(expected, result.LastWriteTimeUtc);
    }

    [TestMethod]
    public async Task OpenForReadAsync_MissingFile_ReportsNoLastWriteTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "missing.txt");

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("missing.txt"), CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.NotFound, result.Status);
        Assert.AreEqual(FileAccessStatus.NotFound, result.Status);
        diagnostics.Assert("last write time", null, result.LastWriteTimeUtc);
        Assert.IsNull(result.LastWriteTimeUtc);
    }

    // FileUrlPath forwards c|/Windows with its bar. Windows rejects the bar as an invalid
    // name character, an IOException; elsewhere it is a legal name whose parent "c|" is
    // missing.
    [TestMethod]
    public async Task OpenForReadAsync_BarInPlaceOfDriveColon_FailsWithoutThrowing()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        FileAccessStatus expected = OperatingSystem.IsWindows() ? FileAccessStatus.IoError : FileAccessStatus.NotFound;
        diagnostics.Arrange("path", "c|/Windows");

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("c|/Windows"), CancellationToken.None);

        ActResult(diagnostics, result);
        AssertFailed(expected, result);
    }

    // A malformed escape such as %GG reaches the file system as a literal percent sign,
    // which is a legal file name character everywhere, so a missing one is plain NotFound.
    [TestMethod]
    public async Task OpenForReadAsync_LiteralPercentInMissingName_IsNotFound()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "%GG.txt");

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("%GG.txt"), CancellationToken.None);

        ActResult(diagnostics, result);
        AssertFailed(FileAccessStatus.NotFound, result);
    }

    // On Windows the null device opens, but as a handle that cannot seek and has no length:
    // the shape file:///dev/stdin arrives in, which the handler must not seek.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDevice_OpensANonSeekableHandleOfLengthZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", "NUL");

        var result = await new PhysicalFileSystem().OpenForReadAsync("NUL", CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsNotNull(result.Content);
        await using var content = result.Content;
        diagnostics.Assert("can seek", false, content.CanSeek);
        Assert.IsFalse(content.CanSeek);
        diagnostics.Assert("length", 0L, result.Length);
        Assert.AreEqual(0L, result.Length);
    }

    // On Linux and macOS lseek(2) succeeds on /dev/null, so .NET reports it seekable, and its
    // fstat size is zero - the same size curl's fstat reads, so an offset past it still fails.
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public async Task OpenForReadAsync_UnixNullDevice_OpensASeekableHandleOfLengthZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", "/dev/null");

        var result = await new PhysicalFileSystem().OpenForReadAsync("/dev/null", CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsNotNull(result.Content);
        await using var content = result.Content;
        diagnostics.Assert("can seek", true, content.CanSeek);
        Assert.IsTrue(content.CanSeek);
        diagnostics.Assert("length", 0L, result.Length);
        Assert.AreEqual(0L, result.Length);
    }

    // Windows reports no last-write time for NUL; the Windows C runtime's fstat reports
    // zero, which curl 8.21.0 prints as the epoch.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDeviceReportingEpoch_ReportsTheUnixEpoch()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fileSystem = new PhysicalFileSystem(setsUnixCreateMode: false, reportsUnreadableTimestampAsEpoch: true);
        diagnostics.Arrange("path", "NUL");
        diagnostics.Arrange("reports unreadable timestamp as epoch", true);

        var result = await fileSystem.OpenForReadAsync("NUL", CancellationToken.None);

        ActResult(diagnostics, result);
        await result.Content!.DisposeAsync();
        diagnostics.Assert("last write time", DateTimeOffset.UnixEpoch, result.LastWriteTimeUtc);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, result.LastWriteTimeUtc);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDeviceNotReportingEpoch_ReportsNoTimestamp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fileSystem = new PhysicalFileSystem(setsUnixCreateMode: false, reportsUnreadableTimestampAsEpoch: false);
        diagnostics.Arrange("path", "NUL");
        diagnostics.Arrange("reports unreadable timestamp as epoch", false);

        var result = await fileSystem.OpenForReadAsync("NUL", CancellationToken.None);

        ActResult(diagnostics, result);
        await result.Content!.DisposeAsync();
        diagnostics.Assert("last write time", null, result.LastWriteTimeUtc);
        Assert.IsNull(result.LastWriteTimeUtc);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_DestinationDirectoryMissing_IsNotFound()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "missing/destination.txt");
        diagnostics.Arrange("write mode", FileWriteMode.Truncate);

        var result = await new PhysicalFileSystem()
            .OpenForWriteAsync(directory.Combine("missing/destination.txt"), FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        ActResult(diagnostics, result);
        AssertFailed(FileAccessStatus.NotFound, result);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_Directory_IsIsDirectory()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "the temporary directory itself");
        diagnostics.Arrange("write mode", FileWriteMode.Truncate);

        var result = await new PhysicalFileSystem()
            .OpenForWriteAsync(directory.Path, FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        ActResult(diagnostics, result);
        AssertFailed(FileAccessStatus.IsDirectory, result);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_TruncateOverExistingFile_ReplacesItsContent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("destination.txt");
        await System.IO.File.WriteAllTextAsync(path, "old content that is longer");
        diagnostics.Arrange("path", "destination.txt");
        diagnostics.Arrange("existing content", "old content that is longer");
        diagnostics.Arrange("write mode", FileWriteMode.Truncate);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        diagnostics.Assert("length", 0L, result.Length);
        Assert.AreEqual(0L, result.Length);
        await using (var content = result.Content!)
        {
            await content.WriteAsync(Content);
        }

        byte[] written = await System.IO.File.ReadAllBytesAsync(path);
        diagnostics.Diff("file content", Content, written);
        CollectionAssert.AreEqual(Content, written);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_TruncateWithNoFile_CreatesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("created.txt");
        diagnostics.Arrange("path", "created.txt");
        diagnostics.Arrange("write mode", FileWriteMode.Truncate);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        diagnostics.Assert("file exists", true, System.IO.File.Exists(path));
        Assert.IsTrue(System.IO.File.Exists(path));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_CreateNewOverExistingFile_IsAlreadyExistsAndKeepsItsBytes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("taken.txt");
        await System.IO.File.WriteAllTextAsync(path, "kept");
        diagnostics.Arrange("path", "taken.txt");
        diagnostics.Arrange("existing content", "kept");
        diagnostics.Arrange("write mode", FileWriteMode.CreateNew);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.CreateNew, DefaultCreateMode, CancellationToken.None);

        ActResult(diagnostics, result);
        AssertFailed(FileAccessStatus.AlreadyExists, result);
        string kept = await System.IO.File.ReadAllTextAsync(path);
        diagnostics.Assert("file content", "kept", kept);
        Assert.AreEqual("kept", kept);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_CreateNewWithNoFile_CreatesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("new.txt");
        diagnostics.Arrange("path", "new.txt");
        diagnostics.Arrange("write mode", FileWriteMode.CreateNew);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.CreateNew, DefaultCreateMode, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await using (var content = result.Content!)
        {
            await content.WriteAsync(Content);
        }

        byte[] written = await System.IO.File.ReadAllBytesAsync(path);
        diagnostics.Diff("file content", Content, written);
        CollectionAssert.AreEqual(Content, written);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_Append_PositionsAfterTheExistingContent()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("destination.txt");
        await System.IO.File.WriteAllTextAsync(path, "Hello");
        diagnostics.Arrange("path", "destination.txt");
        diagnostics.Arrange("existing content", "Hello");
        diagnostics.Arrange("write mode", FileWriteMode.Append);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Append, DefaultCreateMode, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        diagnostics.Assert("length", 5L, result.Length);
        Assert.AreEqual(5L, result.Length);
        await using (var content = result.Content!)
        {
            diagnostics.Assert("position", 5L, content.Position);
            Assert.AreEqual(5L, content.Position);
            await content.WriteAsync(Encoding.ASCII.GetBytes(" file"));
        }

        byte[] written = await System.IO.File.ReadAllBytesAsync(path);
        diagnostics.Diff("file content", Content, written);
        CollectionAssert.AreEqual(Content, written);
    }

    // FileStreamOptions.UnixCreateMode throws on Windows, so reaching it there proves the
    // create mode is handed to the operating system on the POSIX path; nothing is opened.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForWriteAsync_SettingUnixCreateModeOnWindows_ReachesUnixCreateMode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fileSystem = new PhysicalFileSystem(setsUnixCreateMode: true);
        diagnostics.Arrange("sets unix create mode", true);
        diagnostics.Arrange("create mode", Mode0600);

        var exception = await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
            () => fileSystem.OpenForWriteAsync("unused", FileWriteMode.Truncate, Mode0600, CancellationToken.None).AsTask());

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(PlatformNotSupportedException), exception.GetType().Name);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForWriteAsync_CreateModeOnWindows_IsIgnored()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("created.txt");
        diagnostics.Arrange("path", "created.txt");
        diagnostics.Arrange("create mode", UnixFileMode.None);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, UnixFileMode.None, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        diagnostics.Assert("file exists", true, System.IO.File.Exists(path));
        Assert.IsTrue(System.IO.File.Exists(path));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    [UnsupportedOSPlatform("windows")]
    public async Task OpenForWriteAsync_CreateModeOnPosix_IsTheModeOfTheCreatedFile()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("created.txt");
        diagnostics.Arrange("path", "created.txt");
        diagnostics.Arrange("create mode", Mode0600);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, Mode0600, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        UnixFileMode mode = System.IO.File.GetUnixFileMode(path);
        diagnostics.Assert("file mode", Mode0600, mode);
        Assert.AreEqual(Mode0600, mode);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    [UnsupportedOSPlatform("windows")]
    public async Task OpenForWriteAsync_CreateModeOnPosixOverExistingFile_KeepsItsMode()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("existing.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        System.IO.File.SetUnixFileMode(path, DefaultCreateMode);
        diagnostics.Arrange("path", "existing.txt");
        diagnostics.Arrange("existing mode", DefaultCreateMode);
        diagnostics.Arrange("create mode", Mode0600);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, Mode0600, CancellationToken.None);

        ActResult(diagnostics, result);
        diagnostics.Assert("status", FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        UnixFileMode mode = System.IO.File.GetUnixFileMode(path);
        diagnostics.Assert("file mode", DefaultCreateMode, mode);
        Assert.AreEqual(DefaultCreateMode, mode);
    }

    [TestMethod]
    public async Task TrySetLastWriteUnixSeconds_ExistingFile_SetsItsLastWriteTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        var lastWriteTimeUtc = new DateTimeOffset(2020, 1, 2, 10, 4, 5, TimeSpan.Zero);
        diagnostics.Arrange("path", "out.txt");
        diagnostics.Arrange("unix seconds", lastWriteTimeUtc.ToUnixTimeSeconds());

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, lastWriteTimeUtc.ToUnixTimeSeconds(), out int errorCode, out _);

        diagnostics.Act("set", set);
        diagnostics.Act("error code", errorCode);
        diagnostics.Assert("set", true, set);
        Assert.IsTrue(set);
        diagnostics.Assert("error code", 0, errorCode);
        Assert.AreEqual(0, errorCode);
        DateTime lastWrite = System.IO.File.GetLastWriteTimeUtc(path);
        diagnostics.Assert("last write time", lastWriteTimeUtc.UtcDateTime, lastWrite);
        Assert.AreEqual(lastWriteTimeUtc.UtcDateTime, lastWrite);
    }

    [TestMethod]
    public void TrySetLastWriteUnixSeconds_MissingFile_ReturnsFalseWithErrorFileNotFound()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "missing.txt");
        diagnostics.Arrange("unix seconds", 0);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(directory.Combine("missing.txt"), 0, out int errorCode, out _);

        diagnostics.Act("set", set);
        diagnostics.Act("error code", errorCode);
        diagnostics.Assert("set", false, set);
        Assert.IsFalse(set);
        diagnostics.Assert("error code", 2, errorCode);
        Assert.AreEqual(2, errorCode);
    }

    /// <summary>
    /// 30827-12-31T23:59:59Z, the time curl 8.21.0 caps an <c>-R</c> time to on Windows, is
    /// past <see cref="DateTime" />; it reaches the file through <c>SetFileTime</c> and reads
    /// back through the raw Win32 file time (BL-1425).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    public async Task TrySetLastWriteUnixSeconds_OnWindowsYear30827_SetsTheRawFileTime()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        diagnostics.Arrange("path", "out.txt");
        diagnostics.Arrange("unix seconds", 910670515199);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, 910670515199, out int errorCode, out _);

        diagnostics.Act("set", set);
        diagnostics.Act("error code", errorCode);
        diagnostics.Assert("set", true, set);
        Assert.IsTrue(set);
        diagnostics.Assert("error code", 0, errorCode);
        Assert.AreEqual(0, errorCode);
        long readBack = (ReadWin32LastWriteFileTime(path) / 10_000_000) - 11_644_473_600;
        diagnostics.Assert("unix seconds read back", 910670515199, readBack);
        Assert.AreEqual(910670515199, readBack);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TrySetLastWriteUnixSeconds_OnWindowsMissingFilePastYear9999_ReturnsFalseWithErrorFileNotFound()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "missing.txt");
        diagnostics.Arrange("unix seconds", 910670515199);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(directory.Combine("missing.txt"), 910670515199, out int errorCode, out _);

        diagnostics.Act("set", set);
        diagnostics.Act("error code", errorCode);
        diagnostics.Assert("set", false, set);
        Assert.IsFalse(set);
        diagnostics.Assert("error code", 2, errorCode);
        Assert.AreEqual(2, errorCode);
    }

    /// <summary>
    /// On Windows a missing file fails at the open, curl's <c>CreateFile</c> step, and a time
    /// before 1601 that <c>SetFileTime</c> refuses on an open file fails at the stamp, so the two
    /// are reported apart (BL-1453).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TrySetLastWriteUnixSeconds_OnWindowsOpenOrStampFailure_ReportsWhichStepFailed()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        const long Before1601 = -11644473601;
        diagnostics.Arrange("missing path", "missing.txt");
        diagnostics.Arrange("existing path", "out.txt");
        diagnostics.Arrange("stamp unix seconds", Before1601);

        bool opened = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(directory.Combine("missing.txt"), 0, out int openErrorCode, out FileTimeFailedStep openStep);
        bool stamped = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, Before1601, out int stampErrorCode, out FileTimeFailedStep stampStep);

        diagnostics.Act("missing file set", opened);
        diagnostics.Act("missing file step", openStep);
        diagnostics.Act("missing file error code", openErrorCode);
        diagnostics.Act("refused time set", stamped);
        diagnostics.Act("refused time step", stampStep);
        diagnostics.Act("refused time error code", stampErrorCode);
        diagnostics.Assert("missing file set", false, opened);
        Assert.IsFalse(opened);
        diagnostics.Assert("missing file step", FileTimeFailedStep.Open, openStep);
        Assert.AreEqual(FileTimeFailedStep.Open, openStep);
        diagnostics.Assert("missing file error code", 2, openErrorCode);
        Assert.AreEqual(2, openErrorCode);
        diagnostics.Assert("refused time set", false, stamped);
        Assert.IsFalse(stamped);
        diagnostics.Assert("refused time step", FileTimeFailedStep.SetTime, stampStep);
        Assert.AreEqual(FileTimeFailedStep.SetTime, stampStep);
        diagnostics.Assert("refused time error code", 87, stampErrorCode);
        Assert.AreEqual(87, stampErrorCode);
    }

    [TestMethod]
    public async Task TrySetLastWriteUnixSeconds_TimeSet_ReportsNoFailedStep()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        diagnostics.Arrange("path", "out.txt");
        diagnostics.Arrange("unix seconds", 1577959445);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, 1577959445, out _, out FileTimeFailedStep failedStep);

        diagnostics.Act("set", set);
        diagnostics.Act("failed step", failedStep);
        diagnostics.Assert("set", true, set);
        Assert.IsTrue(set);
        diagnostics.Assert("failed step", FileTimeFailedStep.None, failedStep);
        Assert.AreEqual(FileTimeFailedStep.None, failedStep);
    }

    /// <summary>
    /// A time past year 9999, or before year 1, is set or refused by the operating system and
    /// reported either way, never thrown: <c>utimes</c> takes either off Windows, and Windows'
    /// <c>SetFileTime</c> refuses a time before 1601.
    /// </summary>
    /// <param name="unixSeconds">The time to set.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [TestMethod]
    [DataRow(1200110860800L)]
    [DataRow(-62135596801L)]
    public async Task TrySetLastWriteUnixSeconds_OutsideDateTime_ReportsTheOutcomeWithoutThrowing(long unixSeconds)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        diagnostics.Arrange("path", "out.txt");
        diagnostics.Arrange("unix seconds", unixSeconds);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, unixSeconds, out int errorCode, out _);

        diagnostics.Act("set", set);
        diagnostics.Act("error code", errorCode);
        diagnostics.Assert("set is error code zero", set, errorCode == 0);
        Assert.AreEqual(set, errorCode == 0);
    }

    [SupportedOSPlatform("windows")]
    private static long ReadWin32LastWriteFileTime(string path)
    {
        using Microsoft.Win32.SafeHandles.SafeFileHandle file = System.IO.File.OpenHandle(path);
        _ = GetFileTime(file, IntPtr.Zero, IntPtr.Zero, out long lastWrite);

        return lastWrite;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileTime(
        Microsoft.Win32.SafeHandles.SafeFileHandle file,
        IntPtr creationTime,
        IntPtr lastAccessTime,
        out long lastWriteTime);

    private static void ActResult(TestDiagnostics diagnostics, FileOpenResult result)
    {
        diagnostics.Act("status", result.Status);
        diagnostics.Act("content", result.Content is null ? "none" : "open");
        diagnostics.Act("length", result.Length);
        diagnostics.Act("failure exception", result.FailureException?.GetType().Name ?? "none");
    }

    private void AssertFailed(FileAccessStatus expected, FileOpenResult result)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Assert("status", expected, result.Status);
        Assert.AreEqual(expected, result.Status);
        diagnostics.Assert("content", "none", result.Content is null ? "none" : "open");
        Assert.IsNull(result.Content);
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var fileSystem = new PhysicalFileSystem();
        diagnostics.Arrange("path", "unused");
        diagnostics.Arrange("token", "cancelled");

        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => fileSystem.ListEntryNamesAsync("unused", new CancellationToken(canceled: true)).AsTask());

        diagnostics.Act("exception", exception.GetType().Name);
        diagnostics.Assert("exception", nameof(OperationCanceledException), exception.GetType().Name);
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_Directory_ReturnsEveryEntryNameIncludingDotNamesAndSubdirectories()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        await System.IO.File.WriteAllBytesAsync(directory.Combine("a.txt"), Content);
        await System.IO.File.WriteAllBytesAsync(directory.Combine(".hidden"), Content);
        Directory.CreateDirectory(directory.Combine("sub"));
        diagnostics.Arrange("entries", "a.txt, .hidden, sub/");

        var names = await new PhysicalFileSystem().ListEntryNamesAsync(directory.Path, CancellationToken.None);

        diagnostics.Act("names", names is null ? "null" : string.Join(", ", names.Order(StringComparer.Ordinal)));
        Assert.IsNotNull(names);
        diagnostics.Assert("names", ".hidden, a.txt, sub", string.Join(", ", names.Order(StringComparer.Ordinal)));
        CollectionAssert.AreEquivalent(new[] { "a.txt", ".hidden", "sub" }, names.ToArray());
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_MissingDirectory_ReturnsNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        diagnostics.Arrange("path", "missing");

        var names = await new PhysicalFileSystem().ListEntryNamesAsync(directory.Combine("missing"), CancellationToken.None);

        diagnostics.Act("names", names is null ? "null" : string.Join(", ", names));
        diagnostics.Assert("names", "null", names is null ? "null" : string.Join(", ", names));
        Assert.IsNull(names);
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_RegularFile_ReturnsNull()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("source.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        diagnostics.Arrange("path", "source.txt (a regular file)");

        var names = await new PhysicalFileSystem().ListEntryNamesAsync(path, CancellationToken.None);

        diagnostics.Act("names", names is null ? "null" : string.Join(", ", names));
        diagnostics.Assert("names", "null", names is null ? "null" : string.Join(", ", names));
        Assert.IsNull(names);
    }

    /// <summary>
    /// A directory under the system temporary path, removed with everything in it on
    /// disposal.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory("curl-physicalfilesystem-").FullName;
        }

        public string Path { get; }

        public string Combine(string relativePath) => System.IO.Path.Combine(Path, relativePath);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
