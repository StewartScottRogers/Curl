using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace Curl.Core.UnitTests.FileSystem;

/// <summary>
/// Fitness finding STD-207: off Windows, a <see cref="FileWriteMode.Replace" /> open writes a
/// temporary file and renames it over the path, as libcurl's <c>Curl_fopen</c> does.
/// </summary>
[TestClass]
public sealed class PhysicalFileSystemReplaceTests
{
    private const UnixFileMode Mode0666 =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

    private readonly string directory = Directory.CreateTempSubdirectory("curl-replace-").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    public async Task OpenForWriteAsync_ReplaceOfARegularFile_ChangesItOnlyWhenTheStreamCloses()
    {
        string path = FileHolding("jar.txt", "old");

        FileOpenResult opened = await OpenForReplaceAsync(ReplacingFileSystem(Mode0666), path);
        await using (Stream content = opened.Content!)
        {
            await content.WriteAsync("new"u8.ToArray());
            content.Write("er"u8.ToArray(), 0, 2);
            await content.FlushAsync();

            Assert.AreEqual(string.Empty, File.ReadAllText(path));
            Assert.HasCount(2, Directory.GetFiles(directory, "*"));
        }

        Assert.AreEqual("newer", File.ReadAllText(path));
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_ReplaceOfADeviceOrPipe_WritesItInPlace()
    {
        string path = FileHolding("pipe", "old");

        FileOpenResult opened = await OpenForReplaceAsync(ReplacingFileSystem(mode: null), path);
        await using (Stream content = opened.Content!)
        {
            Assert.IsInstanceOfType<FileStream>(content);
        }

        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_ReplaceUnderAMissingDirectory_Fails()
    {
        string path = Path.Combine(directory, "missing", "jar.txt");

        FileOpenResult opened = await OpenForReplaceAsync(ReplacingFileSystem(Mode0666), path);

        Assert.AreEqual(FileAccessStatus.NotFound, opened.Status);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForWriteAsync_ReplaceWhoseTemporaryFileCannotBeCreated_Fails()
    {
        // The device namespace opens NUL and refuses any other new name beside it.
        FileOpenResult opened = await OpenForReplaceAsync(ReplacingFileSystem(Mode0666), @"\\.\NUL");

        Assert.IsFalse(opened.IsOpen);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_ReplaceWhereEveryFileIsWrittenInPlace_Truncates()
    {
        string path = FileHolding("jar.txt", "old");

        FileOpenResult opened = await OpenForReplaceAsync(new PhysicalFileSystem(setsUnixCreateMode: false), path);
        await using (Stream content = opened.Content!)
        {
            Assert.IsInstanceOfType<FileStream>(content);
        }

        Assert.AreEqual(string.Empty, File.ReadAllText(path));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_WorldWritableFileOffWindows_GetsTheUmaskedMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // A file created with 0666 shows what this process's umask leaves of it.
        string probe = Path.Combine(directory, "probe");
        new FileStream(probe, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = Mode0666 }).Dispose();
        string path = FileHolding("jar.txt", "old");
        File.SetUnixFileMode(path, Mode0666);

        bool replaced = await new PhysicalFileSystem().TryReplaceFileAsync(path, "new"u8.ToArray(), Mode0666, CancellationToken.None);

        Assert.IsTrue(replaced);
        Assert.AreEqual("new", File.ReadAllText(path));
        Assert.AreEqual(File.GetUnixFileMode(probe), File.GetUnixFileMode(path));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_SymbolicLinkOffWindows_IsReplacedAndItsTargetGetsNothing()
    {
        string target = Path.Combine(directory, "victim.txt");
        string link = Path.Combine(directory, "jar.txt");
        File.CreateSymbolicLink(link, "victim.txt");

        bool replaced = await new PhysicalFileSystem().TryReplaceFileAsync(link, "b=1"u8.ToArray(), Mode0666, CancellationToken.None);

        Assert.IsTrue(replaced);
        Assert.IsNull(new FileInfo(link).LinkTarget);
        Assert.AreEqual("b=1", File.ReadAllText(link));
        Assert.AreEqual(string.Empty, File.ReadAllText(target));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task OpenForWriteAsync_ReplaceOfTheNullDeviceOffWindows_WritesItInPlace()
    {
        FileOpenResult opened = await OpenForReplaceAsync(new PhysicalFileSystem(), "/dev/null");

        await using Stream content = opened.Content!;
        Assert.IsInstanceOfType<FileStream>(content);
    }

    [TestMethod]
    public void ReplaceableModeReaderFor_Platform_ReadsModesOnlyOffWindows()
    {
        Assert.IsNull(PhysicalFileSystem.ReplaceableModeReaderFor(isWindows: true));
        Assert.IsNotNull(PhysicalFileSystem.ReplaceableModeReaderFor(isWindows: false));
    }

    [TestMethod]
    [DataRow(0x81B6, 0x1B6, DisplayName = "regular file, 0666")]
    [DataRow(0x89ED, 0x9ED, DisplayName = "regular file, set-user-ID 04755")]
    [DataRow(0x21B6, -1, DisplayName = "character device")]
    [DataRow(0x11B6, -1, DisplayName = "FIFO")]
    public void ReplaceableModeIn_Status_GivesTheBitsOfARegularFileOnly(int status, int bits)
    {
        UnixFileMode? mode = PhysicalFileSystem.ReplaceableModeIn(status);

        Assert.AreEqual(bits < 0 ? null : (UnixFileMode?)bits, mode);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "write fails")]
    [DataRow(true, DisplayName = "flush fails")]
    public void ReplacementFileStream_AfterAFailedWrite_DeletesTheTemporaryFileAndKeepsTheOld(bool failsOnFlush)
    {
        string path = FileHolding("jar.txt", "old");
        string temporaryPath = FileHolding("jar.tmp", "partial");
        ReplacementFileStream stream = new(new FailingStream(), temporaryPath, path);

        Assert.ThrowsExactly<IOException>(() =>
        {
            if (failsOnFlush)
            {
                stream.Flush();
            }
            else
            {
                stream.Write([1], 0, 1);
            }
        });
        stream.Dispose();

        Assert.AreEqual("old", File.ReadAllText(path));
        Assert.IsFalse(File.Exists(temporaryPath));
    }

    [TestMethod]
    public void ReplacementFileStream_ThatCannotBeRenamed_ThrowsIOExceptionAndDeletesTheTemporaryFile()
    {
        string path = Directory.CreateDirectory(Path.Combine(directory, "taken")).FullName;
        string temporaryPath = FileHolding("jar.tmp", "new");
        ReplacementFileStream stream = new(new MemoryStream(), temporaryPath, path);

        Assert.ThrowsExactly<IOException>(stream.Dispose);
        stream.Dispose();

        Assert.IsFalse(File.Exists(temporaryPath));
    }

    [TestMethod]
    public void ReplacementFileStream_IsWriteOnlyAndCannotSeek()
    {
        using ReplacementFileStream stream = new(new MemoryStream(), Path.Combine(directory, "none.tmp"), FileHolding("jar.txt", "old"));

        Assert.IsFalse(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsTrue(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));

        // Nothing was written beside the path, so there is nothing to rename over it.
        File.WriteAllText(Path.Combine(directory, "none.tmp"), "new");
    }

    private static PhysicalFileSystem ReplacingFileSystem(UnixFileMode? mode) =>
        new(setsUnixCreateMode: false, readReplaceableMode: (SafeFileHandle _) => mode);

    private static ValueTask<FileOpenResult> OpenForReplaceAsync(PhysicalFileSystem fileSystem, string path) =>
        fileSystem.OpenForWriteAsync(path, FileWriteMode.Replace, Mode0666, CancellationToken.None);

    private string FileHolding(string name, string content)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class FailingStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("There is not enough space on the disk.");

        public override void Flush() => throw new IOException("There is not enough space on the disk.");
    }
}
