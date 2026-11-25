using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Pins what a resumable upload rereads (ADR-1401): a window over a seekable upload, otherwise a copy in memory up to
/// the limit and in a spool file past it, with curl's exit codes when the copy cannot be made.
/// </summary>
[TestClass]
public sealed class RereadableUploadTests
{
    private static readonly byte[] Content = [.. Enumerable.Range(0, 200_000).Select(position => ReadRecordingUpload.ByteAt(position))];

    [TestMethod]
    public async Task OpenAsync_SeekableUpload_IsAWindowFromWhereItStandsThatReadsNothingAndLeavesItOpen()
    {
        var upload = new ReadRecordingUpload(3L * int.MaxValue) { Position = 5 };

        var opened = await RereadableUpload.OpenAsync(upload, memoryLimit: 0, NoSpoolFile, CancellationToken.None);
        var length = opened.Length;
        await opened.DisposeAsync();

        Assert.AreEqual((3L * int.MaxValue) - 5, length);
        Assert.AreEqual(0, upload.BytesRead);
        Assert.AreEqual(5, upload.Position);
    }

    [TestMethod]
    public async Task OpenAsync_UnseekableUploadWithinTheLimit_IsCopiedIntoMemory()
    {
        var upload = new UnseekableUpload(Content);

        await using var opened = await RereadableUpload.OpenAsync(upload, Content.Length, NoSpoolFile, CancellationToken.None);

        CollectionAssert.AreEqual(Content, ((MemoryStream)opened).ToArray());
    }

    [TestMethod]
    public async Task OpenAsync_UnseekableUploadPastTheLimit_IsCopiedWholeIntoTheSpoolFile()
    {
        var spool = new MemoryStream();

        var opened = await RereadableUpload.OpenAsync(new UnseekableUpload(Content), memoryLimit: 100_000, () => spool, CancellationToken.None);

        Assert.AreSame(spool, opened);
        CollectionAssert.AreEqual(Content, spool.ToArray());
    }

    [TestMethod]
    public async Task OpenAsync_UploadThatCannotBeReadAfterSpooling_FailsWithReadErrorAndDisposesTheSpoolFile()
    {
        var spool = new MemoryStream();
        var upload = new UnseekableUpload(Content, failure: new IOException("pipe gone"));

        var failure = await Assert.ThrowsExactlyAsync<ResumableUploadTransferException>(
            () => RereadableUpload.OpenAsync(upload, memoryLimit: 0, () => spool, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("Failed reading the upload: pipe gone", failure.Message);
        Assert.IsFalse(failure.CanResume);
        Assert.IsFalse(spool.CanWrite);
    }

    [TestMethod]
    public async Task OpenAsync_CopyCancelled_ThrowsTheCancellationAndDisposesTheSpoolFile()
    {
        var spool = new MemoryStream();
        var upload = new UnseekableUpload(Content, failure: new OperationCanceledException());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => RereadableUpload.OpenAsync(upload, memoryLimit: 0, () => spool, CancellationToken.None));

        Assert.IsFalse(spool.CanWrite);
    }

    [TestMethod]
    public async Task OpenAsync_SpoolFileThatCannotBeCreated_FailsWithWriteError()
    {
        var failure = await Assert.ThrowsExactlyAsync<ResumableUploadTransferException>(
            () => RereadableUpload.OpenAsync(
                new UnseekableUpload(Content), memoryLimit: 0, () => throw new UnauthorizedAccessException("denied"), CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WriteError, failure.ExitCode);
        Assert.AreEqual("Failed writing the upload to a temporary file: denied", failure.Message);
    }

    [TestMethod]
    public async Task OpenAsync_SpoolFileThatCannotBeWritten_FailsWithWriteErrorAndDisposesIt()
    {
        var spool = new FullDisk();

        var failure = await Assert.ThrowsExactlyAsync<ResumableUploadTransferException>(
            () => RereadableUpload.OpenAsync(new UnseekableUpload(Content), memoryLimit: 0, () => spool, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WriteError, failure.ExitCode);
        Assert.AreEqual("Failed writing the upload to a temporary file: disk full", failure.Message);
        Assert.IsTrue(spool.Disposed);
    }

    [TestMethod]
    public async Task CreateTemporaryFile_WrittenAndDisposed_IsReadableAgainAndThenDeleted()
    {
        var file = (FileStream)RereadableUpload.CreateTemporaryFile();
        var path = file.Name;
        var read = new byte[3];

        await file.WriteAsync(new byte[] { 1, 2, 3 });
        file.Position = 0;
        await file.ReadExactlyAsync(read);
        await file.DisposeAsync();

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, read);
        Assert.IsFalse(File.Exists(path));
    }

    private static Stream NoSpoolFile() => throw new AssertFailedException("No spool file was expected.");

    private sealed class FullDisk : MemoryStream
    {
        public bool Disposed { get; private set; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("disk full");

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
