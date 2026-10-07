using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

namespace Curl.Protocol.File;

/// <summary>
/// Pins curl 8.21.0's refusal of a <c>file://</c> path that percent-decodes to a NUL byte:
/// <c>lib/file.c</c> decodes it with <c>REJECT_ZERO</c>, so the transfer ends with exit 3
/// and <c>curl_easy_strerror</c>'s bare text before anything is opened, and, since no
/// <c>failf</c> wrote it, <c>-v</c> prints no line at all. Measured on Windows on
/// 2026-10-07 (BL-1451): <c>curl -sSv file:///dir/f.txt%00x</c> and
/// <c>curl -sSv -T f.txt file:///dir/up%00x</c> each write only
/// <c>curl: (3) URL using bad/illegal format or missing URL</c>, while
/// <c>file:///dir/f%0atxt</c> is still decoded and fails to open with exit 37.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerNulBytePathTests
{
    private const string UrlMalformed = "URL using bad/illegal format or missing URL";

    [TestMethod]
    public async Task ExecuteAsync_DownloadOfPathDecodingToNul_FailsWithUrlMalformatBeforeOpeningAnything()
    {
        var fileSystem = new FakeFileSystem();
        var events = new RecordingTransferEvents();
        var progress = new RecordingTransferProgress { Transcript = events.Transcript };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("file:///dir/f.txt%00x"),
            Output = new MemoryStream(),
            Events = events,
            Progress = progress,
        });

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(UrlMalformed, result.ErrorMessage);
        Assert.IsEmpty(fileSystem.Calls);
        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToPathDecodingToNul_FailsWithUrlMalformatBeforeCreatingAnything()
    {
        var fileSystem = new FakeFileSystem();
        var events = new RecordingTransferEvents();
        var progress = new RecordingTransferProgress { Transcript = events.Transcript };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("file:///dir/up%00x"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.ASCII.GetBytes("hi\n")),
            Events = events,
            Progress = progress,
        });

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual(UrlMalformed, result.ErrorMessage);
        Assert.IsEmpty(fileSystem.Calls);
        Assert.IsEmpty(events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_DownloadOfPathDecodingToLineFeed_DecodesAndOpensIt()
    {
        var fileSystem = new FakeFileSystem();
        string osPath = "/dir/f\ntxt".Replace('/', Path.DirectorySeparatorChar);
        fileSystem.AddFile(osPath, Encoding.ASCII.GetBytes("hello\n"));
        var output = new MemoryStream();

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("file:///dir/f%0atxt"),
            Output = output,
        });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello\n", Encoding.ASCII.GetString(output.ToArray()));
    }
}
