using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Core.FileSystem;

/// <summary>
/// Drives <see cref="PhysicalFileSystem" /> against the real disk, in a fresh temporary
/// directory per test, or against the null device. Only the pin of curl 8.21.0's measured
/// <c>file:///NUL</c> header date is <c>[TestCategory("Integration")]</c>; the rest need no
/// network, and the fast run must reach every line of <see cref="PhysicalFileSystem" />
/// for its coverage gate.
/// </summary>
[TestClass]
public sealed partial class PhysicalFileSystemTests
{
    private const UnixFileMode DefaultCreateMode = TransferContext.DefaultCreateFileMode;

    private const UnixFileMode Mode0600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static byte[] Content => Encoding.ASCII.GetBytes("Hello file");

    [TestMethod]
    public async Task OpenForReadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var fileSystem = new PhysicalFileSystem();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => fileSystem.OpenForReadAsync("unused", new CancellationToken(canceled: true)).AsTask());
    }

    [TestMethod]
    public async Task OpenForWriteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var fileSystem = new PhysicalFileSystem();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => fileSystem
                .OpenForWriteAsync("unused", FileWriteMode.Truncate, DefaultCreateMode, new CancellationToken(canceled: true))
                .AsTask());
    }

    [TestMethod]
    public async Task OpenForReadAsync_ExistingFile_OpensASeekableHandleWithItsLengthAndTimestamp()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("source.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        var expectedLastWriteTimeUtc = new DateTimeOffset(System.IO.File.GetLastWriteTimeUtc(path), TimeSpan.Zero);

        var result = await new PhysicalFileSystem().OpenForReadAsync(path, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsNotNull(result.Content);
        await using var content = result.Content;
        Assert.IsTrue(content.CanSeek);
        Assert.AreEqual((long)Content.Length, result.Length);
        Assert.AreEqual(expectedLastWriteTimeUtc, result.LastWriteTimeUtc);
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        CollectionAssert.AreEqual(Content, copy.ToArray());
    }

    [TestMethod]
    public async Task OpenForReadAsync_MissingFile_IsNotFound()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("missing.txt"), CancellationToken.None);

        AssertFailed(FileAccessStatus.NotFound, result);
    }

    [TestMethod]
    public async Task OpenForReadAsync_MissingFile_CarriesTheFileNotFoundException()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("missing.txt"), CancellationToken.None);

        Assert.IsInstanceOfType<FileNotFoundException>(result.FailureException);
    }

    [TestMethod]
    public async Task OpenForReadAsync_Directory_IsIsDirectory()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Path, CancellationToken.None);

        AssertFailed(FileAccessStatus.IsDirectory, result);
    }

    [TestMethod]
    public async Task OpenForReadAsync_Directory_ReportsDirectoryLastWriteTime()
    {
        using var directory = new TemporaryDirectory();
        var expected = new DateTimeOffset(Directory.GetLastWriteTimeUtc(directory.Path), TimeSpan.Zero);

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Path, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.IsDirectory, result.Status);
        Assert.AreEqual(expected, result.LastWriteTimeUtc);
    }

    [TestMethod]
    public async Task OpenForReadAsync_MissingFile_ReportsNoLastWriteTime()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("missing.txt"), CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.NotFound, result.Status);
        Assert.IsNull(result.LastWriteTimeUtc);
    }

    // FileUrlPath forwards c|/Windows with its bar. Windows rejects the bar as an invalid
    // name character, an IOException; elsewhere it is a legal name whose parent "c|" is
    // missing.
    [TestMethod]
    public async Task OpenForReadAsync_BarInPlaceOfDriveColon_FailsWithoutThrowing()
    {
        using var directory = new TemporaryDirectory();
        FileAccessStatus expected = OperatingSystem.IsWindows() ? FileAccessStatus.IoError : FileAccessStatus.NotFound;

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("c|/Windows"), CancellationToken.None);

        AssertFailed(expected, result);
    }

    // A malformed escape such as %GG reaches the file system as a literal percent sign,
    // which is a legal file name character everywhere, so a missing one is plain NotFound.
    [TestMethod]
    public async Task OpenForReadAsync_LiteralPercentInMissingName_IsNotFound()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("%GG.txt"), CancellationToken.None);

        AssertFailed(FileAccessStatus.NotFound, result);
    }

    // On Windows the null device opens, but as a handle that cannot seek and has no length:
    // the shape file:///dev/stdin arrives in, which the handler must not seek.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDevice_OpensANonSeekableHandleOfLengthZero()
    {
        var result = await new PhysicalFileSystem().OpenForReadAsync("NUL", CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsNotNull(result.Content);
        await using var content = result.Content;
        Assert.IsFalse(content.CanSeek);
        Assert.AreEqual(0L, result.Length);
    }

    // On Linux and macOS lseek(2) succeeds on /dev/null, so .NET reports it seekable, and its
    // fstat size is zero - the same size curl's fstat reads, so an offset past it still fails.
    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    public async Task OpenForReadAsync_UnixNullDevice_OpensASeekableHandleOfLengthZero()
    {
        var result = await new PhysicalFileSystem().OpenForReadAsync("/dev/null", CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsNotNull(result.Content);
        await using var content = result.Content;
        Assert.IsTrue(content.CanSeek);
        Assert.AreEqual(0L, result.Length);
    }

    // Windows reports no last-write time for NUL; the Windows C runtime's fstat reports
    // zero, which curl 8.21.0 prints as the epoch.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDeviceReportingEpoch_ReportsTheUnixEpoch()
    {
        var fileSystem = new PhysicalFileSystem(setsUnixCreateMode: false, reportsUnreadableTimestampAsEpoch: true);

        var result = await fileSystem.OpenForReadAsync("NUL", CancellationToken.None);

        await result.Content!.DisposeAsync();
        Assert.AreEqual(DateTimeOffset.UnixEpoch, result.LastWriteTimeUtc);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDeviceNotReportingEpoch_ReportsNoTimestamp()
    {
        var fileSystem = new PhysicalFileSystem(setsUnixCreateMode: false, reportsUnreadableTimestampAsEpoch: false);

        var result = await fileSystem.OpenForReadAsync("NUL", CancellationToken.None);

        await result.Content!.DisposeAsync();
        Assert.IsNull(result.LastWriteTimeUtc);
    }

    // curl 8.21.0 on Windows, measured: `curl -sI file:///NUL` prints
    // "Content-Length: 0", "Accept-ranges: bytes" and "Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT".
    // FileProtocolHandler writes the date with the "R" format, so this pins its bytes.
    [TestMethod]
    [TestCategory("Integration")]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForReadAsync_WindowsNullDevice_ReportsCurlsMeasuredLastModifiedDate()
    {
        var result = await new PhysicalFileSystem().OpenForReadAsync("NUL", CancellationToken.None);

        await result.Content!.DisposeAsync();
        Assert.AreEqual(0L, result.Length);
        Assert.IsNotNull(result.LastWriteTimeUtc);
        Assert.AreEqual(
            "Thu, 01 Jan 1970 00:00:00 GMT",
            result.LastWriteTimeUtc.Value.UtcDateTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_DestinationDirectoryMissing_IsNotFound()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem()
            .OpenForWriteAsync(directory.Combine("missing/destination.txt"), FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        AssertFailed(FileAccessStatus.NotFound, result);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_Directory_IsIsDirectory()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem()
            .OpenForWriteAsync(directory.Path, FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        AssertFailed(FileAccessStatus.IsDirectory, result);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_TruncateOverExistingFile_ReplacesItsContent()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("destination.txt");
        await System.IO.File.WriteAllTextAsync(path, "old content that is longer");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(0L, result.Length);
        await using (var content = result.Content!)
        {
            await content.WriteAsync(Content);
        }

        CollectionAssert.AreEqual(Content, await System.IO.File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_TruncateWithNoFile_CreatesIt()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("created.txt");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, DefaultCreateMode, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        Assert.IsTrue(System.IO.File.Exists(path));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_CreateNewOverExistingFile_IsAlreadyExistsAndKeepsItsBytes()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("taken.txt");
        await System.IO.File.WriteAllTextAsync(path, "kept");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.CreateNew, DefaultCreateMode, CancellationToken.None);

        AssertFailed(FileAccessStatus.AlreadyExists, result);
        Assert.AreEqual("kept", await System.IO.File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_CreateNewWithNoFile_CreatesIt()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("new.txt");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.CreateNew, DefaultCreateMode, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await using (var content = result.Content!)
        {
            await content.WriteAsync(Content);
        }

        CollectionAssert.AreEqual(Content, await System.IO.File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_Append_PositionsAfterTheExistingContent()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("destination.txt");
        await System.IO.File.WriteAllTextAsync(path, "Hello");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Append, DefaultCreateMode, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(5L, result.Length);
        await using (var content = result.Content!)
        {
            Assert.AreEqual(5L, content.Position);
            await content.WriteAsync(Encoding.ASCII.GetBytes(" file"));
        }

        CollectionAssert.AreEqual(Content, await System.IO.File.ReadAllBytesAsync(path));
    }

    // FileStreamOptions.UnixCreateMode throws on Windows, so reaching it there proves the
    // create mode is handed to the operating system on the POSIX path; nothing is opened.
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForWriteAsync_SettingUnixCreateModeOnWindows_ReachesUnixCreateMode()
    {
        var fileSystem = new PhysicalFileSystem(setsUnixCreateMode: true);

        await Assert.ThrowsExactlyAsync<PlatformNotSupportedException>(
            () => fileSystem.OpenForWriteAsync("unused", FileWriteMode.Truncate, Mode0600, CancellationToken.None).AsTask());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForWriteAsync_CreateModeOnWindows_IsIgnored()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("created.txt");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, UnixFileMode.None, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        Assert.IsTrue(System.IO.File.Exists(path));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    [UnsupportedOSPlatform("windows")]
    public async Task OpenForWriteAsync_CreateModeOnPosix_IsTheModeOfTheCreatedFile()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("created.txt");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, Mode0600, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        Assert.AreEqual(Mode0600, System.IO.File.GetUnixFileMode(path));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux | OperatingSystems.OSX | OperatingSystems.FreeBSD)]
    [UnsupportedOSPlatform("windows")]
    public async Task OpenForWriteAsync_CreateModeOnPosixOverExistingFile_KeepsItsMode()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("existing.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        System.IO.File.SetUnixFileMode(path, DefaultCreateMode);

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, Mode0600, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        Assert.AreEqual(DefaultCreateMode, System.IO.File.GetUnixFileMode(path));
    }

    [TestMethod]
    public async Task TrySetLastWriteUnixSeconds_ExistingFile_SetsItsLastWriteTime()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        var lastWriteTimeUtc = new DateTimeOffset(2020, 1, 2, 10, 4, 5, TimeSpan.Zero);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, lastWriteTimeUtc.ToUnixTimeSeconds(), out int errorCode);

        Assert.IsTrue(set);
        Assert.AreEqual(0, errorCode);
        Assert.AreEqual(lastWriteTimeUtc.UtcDateTime, System.IO.File.GetLastWriteTimeUtc(path));
    }

    [TestMethod]
    public void TrySetLastWriteUnixSeconds_MissingFile_ReturnsFalseWithErrorFileNotFound()
    {
        using var directory = new TemporaryDirectory();

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(directory.Combine("missing.txt"), 0, out int errorCode);

        Assert.IsFalse(set);
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
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, 910670515199, out int errorCode);

        Assert.IsTrue(set);
        Assert.AreEqual(0, errorCode);
        Assert.AreEqual(910670515199, (ReadWin32LastWriteFileTime(path) / 10_000_000) - 11_644_473_600);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void TrySetLastWriteUnixSeconds_OnWindowsMissingFilePastYear9999_ReturnsFalseWithErrorFileNotFound()
    {
        using var directory = new TemporaryDirectory();

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(directory.Combine("missing.txt"), 910670515199, out int errorCode);

        Assert.IsFalse(set);
        Assert.AreEqual(2, errorCode);
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
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);

        bool set = new PhysicalFileSystem().TrySetLastWriteUnixSeconds(path, unixSeconds, out int errorCode);

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

    private static void AssertFailed(FileAccessStatus expected, FileOpenResult result)
    {
        Assert.AreEqual(expected, result.Status);
        Assert.IsNull(result.Content);
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var fileSystem = new PhysicalFileSystem();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => fileSystem.ListEntryNamesAsync("unused", new CancellationToken(canceled: true)).AsTask());
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_Directory_ReturnsEveryEntryNameIncludingDotNamesAndSubdirectories()
    {
        using var directory = new TemporaryDirectory();
        await System.IO.File.WriteAllBytesAsync(directory.Combine("a.txt"), Content);
        await System.IO.File.WriteAllBytesAsync(directory.Combine(".hidden"), Content);
        Directory.CreateDirectory(directory.Combine("sub"));

        var names = await new PhysicalFileSystem().ListEntryNamesAsync(directory.Path, CancellationToken.None);

        Assert.IsNotNull(names);
        CollectionAssert.AreEquivalent(new[] { "a.txt", ".hidden", "sub" }, names.ToArray());
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_MissingDirectory_ReturnsNull()
    {
        using var directory = new TemporaryDirectory();

        var names = await new PhysicalFileSystem().ListEntryNamesAsync(directory.Combine("missing"), CancellationToken.None);

        Assert.IsNull(names);
    }

    [TestMethod]
    public async Task ListEntryNamesAsync_RegularFile_ReturnsNull()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("source.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);

        var names = await new PhysicalFileSystem().ListEntryNamesAsync(path, CancellationToken.None);

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
