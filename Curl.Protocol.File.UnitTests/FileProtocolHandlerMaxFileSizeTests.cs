using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

namespace Curl.Protocol.File;

/// <summary>
/// Pins <c>--max-filesize</c> (<see cref="ITransferContext.MaxFileSize" />) on a
/// <c>file://</c> download, each case measured against curl 8.21.0 on 2026-09-26 with the
/// ten-byte file <c>HelloWorld</c>: a body longer than the limit is written up to the limit
/// and then fails with exit 63, the limit counts body bytes only, and an upload ignores it.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerMaxFileSizeTests
{
    private const int ChunkSize = 16384;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dir/f.txt");

    private static string OsPath => "/dir/f.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("HelloWorld");

    // curl -sS --max-filesize 9 file:///C:/rp13/f.txt prints HelloWorl, then
    // curl: (63) Exceeded the maximum allowed file size (9) with 9 bytes
    [TestMethod]
    public async Task ExecuteAsync_BodyLongerThanMaxFileSize_WritesUpToTheLimitThenReturnsFilesizeExceeded()
    {
        var output = new ChunkRecordingStream();
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("max file size", 9);

        var result = await DownloadAsync(Content, new TransferContext { Url = FileUrl, Output = output, MaxFileSize = 9 });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Diff("body", Encoding.ASCII.GetBytes("HelloWorl"), output.ToArray());
        Diagnostics.Assert("bytes transferred", 9L, result.BytesTransferred);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("HelloWorl"), output.ToArray());
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(63, (int)result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (9) with 9 bytes", result.ErrorMessage);
        Assert.AreEqual(9L, result.BytesTransferred);
    }

    [TestMethod]
    [DataRow(10L)]
    [DataRow(11L)]
    [DataRow(0L)]
    public async Task ExecuteAsync_BodyNoLongerThanMaxFileSizeOrZeroLimit_WritesTheWholeBody(long maxFileSize)
    {
        var output = new ChunkRecordingStream();

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("max file size", maxFileSize);

        var result = await DownloadAsync(Content, new TransferContext { Url = FileUrl, Output = output, MaxFileSize = maxFileSize });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Diff("body", Content, output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Content, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(10L, result.BytesTransferred);
    }

    // curl -sS -r 2-8 --max-filesize 5 prints lloWo and exit 63; with 7 it prints lloWorl and exit 0.
    [TestMethod]
    [DataRow(5L, "lloWo", CurlExitCode.FilesizeExceeded)]
    [DataRow(7L, "lloWorl", CurlExitCode.Ok)]
    public async Task ExecuteAsync_RangeWithMaxFileSize_CountsOnlyTheRange(long maxFileSize, string expected, CurlExitCode exitCode)
    {
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = FileUrl, Output = output, MaxFileSize = maxFileSize, Range = ByteRange.Bounded(2, 8) };

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("max file size", maxFileSize);
        Diagnostics.Arrange("range", "2-8");

        var result = await DownloadAsync(Content, context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Diff("body", expected, Encoding.ASCII.GetString(output.ToArray()));
        Diagnostics.Assert("exit code", exitCode, result.ExitCode);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(expected), output.ToArray());
        Assert.AreEqual(exitCode, result.ExitCode);
    }

    // curl -sS -C 4 --max-filesize 5 prints oWorl and exit 63.
    [TestMethod]
    public async Task ExecuteAsync_ResumeWithMaxFileSize_CountsFromTheOffset()
    {
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = FileUrl, Output = output, MaxFileSize = 5, ResumeFrom = 4 };

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("max file size", 5);
        Diagnostics.Arrange("resume from", 4);

        var result = await DownloadAsync(Content, context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Diff("body", Encoding.ASCII.GetBytes("oWorl"), output.ToArray());
        Diagnostics.Assert("error message", "Exceeded the maximum allowed file size (5) with 5 bytes", result.ErrorMessage);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("oWorl"), output.ToArray());
        Assert.AreEqual("Exceeded the maximum allowed file size (5) with 5 bytes", result.ErrorMessage);
    }

    // curl -sS -i --max-filesize 3 prints the whole header block, then Hel, then exit 63.
    [TestMethod]
    public async Task ExecuteAsync_HeadersWithMaxFileSize_DoNotCountTowardsTheLimit()
    {
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = FileUrl, Output = output, HeaderOutput = output, MaxFileSize = 3 };

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("max file size", 3);
        Diagnostics.Arrange("headers", "written to the same stream as the body");

        var result = await DownloadAsync(Content, context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Bytes("output", output.ToArray());
        Diagnostics.Assert("bytes transferred", 3L, result.BytesTransferred);
        StringAssert.EndsWith(Encoding.ASCII.GetString(output.ToArray()), "\r\n\r\nHel");
        Assert.AreEqual("Exceeded the maximum allowed file size (3) with 3 bytes", result.ErrorMessage);
        Assert.AreEqual(3L, result.BytesTransferred);
    }

    // curl -sS -I --max-filesize 3 prints the headers and exits 0: there is no body to count.
    [TestMethod]
    public async Task ExecuteAsync_NoBodyWithMaxFileSize_Succeeds()
    {
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = FileUrl, Output = output, HeaderOutput = output, NoBody = true, MaxFileSize = 3 };

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("NoBody and max file size", "true, 3");

        var result = await DownloadAsync(Content, context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // curl -sS --max-filesize 1K on a 2048-byte file writes 1024 bytes, exit 63. Here the
    // limit falls exactly on a chunk boundary: the first chunk is written whole, and the
    // second, finding no room at all, writes nothing rather than an empty chunk.
    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeOnAChunkBoundary_WritesOneWholeChunkAndNoEmptyOne()
    {
        var output = new ChunkRecordingStream();
        byte[] content = [.. Enumerable.Repeat((byte)'y', ChunkSize * 2)];
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("content length and max file size", $"{content.Length}, {ChunkSize}");

        var result = await DownloadAsync(content, new TransferContext { Url = FileUrl, Output = output, MaxFileSize = ChunkSize });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("write lengths", string.Join(", ", output.WriteLengths));
        Diagnostics.Assert("write lengths", ChunkSize.ToString(), string.Join(", ", output.WriteLengths));
        CollectionAssert.AreEqual(new[] { ChunkSize }, output.WriteLengths.ToArray());
        Assert.AreEqual($"Exceeded the maximum allowed file size ({ChunkSize}) with {ChunkSize} bytes", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeInsideTheSecondChunk_WritesTheFirstWholeAndTheSecondUpToTheLimit()
    {
        var output = new ChunkRecordingStream();
        byte[] content = [.. Enumerable.Repeat((byte)'y', ChunkSize * 2)];
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("content length and max file size", $"{content.Length}, {ChunkSize + 7}");

        var result = await DownloadAsync(content, new TransferContext { Url = FileUrl, Output = output, MaxFileSize = ChunkSize + 7 });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("write lengths", string.Join(", ", output.WriteLengths));
        Diagnostics.Assert("write lengths", $"{ChunkSize}, 7", string.Join(", ", output.WriteLengths));
        CollectionAssert.AreEqual(new[] { ChunkSize, 7 }, output.WriteLengths.ToArray());
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
    }

    // curl -sS --max-filesize 3 -T f.txt file:///C:/rp13/up.txt writes all ten bytes, exit 0.
    [TestMethod]
    public async Task ExecuteAsync_UploadWithMaxFileSize_IgnoresTheLimit()
    {
        var fileSystem = new FakeFileSystem();
        var context = new TransferContext { Output = new ChunkRecordingStream(), Url = FileUrl, Upload = new TrackedMemoryStream(Content), MaxFileSize = 3 };

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("max file size", 3);
        Diagnostics.Arrange("upload bytes", Content.Length);

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Diff("destination", Content, fileSystem.WrittenBytes(OsPath));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static async Task<TransferResult> DownloadAsync(byte[] content, TransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, content);

        return await new FileProtocolHandler(fileSystem).ExecuteAsync(context);
    }
}
