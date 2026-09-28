using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

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
        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream() });

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

        var result = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), HeaderOutput = headers });

        Assert.IsNotEmpty(headers.ToArray());
        CollectionAssert.AreEqual(ExpectedPseudoHeaders, result.Report!.PseudoHeaders.ToArray());
        Assert.IsEmpty(result.Report.ResponseHeaders);
    }

    // -I printed [][3]; -r 0-2 printed [3].
    [TestMethod]
    public async Task ExecuteAsync_HeadOnlyOrRange_ReportsThreePseudoHeaders()
    {
        var headOnly = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), NoBody = true });
        var ranged = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Range = ByteRange.Bounded(0, 2) });

        Assert.HasCount(3, headOnly.Report!.PseudoHeaders);
        Assert.AreEqual(0L, headOnly.Report.DownloadSize);
        Assert.HasCount(3, ranged.Report!.PseudoHeaders);
        Assert.AreEqual(3L, ranged.Report.DownloadSize);
    }

    // -C 100 past the end exited 36 and printed [3]: the headers came before the failure.
    [TestMethod]
    public async Task ExecuteAsync_ResumePastTheEnd_FailsButStillReportsThreePseudoHeaders()
    {
        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), ResumeFrom = 100 });

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

        var result = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), TimeCondition = condition });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    // A missing file exited 37 and printed [0].
    [TestMethod]
    public async Task ExecuteAsync_MissingFile_ReportsNoHeaders()
    {
        var handler = new FileProtocolHandler(new FakeFileSystem());

        var result = await handler.ExecuteAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream() });

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

        var result = await handler.ExecuteAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), Upload = new MemoryStream(Content) });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    // Not measurable on Windows (see FileTransferMessages.PseudoHeaderLines): without a
    // timestamp the Last-Modified line is not written, so it is not reported either.
    [TestMethod]
    public async Task ExecuteAsync_UnknownTimestamp_ReportsTheTwoLinesWritten()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFileWithoutTimestamp(OsPath, Content);
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream() });

        CollectionAssert.AreEqual(ExpectedPseudoHeaders[..2], result.Report!.PseudoHeaders.ToArray());
    }

    // A header output that fails ends the transfer with exit 23 before the header block is
    // complete; it reports none, the choice recorded in BL-285's Notes.
    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputFails_ReportsNoHeaders()
    {
        var result = await DownloadAsync(
            new TransferContext
            {
                Url = FileUrl,
                Output = new ChunkRecordingStream(),
                HeaderOutput = FaultingStream.FailingOnWrite(1),
            });

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.IsNull(result.Report);
    }

    private static async Task<TransferResult> DownloadAsync(TransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content, FakeFileSystem.DefaultLastWriteTimeUtc);
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }
}
