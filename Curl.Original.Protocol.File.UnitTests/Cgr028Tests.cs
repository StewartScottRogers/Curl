using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.File.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.File;

/// <summary>
/// Pins CGR-028 (BL-3557): a <c>file://</c> source whose open reports a size of zero - a
/// procfs file, a FIFO, <c>/dev/stdin</c> from a pipe, <c>/dev/zero</c> - is read until its
/// end or until a range's byte count, not stopped at the zero size. Each case was measured
/// against curl 8.18.0 (OpenSSL, Linux) on 2026-10-26.
/// </summary>
[TestClass]
public sealed class Cgr028Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dev/stdin");

    private static string OsPath => "/dev/stdin".Replace('/', Path.DirectorySeparatorChar);

    // printf pipe-in | curl -sS file:///dev/stdin prints pipe-in, exit 0.
    [TestMethod]
    public async Task ExecuteAsync_SourceReportsZeroSize_ReadsUntilTheEnd()
    {
        byte[] content = Encoding.ASCII.GetBytes("pipe-in");
        var output = new MemoryStream();
        Diagnostics.Arrange("reported length", 0L);

        var result = await DownloadAsync(content, new TransferContext { Url = FileUrl, Output = output });

        Diagnostics.Diff("body", content, output.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(content, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(7L, result.BytesTransferred);
    }

    // curl -sS -r 0-9 -o z.bin file:///dev/zero writes 10 bytes, exit 0;
    // printf abc | curl -sS -r -1 file:///dev/stdin prints a, exit 0;
    // printf abcdef | curl -sS -r 0- file:///dev/stdin prints abcdef, exit 0.
    [TestMethod]
    [DataRow(0L, 9L, false, 10)]
    [DataRow(0L, -1L, false, 300)]
    [DataRow(1L, 0L, true, 1)]
    public async Task ExecuteAsync_RangeOnSourceReportingZeroSize_SendsTheRangesByteCount(
        long first,
        long last,
        bool suffix,
        int expectedLength)
    {
        byte[] content = new byte[300];
        ByteRange range = suffix ? ByteRange.Suffix(first) : last < 0 ? ByteRange.FromOffset(first) : ByteRange.Bounded(first, last);
        var output = new MemoryStream();
        Diagnostics.Arrange("range", range);

        var result = await DownloadAsync(content, new TransferContext { Url = FileUrl, Output = output, Range = range });

        Diagnostics.Assert("bytes written", expectedLength, output.Length);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expectedLength, output.Length);
    }

    // printf abcdef | curl -sS -C 0 file:///dev/stdin prints abcdef, exit 0.
    [TestMethod]
    public async Task ExecuteAsync_ResumeFromZeroOnSourceReportingZeroSize_ReadsUntilTheEnd()
    {
        byte[] content = Encoding.ASCII.GetBytes("abcdef");
        var output = new MemoryStream();

        var result = await DownloadAsync(
            content,
            new TransferContext { Url = FileUrl, Output = output, ResumeFrom = 0, Range = ByteRange.Bounded(0, 1) });

        Diagnostics.Diff("body", content, output.ToArray());
        CollectionAssert.AreEqual(content, output.ToArray());
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // curl -sS -m 3 --max-filesize 100 file:///dev/zero streams 100 NUL bytes, then
    // curl: (63) Exceeded the maximum allowed file size (100) with 100 bytes
    [TestMethod]
    public async Task ExecuteAsync_MaxFileSizeOnSourceReportingZeroSize_StopsAtTheLimit()
    {
        var output = new MemoryStream();

        var result = await DownloadAsync(new byte[40000], new TransferContext { Url = FileUrl, Output = output, MaxFileSize = 100 });

        Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual("Exceeded the maximum allowed file size (100) with 100 bytes", result.ErrorMessage);
        Assert.AreEqual(100L, output.Length);
    }

    private static async Task<TransferResult> DownloadAsync(byte[] content, TransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFileReadingFrom(OsPath, new NonSeekableStream(content), 0);

        return await new FileProtocolHandler(fileSystem).ExecuteAsync(context);
    }
}
