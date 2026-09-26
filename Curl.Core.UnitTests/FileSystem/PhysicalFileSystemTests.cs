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
public sealed class PhysicalFileSystemTests
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
    public async Task OpenForReadAsync_Directory_IsIsDirectory()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Path, CancellationToken.None);

        AssertFailed(FileAccessStatus.IsDirectory, result);
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

    // A character device opens, but as a handle that cannot seek and has no length: the
    // shape file:///dev/stdin arrives in, which the handler must not seek.
    [TestMethod]
    public async Task OpenForReadAsync_NullDevice_OpensANonSeekableHandleOfLengthZero()
    {
        string nullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

        var result = await new PhysicalFileSystem().OpenForReadAsync(nullDevice, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.IsNotNull(result.Content);
        await using var content = result.Content;
        Assert.IsFalse(content.CanSeek);
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
    public async Task TrySetLastWriteTimeUtc_ExistingFile_SetsItsLastWriteTime()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("out.txt");
        await System.IO.File.WriteAllBytesAsync(path, Content);
        var lastWriteTimeUtc = new DateTimeOffset(2020, 1, 2, 10, 4, 5, TimeSpan.Zero);

        bool set = new PhysicalFileSystem().TrySetLastWriteTimeUtc(path, lastWriteTimeUtc);

        Assert.IsTrue(set);
        Assert.AreEqual(lastWriteTimeUtc.UtcDateTime, System.IO.File.GetLastWriteTimeUtc(path));
    }

    [TestMethod]
    public void TrySetLastWriteTimeUtc_MissingFile_ReturnsFalse()
    {
        using var directory = new TemporaryDirectory();

        bool set = new PhysicalFileSystem().TrySetLastWriteTimeUtc(directory.Combine("missing.txt"), DateTimeOffset.UnixEpoch);

        Assert.IsFalse(set);
    }

    private static void AssertFailed(FileAccessStatus expected, FileOpenResult result)
    {
        Assert.AreEqual(expected, result.Status);
        Assert.IsNull(result.Content);
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
