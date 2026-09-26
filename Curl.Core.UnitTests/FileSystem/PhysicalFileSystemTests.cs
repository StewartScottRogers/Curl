using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Core.FileSystem;

/// <summary>
/// Drives <see cref="PhysicalFileSystem" /> against the real disk, in a fresh temporary
/// directory per test. Every method that touches the disk is
/// <c>[TestCategory("Integration")]</c>, so the fast run leaves them out; the two
/// cancellation tests refuse before any disk access and stay in the fast run.
/// </summary>
[TestClass]
public sealed class PhysicalFileSystemTests
{
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
                .OpenForWriteAsync("unused", FileWriteMode.Truncate, new CancellationToken(canceled: true))
                .AsTask());
    }

    [TestMethod]
    [TestCategory("Integration")]
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
    [TestCategory("Integration")]
    public async Task OpenForReadAsync_MissingFile_IsNotFound()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("missing.txt"), CancellationToken.None);

        AssertFailed(FileAccessStatus.NotFound, result);
    }

    [TestMethod]
    [TestCategory("Integration")]
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
    [TestCategory("Integration")]
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
    [TestCategory("Integration")]
    public async Task OpenForReadAsync_LiteralPercentInMissingName_IsNotFound()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem().OpenForReadAsync(directory.Combine("%GG.txt"), CancellationToken.None);

        AssertFailed(FileAccessStatus.NotFound, result);
    }

    // A character device opens, but as a handle that cannot seek and has no length: the
    // shape file:///dev/stdin arrives in, which the handler must not seek.
    [TestMethod]
    [TestCategory("Integration")]
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

    [TestMethod]
    [TestCategory("Integration")]
    public async Task OpenForWriteAsync_DestinationDirectoryMissing_IsNotFound()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem()
            .OpenForWriteAsync(directory.Combine("missing/destination.txt"), FileWriteMode.Truncate, CancellationToken.None);

        AssertFailed(FileAccessStatus.NotFound, result);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task OpenForWriteAsync_Directory_IsIsDirectory()
    {
        using var directory = new TemporaryDirectory();

        var result = await new PhysicalFileSystem()
            .OpenForWriteAsync(directory.Path, FileWriteMode.Truncate, CancellationToken.None);

        AssertFailed(FileAccessStatus.IsDirectory, result);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task OpenForWriteAsync_TruncateOverExistingFile_ReplacesItsContent()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("destination.txt");
        await System.IO.File.WriteAllTextAsync(path, "old content that is longer");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(0L, result.Length);
        await using (var content = result.Content!)
        {
            await content.WriteAsync(Content);
        }

        CollectionAssert.AreEqual(Content, await System.IO.File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task OpenForWriteAsync_TruncateWithNoFile_CreatesIt()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("created.txt");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Truncate, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        await result.Content!.DisposeAsync();
        Assert.IsTrue(System.IO.File.Exists(path));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task OpenForWriteAsync_Append_PositionsAfterTheExistingContent()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.Combine("destination.txt");
        await System.IO.File.WriteAllTextAsync(path, "Hello");

        var result = await new PhysicalFileSystem().OpenForWriteAsync(path, FileWriteMode.Append, CancellationToken.None);

        Assert.AreEqual(FileAccessStatus.Ok, result.Status);
        Assert.AreEqual(5L, result.Length);
        await using (var content = result.Content!)
        {
            Assert.AreEqual(5L, content.Position);
            await content.WriteAsync(Encoding.ASCII.GetBytes(" file"));
        }

        CollectionAssert.AreEqual(Content, await System.IO.File.ReadAllBytesAsync(path));
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
