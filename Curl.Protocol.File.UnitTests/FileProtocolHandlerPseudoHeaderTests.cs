using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

namespace Curl.Protocol.File;

/// <summary>
/// Pins the pseudo-headers a <c>file://</c> download reports, the source of
/// <c>%{num_headers}</c>. Measured against curl 8.21.0 (mingw, Schannel) on 2026-09-26 with
/// the twelve-byte file <c>hello world\n</c>; the commands are in BL-285's Notes. curl counts
/// the three header lines whenever they were produced, <c>-D</c> or not, yet
/// <c>%header{Content-Length}</c> prints nothing, because curl's header API never holds them:
/// so they are reported as <see cref="TransferReport.PseudoHeaders" /> and never as
/// <see cref="TransferReport.ResponseHeaders" />.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerPseudoHeaderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static readonly KeyValuePair<string, string>[] ExpectedPseudoHeaders =
    [
        new("Content-Length", "12"),
        new("Accept-ranges", "bytes"),
        new("Last-Modified", "Wed, 24 Jun 2026 12:34:56 GMT"),
    ];

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///bl285tmp/a.txt");

    private static string OsPath => "/bl285tmp/a.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("hello world\n");

    // curl -s -o NUL -w "[%header{Content-Length}][%{num_headers}]" file:///C:/bl285tmp/a.txt
    // printed [][3].
    [TestMethod]
    public async Task ExecuteAsync_DownloadWithoutHeaderOutput_ReportsThreePseudoHeadersAndNoResponseHeaders()
    {
        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("file", "/bl285tmp/a.txt, 12 bytes");

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream() });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("download size", 12L, result.Report?.DownloadSize);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNotNull(result.Report);
        CollectionAssert.AreEqual(ExpectedPseudoHeaders, result.Report.PseudoHeaders.ToArray());
        Assert.IsEmpty(result.Report.ResponseHeaders);
        Assert.AreEqual(12L, result.Report.DownloadSize);
    }

    // The same with -D - wrote the three lines and still printed [][][][3].
    [TestMethod]
    public async Task ExecuteAsync_DownloadWithHeaderOutput_ReportsTheLinesItWrote()
    {
        var headers = new ChunkRecordingStream();
        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("header output", "requested");

        var result = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), HeaderOutput = headers });

        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Bytes("header output", headers.ToArray());
        Diagnostics.Assert("pseudo-headers", DescribeExpected(ExpectedPseudoHeaders), DescribePseudoHeaders(result));
        Assert.IsNotEmpty(headers.ToArray());
        CollectionAssert.AreEqual(ExpectedPseudoHeaders, result.Report!.PseudoHeaders.ToArray());
        Assert.IsEmpty(result.Report.ResponseHeaders);
    }

    // -I printed [][3]; -r 0-2 printed [3].
    [TestMethod]
    public async Task ExecuteAsync_HeadOnlyOrRange_ReportsThreePseudoHeaders()
    {
        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("transfers", "head only, and range 0-2");

        var headOnly = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), NoBody = true });
        var ranged = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Range = ByteRange.Bounded(0, 2) });

        Diagnostics.Act("head only pseudo-headers", DescribePseudoHeaders(headOnly));
        Diagnostics.Act("ranged pseudo-headers", DescribePseudoHeaders(ranged));
        Diagnostics.Assert("head only download size", 0L, headOnly.Report!.DownloadSize);
        Diagnostics.Assert("ranged download size", 3L, ranged.Report!.DownloadSize);
        Assert.HasCount(3, headOnly.Report!.PseudoHeaders);
        Assert.AreEqual(0L, headOnly.Report.DownloadSize);
        Assert.HasCount(3, ranged.Report!.PseudoHeaders);
        Assert.AreEqual(3L, ranged.Report.DownloadSize);
    }

    // -C 100 past the end exited 36 and printed [3]: the headers came before the failure.
    [TestMethod]
    public async Task ExecuteAsync_ResumePastTheEnd_FailsButStillReportsThreePseudoHeaders()
    {
        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("resume from", 100);

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), ResumeFrom = 100 });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Assert("exit code", CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.HasCount(3, result.Report!.PseudoHeaders);
    }

    // An unmet -z "Sat, 01 Jan 2099 00:00:00 GMT" exited 0 and printed [0].
    [TestMethod]
    public async Task ExecuteAsync_TimeConditionNotMet_ReportsNoHeaders()
    {
        var condition = new TimeCondition(
            new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TimeConditionKind.IfModifiedSince);
        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("time condition", "if modified since 2099-01-01");

        var result = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), TimeCondition = condition });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    // A missing file exited 37 and printed [0].
    [TestMethod]
    public async Task ExecuteAsync_MissingFile_ReportsNoHeaders()
    {
        var handler = new FileProtocolHandler(new FakeFileSystem());

        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("file system", "empty");

        var result = await handler.ExecuteAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream() });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Assert("exit code", CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    // -T a.txt file:///C:/bl285tmp/b.txt printed [0].
    [TestMethod]
    public async Task ExecuteAsync_Upload_ReportsNoHeaders()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.WriteInto(OsPath, new ChunkRecordingStream());
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("upload bytes", Content.Length);

        var result = await handler.ExecuteAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Upload = new MemoryStream(Content) });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    // Not measurable on Windows (see FileTransferMessages.PseudoHeaders): without a
    // timestamp the Last-Modified line is not written, so it is not reported either.
    [TestMethod]
    public async Task ExecuteAsync_UnknownTimestamp_ReportsTheTwoLinesWritten()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFileWithoutTimestamp(OsPath, Content);
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("file", "/bl285tmp/a.txt, 12 bytes, no timestamp");

        var result = await handler.ExecuteAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream() });

        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Assert("pseudo-headers", DescribeExpected(ExpectedPseudoHeaders[..2]), DescribePseudoHeaders(result));
        CollectionAssert.AreEqual(ExpectedPseudoHeaders[..2], result.Report!.PseudoHeaders.ToArray());
    }

    // A header output that fails ends the transfer with exit 23 before the header block is
    // complete; it reports none, the choice recorded in BL-285's Notes.
    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputFails_ReportsNoHeaders()
    {
        Diagnostics.Arrange("url", "file:///bl285tmp/a.txt");
        Diagnostics.Arrange("header output", "fails on write 1");

        var result = await DownloadAsync(
            new TransferContext
            {
                Url = FileUrl,
                Output = new ChunkRecordingStream(),
                HeaderOutput = FaultingStream.FailingOnWrite(1),
            });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("pseudo-headers", DescribePseudoHeaders(result));
        Diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    private static string DescribePseudoHeaders(TransferResult result) =>
        result.Report is null ? "(no report)" : DescribeExpected(result.Report.PseudoHeaders);

    private static string DescribeExpected(IEnumerable<KeyValuePair<string, string>> headers) =>
        string.Join(" | ", headers.Select(header => $"{header.Key}: {header.Value}"));

    private static async Task<TransferResult> DownloadAsync(TransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content, FakeFileSystem.DefaultLastWriteTimeUtc);
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }
}
