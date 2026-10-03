using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

namespace Curl.Protocol.File;

/// <summary>
/// Pins where a <c>file://</c> download refuses <c>-r</c>/<c>--range</c> text that names no
/// range (<see cref="ITransferContext.RangeText" /> set, <see cref="ITransferContext.Range" />
/// <see langword="null" />): with exit 33 after the open and after the <c>-i</c> header block,
/// never under <c>-I</c>, as curl 8.21.0's <c>file_do</c> calls <c>Curl_range</c>. Measured
/// on 2026-10-03 against the twelve-byte file <c>hello world\n</c>; the commands are in
/// BL-1334's Context. The file lives in <see cref="FakeFileSystem" />, so no disk is touched
/// and the drive-less URL holds on every platform.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerUnparsableRangeTests
{
    private const string RangeNotDelivered = "Requested range was not delivered by the server";

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///bl1334tmp/f.txt");

    private static string OsPath => "/bl1334tmp/f.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("hello world\n");

    // curl -sv -r 5-2 file:///.../f.txt: stdout empty, exit 33.
    [TestMethod]
    public async Task ExecuteAsync_RangeTextNamingNoRange_FailsWithExit33AndWritesNothing()
    {
        var output = new ChunkRecordingStream();

        var result = await DownloadAsync(new TransferContext { Url = FileUrl, Output = output, RangeText = "5-2" });

        Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode);
        Assert.AreEqual(RangeNotDelivered, result.ErrorMessage);
        Assert.IsEmpty(output.ToArray());
    }

    // curl -sv -r 5-2 file:///.../f.txt: stderr is only "* shutting down connection #0" (BL-1322):
    // the message is curl_easy_strerror's, which no failf wrote.
    [TestMethod]
    public async Task ExecuteAsync_RangeTextNamingNoRange_ReportsOnlyTheShutdownLine()
    {
        var events = new RecordingTransferEvents();

        await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), RangeText = "5-2", Events = events });

        CollectionAssert.AreEqual(new[] { "* shutting down connection #0" }, events.Transcript);
    }

    // curl -s -i -r 5-2: the three header lines and the empty line, then exit 33.
    [TestMethod]
    public async Task ExecuteAsync_RangeTextNamingNoRangeWithHeaderOutput_WritesTheHeaderBlockThenFails()
    {
        var headers = new ChunkRecordingStream();
        var output = new ChunkRecordingStream();

        var result = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = output, HeaderOutput = headers, RangeText = "5-2" });

        Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode);
        Assert.AreEqual(
            "Content-Length: 12\r\nAccept-ranges: bytes\r\nLast-Modified: Wed, 24 Jun 2026 12:34:56 GMT\r\n\r\n",
            Encoding.ASCII.GetString(headers.ToArray()));
        Assert.IsEmpty(output.ToArray());
        Assert.HasCount(3, result.Report!.PseudoHeaders);
    }

    // curl -sv -I -r 5-2: the same header block, exit 0.
    [TestMethod]
    public async Task ExecuteAsync_RangeTextNamingNoRangeWithNoBody_SucceedsWithTheHeaderBlock()
    {
        var headers = new ChunkRecordingStream();

        var result = await DownloadAsync(
            new TransferContext
            {
                Url = FileUrl,
                Output = new ChunkRecordingStream(),
                HeaderOutput = headers,
                NoBody = true,
                RangeText = "5-2",
            });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(
            "Content-Length: 12\r\nAccept-ranges: bytes\r\nLast-Modified: Wed, 24 Jun 2026 12:34:56 GMT\r\n\r\n",
            Encoding.ASCII.GetString(headers.ToArray()));
    }

    // curl -v -r 5-2 file:///.../nonexist.txt: exit 37, Could not open file.
    [TestMethod]
    public async Task ExecuteAsync_RangeTextNamingNoRangeForMissingFile_FailsWithExit37()
    {
        var handler = new FileProtocolHandler(new FakeFileSystem());

        var result = await handler.ExecuteAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), RangeText = "5-2" });

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file /bl1334tmp/f.txt", result.ErrorMessage);
    }

    // curl_range.c refuses both with CURLE_RANGE_ERROR.
    [TestMethod]
    [DataRow("abc")]
    [DataRow("-0")]
    public async Task ExecuteAsync_OtherRangeTextNamingNoRange_FailsWithExit33(string rangeText)
    {
        var result = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = new ChunkRecordingStream(), RangeText = rangeText });

        Assert.AreEqual(CurlExitCode.RangeError, result.ExitCode);
        Assert.AreEqual(RangeNotDelivered, result.ErrorMessage);
    }

    // With Range parsed, RangeText is only its source text and the window is sent as before.
    [TestMethod]
    public async Task ExecuteAsync_RangeTextWithParsedRange_SendsTheWindow()
    {
        var output = new ChunkRecordingStream();

        var result = await DownloadAsync(
            new TransferContext { Url = FileUrl, Output = output, RangeText = "0-4", Range = ByteRange.Bounded(0, 4) });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
    }

    // An upload ignores -r, text that names no range included.
    [TestMethod]
    public async Task ExecuteAsync_UploadWithRangeTextNamingNoRange_IgnoresIt()
    {
        var fileSystem = new FakeFileSystem();
        var destination = new ChunkRecordingStream();
        fileSystem.WriteInto(OsPath, destination);
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(
            new TransferContext
            {
                Url = FileUrl,
                Output = new ChunkRecordingStream(),
                Upload = new MemoryStream(Content),
                RangeText = "5-2",
            });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Content, destination.ToArray());
    }

    private static async Task<TransferResult> DownloadAsync(TransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content, FakeFileSystem.DefaultLastWriteTimeUtc);
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }
}
