using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

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
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dir/f.txt");

    private static string OsPath => "/dir/f.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("0123456789");

    [TestMethod]
    public async Task ExecuteAsync_SuccessfulDownload_ReportsStartedOnce()
    {
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("file", "/dir/f.txt, 10 bytes");

        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress });

        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("transfer started count", 1, progress.TransferStartedCount);
        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadPastMaxFileSize_ReportsStartedOnce()
    {
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("max file size", 5);

        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress, MaxFileSize = 5 }, CurlExitCode.FilesizeExceeded);

        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("transfer started count", 1, progress.TransferStartedCount);
        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadResumedPastTheEnd_ReportsStartedOnce()
    {
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("resume from", 20);

        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress, ResumeFrom = 20 }, CurlExitCode.BadDownloadResume);

        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("transfer started count", 1, progress.TransferStartedCount);
        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadWithUnmetTimeCondition_ReportsStartedOnce()
    {
        var condition = new TimeCondition(FakeFileSystem.DefaultLastWriteTimeUtc.AddDays(1), TimeConditionKind.IfModifiedSince);
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("time condition", "if modified since one day after the file timestamp");

        var progress = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress, TimeCondition = condition });

        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("transfer started count", 1, progress.TransferStartedCount);
        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuccessfulUpload_ReportsStartedOnce()
    {
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("upload bytes", Content.Length);

        var progress = await UploadAsync(new FakeFileSystem(), CurlExitCode.Ok);

        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("transfer started count", 1, progress.TransferStartedCount);
        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationDoesNotOpen_ReportsStartedOnce()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForWrite(OsPath, FileAccessStatus.NotFound);
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("open for write", FileAccessStatus.NotFound);

        var progress = await UploadAsync(fileSystem, CurlExitCode.WriteError);

        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("transfer started count", 1, progress.TransferStartedCount);
        Assert.AreEqual(1, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_MissingFile_NeverReportsStarted()
    {
        var progress = new RecordingTransferProgress();
        var context = new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress };
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("file system", "empty");

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Diagnostics.Assert("transfer started count", 0, progress.TransferStartedCount);
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
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("file system", "directory /dir/f.txt");

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Diagnostics.Assert("transfer started count", 0, progress.TransferStartedCount);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(0, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_NotAFileUrl_NeverReportsStarted()
    {
        var progress = new RecordingTransferProgress();
        var context = new TransferContext { Url = CurlUrl.Parse("http://example.com/x"), Output = new ChunkRecordingStream(), Progress = progress };
        Diagnostics.Arrange("url", "http://example.com/x");

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("transfer started count", progress.TransferStartedCount);
        Diagnostics.Assert("exit code", CurlExitCode.UrlMalformat, result.ExitCode);
        Diagnostics.Assert("transfer started count", 0, progress.TransferStartedCount);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(0, progress.TransferStartedCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadAndUpload_NeverReportByteCounts()
    {
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("transfers", "one download, one upload");

        var download = await DownloadAsync(progress => new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Progress = progress });
        var upload = await UploadAsync(new FakeFileSystem(), CurlExitCode.Ok);

        Diagnostics.Act("download byte report count", download.ByteReportCount);
        Diagnostics.Act("upload byte report count", upload.ByteReportCount);
        Diagnostics.Assert("download byte report count", 0, download.ByteReportCount);
        Diagnostics.Assert("upload byte report count", 0, upload.ByteReportCount);
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
