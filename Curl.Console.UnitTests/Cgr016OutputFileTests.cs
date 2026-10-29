using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins CGR-016's <c>-o</c> part (BL-3853): off Windows an output file name holding an argument
/// byte that is not valid UTF-8 opens under its raw bytes, as curl 8.18.0 on Linux creates
/// <c>-o $'\xff.txt'</c> as FF 2E 74 78 74; every other name opens through the file system.
/// </summary>
[TestClass]
public sealed class Cgr016OutputFileTests
{
    private readonly InMemoryFileSystem fileSystem = new();

    private readonly List<(byte[] Path, FileWriteMode Mode, UnixFileMode CreateMode)> rawOpens = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OpenForWriteAsync_EscapedByteOffWindows_OpensTheRawBytes()
    {
        FileOpenResult opened = await OpenAsync("\uDCFF.txt", isWindows: false);

        Assert.AreEqual(FileAccessStatus.AccessDenied, opened.Status);
        Assert.HasCount(1, rawOpens);
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x2E, 0x74, 0x78, 0x74, 0x00 }, rawOpens[0].Path);
        Assert.AreEqual(FileWriteMode.Append, rawOpens[0].Mode);
        Assert.AreEqual(DeferredOutputFileStream.CreateMode, rawOpens[0].CreateMode);
        Assert.IsEmpty(fileSystem.WriteModes);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_EscapedByteOnWindows_OpensThroughTheFileSystem()
    {
        FileOpenResult opened = await OpenAsync("\uDCFF.txt", isWindows: true);

        Assert.IsTrue(opened.IsOpen);
        Assert.IsEmpty(rawOpens);
        Assert.IsTrue(fileSystem.Written.ContainsKey("\uDCFF.txt"));
    }

    [TestMethod]
    public async Task OpenForWriteAsync_ValidNameOffWindows_OpensThroughTheFileSystem()
    {
        FileOpenResult opened = await OpenAsync("dir/é�.txt", isWindows: false);

        Assert.IsTrue(opened.IsOpen);
        Assert.IsEmpty(rawOpens);
    }

    [TestMethod]
    public async Task OpenForWriteAsync_CancelledRawOpen_Throws()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await RawByteFileName.OpenForWriteAsync(
            fileSystem, "\uDC80", FileWriteMode.Truncate, DeferredOutputFileStream.CreateMode, RecordRawOpen, cancelled.Token));
        Assert.IsEmpty(rawOpens);
    }

    [TestMethod]
    public void HoldsEscapedByte_OnlyTheEscapeRange_Counts()
    {
        Assert.IsTrue(RawByteFileName.HoldsEscapedByte("a\uDC80"));
        Assert.IsTrue(RawByteFileName.HoldsEscapedByte("\uDCFFb"));
        Assert.IsFalse(RawByteFileName.HoldsEscapedByte("a\uDC7F\uDD00b.txt"));
    }

    [TestMethod]
    public void NullTerminatedBytes_KeepsValidUtf8AndEscapedBytes()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x64, 0x2F, 0xC3, 0xA9, 0xFE, 0x00 },
            RawByteFileName.NullTerminatedBytes("d/é\uDCFE"));
    }

    [TestMethod]
    public void OpenFlagsFor_EachMode_CreatesForWriting()
    {
        const int Always = RawByteFileName.WriteOnly | RawByteFileName.CloseOnExec | RawByteFileName.Create;

        Assert.AreEqual(Always | RawByteFileName.Truncate, RawByteFileName.OpenFlagsFor(FileWriteMode.Truncate));
        Assert.AreEqual(Always | RawByteFileName.Exclusive, RawByteFileName.OpenFlagsFor(FileWriteMode.CreateNew));
        Assert.AreEqual(Always, RawByteFileName.OpenFlagsFor(FileWriteMode.Append));
    }

    [TestMethod]
    [DataRow(1, FileAccessStatus.AccessDenied)]
    [DataRow(13, FileAccessStatus.AccessDenied)]
    [DataRow(2, FileAccessStatus.NotFound)]
    [DataRow(17, FileAccessStatus.AlreadyExists)]
    [DataRow(20, FileAccessStatus.NotADirectory)]
    [DataRow(21, FileAccessStatus.IsDirectory)]
    [DataRow(28, FileAccessStatus.IoError)]
    public void StatusFor_Errno_IsItsStatus(int errno, FileAccessStatus expected) =>
        Assert.AreEqual(expected, RawByteFileName.StatusFor(errno));

    private ValueTask<FileOpenResult> OpenAsync(string path, bool isWindows) =>
        RawByteFileName.OpenForWriteAsync(
            fileSystem, path, FileWriteMode.Append, DeferredOutputFileStream.CreateMode, isWindows ? null : RecordRawOpen, TestContext.CancellationToken);

    private FileOpenResult RecordRawOpen(byte[] path, FileWriteMode mode, UnixFileMode createMode)
    {
        rawOpens.Add((path, mode, createMode));
        return FileOpenResult.Failed(FileAccessStatus.AccessDenied);
    }
}
