using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

namespace Curl.Protocol.File;

/// <summary>
/// Pins when the <c>file://</c> handler reports "transfer started" to the progress sink,
/// against curl 8.21.0 as measured in BL-129: a download is started once its source is
/// open, so a failure after the open (exit 63, exit 36, an unmet <c>-z</c>) draws the meter
/// and a failed open (exit 37) does not; an upload is started before its destination
/// opens, so even exit 23 draws the meter. The handler never reports byte counts, because
/// curl's <c>file://</c> status line stays at zero.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerProgressTests
{
    private static CurlUrl FileUrl => CurlUrl.Parse("file:///C:/dir/f.txt");

    private static string OsPath => "C:/dir/f.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("0123456789");

    [TestMethod]
    public async Task ExecuteAsync_SuccessfulDownload_ReportsStartedOnce()
    {
        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress });

        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadPastMaxFileSize_ReportsStartedOnce()
    {
        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress, MaxFileSize = 5 }, CurlExitCode.FilesizeExceeded);

        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadResumedPastTheEnd_ReportsStartedOnce()
    {
        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress, ResumeFrom = 20 }, CurlExitCode.BadDownloadResume);

        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithUnmetTimeCondition_ReportsStartedOnce()
    {
        var condition = new TimeCondition(FakeFileSystem.DefaultLastWriteTimeUtc.AddDays(1), TimeConditionKind.IfModifiedSince);

        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress, TimeCondition = condition });

        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessfulUpload_ReportsStartedOnce()
    {
        var progress = await UploadAsync(new FakeFileSystem(), CurlExitCode.Ok);

        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationDoesNotOpen_ReportsStartedOnce()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForWrite(OsPath, FileAccessStatus.NotFound);

        var progress = await UploadAsync(fileSystem, CurlExitCode.WriteError);

        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingFile_NeverReportsStarted()
    {
        var progress = new RecordingTransferProgress();
        var context = new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress };

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(0, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DirectorySource_NeverReportsStarted()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddDirectory(OsPath);
        var progress = new RecordingTransferProgress();
        var context = new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(0, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotAFileUrl_NeverReportsStarted()
    {
        var progress = new RecordingTransferProgress();
        var context = new TransferContext { Url = CurlUrl.Parse("http://example.com/x"), Output = new ChunkRecordingStream(), Progress = progress };

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(0, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadAndUpload_NeverReportByteCounts()
    {
        var download = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress });
        var upload = await UploadAsync(new FakeFileSystem(), CurlExitCode.Ok);

        Assert.AreEqual(0, download.ByteReportCount);
        Assert.AreEqual(0, upload.ByteReportCount);
    }

    private static async Task<RecordingTransferProgress> DownloadAsync(
        Func<ITransferProgress, TransferContext> createContext,
        CurlExitCode expectedExitCode = CurlExitCode.Ok)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var progress = new RecordingTransferProgress();
        var context = createContext(progress);

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(expectedExitCode, result.ExitCode);
        return progress;
    }

    private static async Task<RecordingTransferProgress> UploadAsync(FakeFileSystem fileSystem, CurlExitCode expectedExitCode)
    {
        var progress = new RecordingTransferProgress();
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            Upload = new TrackedMemoryStream(Content),
            Progress = progress,
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(expectedExitCode, result.ExitCode);
        return progress;
    }
}
