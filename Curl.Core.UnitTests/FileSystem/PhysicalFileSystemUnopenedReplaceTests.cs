using System.Runtime.InteropServices;
using Curl.Core.FileSystem;
using Curl.Protocol.Abstractions;

namespace Curl.Core.UnitTests.FileSystem;

/// <summary>
/// ADR-1385: on Windows a <see cref="FileWriteMode.Replace" /> open of an existing file writes a
/// temporary file and renames it over the path without opening it, retrying a refused rename
/// for a second, as curl's Windows build does with the cookie jar, the HSTS cache and the
/// alt-svc cache.
/// </summary>
[TestClass]
public sealed partial class PhysicalFileSystemUnopenedReplaceTests
{
    private readonly string directory = Directory.CreateTempSubdirectory("curl-unopened-").FullName;

    [TestCleanup]
    public void DeleteDirectory()
    {
        foreach (string file in Directory.GetFiles(directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(directory, recursive: true);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_ReplaceOfAnUnopenedFile_ChangesItOnlyWhenTheStreamCloses()
    {
        string path = FileHolding("jar.txt", "old");
        PhysicalFileSystem fileSystem = new(setsUnixCreateMode: false, isReplacedUnopened: File.Exists);

        FileOpenResult opened = await fileSystem.OpenForWriteAsync(path, FileWriteMode.Replace, default, CancellationToken.None);
        await using (Stream content = opened.Content!)
        {
            await content.WriteAsync("new"u8.ToArray());

            Assert.AreEqual("old", File.ReadAllText(path));
        }

        Assert.AreEqual("new", File.ReadAllText(path));
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    public async Task DisposeAsync_RenameRefusedForTheWholeWindow_ThrowsAfterASecondAndDeletesTheTemporaryFile()
    {
        string path = Directory.CreateDirectory(Path.Combine(directory, "taken")).FullName;
        string temporaryPath = FileHolding("jar.tmp", "new");
        SteppingClock clock = new(TimeSpan.FromMilliseconds(400), () => { });
        ReplacementFileStream stream = new(new MemoryStream(), temporaryPath, path, PhysicalFileSystem.WindowsRenameRetryWindow, clock);

        await Assert.ThrowsExactlyAsync<IOException>(async () => await stream.DisposeAsync());
        await stream.DisposeAsync();

        Assert.AreEqual(3, clock.Delays);
        Assert.IsFalse(File.Exists(temporaryPath));
    }

    [TestMethod]
    public async Task DisposeAsync_RenameAllowedWithinTheWindow_ReplacesTheFile()
    {
        string path = Directory.CreateDirectory(Path.Combine(directory, "taken")).FullName;
        string temporaryPath = FileHolding("jar.tmp", "new");
        SteppingClock clock = new(TimeSpan.FromMilliseconds(1), () => Directory.Delete(path));
        ReplacementFileStream stream = new(new MemoryStream(), temporaryPath, path, PhysicalFileSystem.WindowsRenameRetryWindow, clock);

        await stream.DisposeAsync();

        Assert.AreEqual(1, clock.Delays);
        Assert.AreEqual("new", File.ReadAllText(path));
    }

    [TestMethod]
    public async Task DisposeAsync_AfterAFailedWrite_DeletesTheTemporaryFileAndKeepsTheOld()
    {
        string path = FileHolding("jar.txt", "old");
        string temporaryPath = FileHolding("jar.tmp", "partial");
        ReplacementFileStream stream = new(new MemoryStream([0], writable: false), temporaryPath, path);

        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([1], 0, 1));
        await stream.DisposeAsync();

        Assert.AreEqual("old", File.ReadAllText(path));
        Assert.IsFalse(File.Exists(temporaryPath));
    }

    [TestMethod]
    public void Dispose_TemporaryFileGone_ThrowsAndKeepsTheOld()
    {
        string path = FileHolding("jar.txt", "old");
        ReplacementFileStream stream = new(new MemoryStream(), Path.Combine(directory, "gone.tmp"), path);

        Assert.ThrowsExactly<FileNotFoundException>(stream.Dispose);

        Assert.AreEqual("old", File.ReadAllText(path));
    }

    [TestMethod]
    public void UnopenedReplacementCheckFor_Platform_ChecksOnlyOnWindows()
    {
        Assert.IsNotNull(PhysicalFileSystem.UnopenedReplacementCheckFor(isWindows: true));
        Assert.IsNull(PhysicalFileSystem.UnopenedReplacementCheckFor(isWindows: false));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_ExistingFileOnWindows_IsReplacedByANewFile()
    {
        string path = FileHolding("jar.txt", "old");

        bool replaced = await ReplaceAsync(new PhysicalFileSystem(), path);

        Assert.IsTrue(replaced);
        Assert.AreEqual("new", File.ReadAllText(path));
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_HardLinkedFileOnWindows_LeavesTheOtherNameItsOldContent()
    {
        string path = FileHolding("jar.txt", "old");
        string link = Path.Combine(directory, "link.txt");
        Assert.IsTrue(CreateHardLink(link, path, 0));

        bool replaced = await ReplaceAsync(new PhysicalFileSystem(), path);

        Assert.IsTrue(replaced);
        Assert.AreEqual("new", File.ReadAllText(path));
        Assert.AreEqual("old", File.ReadAllText(link));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_HiddenFileOnWindows_IsReplacedByOneThatIsNotHidden()
    {
        string path = FileHolding("jar.txt", "old");
        File.SetAttributes(path, FileAttributes.Hidden);

        bool replaced = await ReplaceAsync(new PhysicalFileSystem(), path);

        Assert.IsTrue(replaced);
        Assert.AreEqual("new", File.ReadAllText(path));
        Assert.AreEqual((FileAttributes)0, File.GetAttributes(path) & FileAttributes.Hidden);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_ReadOnlyFileOnWindows_KeepsItsContentAndAttribute()
    {
        string path = FileHolding("jar.txt", "old");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        SteppingClock clock = new(TimeSpan.FromMilliseconds(400), () => { });

        bool replaced = await ReplaceAsync(WindowsFileSystem(clock), path);

        Assert.IsFalse(replaced);
        Assert.AreEqual(3, clock.Delays);
        Assert.AreEqual("old", File.ReadAllText(path));
        Assert.AreEqual(FileAttributes.ReadOnly, File.GetAttributes(path) & FileAttributes.ReadOnly);
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(FileShare.None, DisplayName = "shared with nobody")]
    [DataRow(FileShare.Read, DisplayName = "shared for reading")]
    [DataRow(FileShare.ReadWrite, DisplayName = "shared for reading and writing")]
    [DataRow(FileShare.ReadWrite | FileShare.Delete, DisplayName = "shared for deleting too")]
    public async Task TryReplaceFileAsync_FileHeldOpenOnWindows_KeepsItsContent(FileShare share)
    {
        string path = FileHolding("jar.txt", "old");
        SteppingClock clock = new(TimeSpan.FromMilliseconds(400), () => { });

        bool replaced;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, share))
        {
            replaced = await ReplaceAsync(WindowsFileSystem(clock), path);
        }

        Assert.IsFalse(replaced);
        Assert.AreEqual("old", File.ReadAllText(path));
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_FileReleasedWithinASecondOnWindows_IsReplaced()
    {
        string path = FileHolding("jar.txt", "old");
        FileStream holder = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        SteppingClock clock = new(TimeSpan.FromMilliseconds(1), holder.Dispose);

        bool replaced = await ReplaceAsync(WindowsFileSystem(clock), path);

        Assert.IsTrue(replaced);
        Assert.AreEqual("new", File.ReadAllText(path));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task TryReplaceFileAsync_MissingFileOnWindows_IsCreated()
    {
        string path = Path.Combine(directory, "jar.txt");

        bool replaced = await ReplaceAsync(new PhysicalFileSystem(), path);

        Assert.IsTrue(replaced);
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(directory));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task OpenForWriteAsync_ReplaceOfTheNullDeviceOnWindows_WritesItInPlace()
    {
        FileOpenResult opened = await new PhysicalFileSystem().OpenForWriteAsync("NUL", FileWriteMode.Replace, default, CancellationToken.None);

        await using Stream content = opened.Content!;
        Assert.IsInstanceOfType<FileStream>(content);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLink(string fileName, string existingFileName, nint securityAttributes);

    private static PhysicalFileSystem WindowsFileSystem(TimeProvider clock) =>
        new(setsUnixCreateMode: false, isReplacedUnopened: File.Exists, renameClock: clock);

    private static Task<bool> ReplaceAsync(PhysicalFileSystem fileSystem, string path) =>
        fileSystem.TryReplaceFileAsync(path, "new"u8.ToArray(), default, CancellationToken.None);

    private string FileHolding(string name, string content)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A clock that moves on by <c>step</c> at each delay, which then ends at once.</summary>
    private sealed class SteppingClock(TimeSpan step, Action onDelay) : TimeProvider
    {
        private long ticks;

        public int Delays { get; private set; }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref ticks);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays++;
            Interlocked.Add(ref ticks, step.Ticks);
            onDelay();
            return System.CreateTimer(callback, state, TimeSpan.Zero, period);
        }
    }
}
