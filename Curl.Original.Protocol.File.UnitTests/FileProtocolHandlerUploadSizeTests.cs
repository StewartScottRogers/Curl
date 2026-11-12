using System.Text;
using Curl.Original.Protocol.File.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins that a <c>file://</c> upload reports its bytes as the upload size, not the download
/// size, as curl's <c>%{size_upload}</c> and <c>%{size_download}</c> show (BL-5025).
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerUploadSizeTests
{
    private static string OsPath => "/dir/w.out".Replace('/', Path.DirectorySeparatorChar);

    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsTheBytesAsUploadSizeAndNoDownloadSize()
    {
        var fileSystem = new FakeFileSystem();
        var context = new TransferContext
        {
            Output = new MemoryStream(),
            Url = CurlUrl.Parse("file:///dir/w.out"),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes("HELLO\n")),
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNotNull(result.Report);
        Assert.AreEqual(6L, result.Report.UploadSize);
        Assert.AreEqual(0L, result.Report.DownloadSize);
        Assert.AreEqual("HELLO\n", Encoding.ASCII.GetString(fileSystem.WrittenBytes(OsPath)));
    }

    [TestMethod]
    public async Task ExecuteAsync_Download_ReportsTheBytesAsDownloadSizeAndNoUploadSize()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("HELLO\n"));
        var context = new TransferContext
        {
            Output = new MemoryStream(),
            Url = CurlUrl.Parse("file:///dir/w.out"),
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(6L, result.Report?.DownloadSize ?? result.BytesTransferred);
        Assert.AreEqual(0L, result.Report?.UploadSize ?? 0L);
    }
}
