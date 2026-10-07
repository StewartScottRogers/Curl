using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

namespace Curl.Protocol.File;

/// <summary>
/// Pins the decisions in <see cref="FileProtocolHandler" /> that the BL-008 re-review found
/// no test reaching: which cancellation token each call is given, which streams are
/// disposed on which path, how a short read is told apart from the end, and the range and
/// resume boundaries. Each test here fails against a one-line change to the handler that
/// every test in <see cref="FileProtocolHandlerTests" /> survives.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerDecisionTests
{
    private const string HeaderWriteFailedForFirstLine =
        "client returned ERROR on write of 20 bytes";

    private const string ResumeFailedMessage = "failed to resume file:// transfer";

    private const string CouldNotResumeDownloadMessage = "Could not resume download";

    private const string DestinationWriteFailedMessage = "Failed sending data to the peer";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static CurlUrl FileUrl => CurlUrl.Parse("file:///dir/f.txt");

    private static string OsPath => "/dir/f.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("Hello file");

    [TestMethod]
    public async Task ExecuteAsync_Download_PassesTheContextTokenToTheOpen()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("fake file /dir/f.txt bytes", Content.Length);

        await handler.ExecuteAsync(context);

        Diagnostics.Act("open token count", fileSystem.OpenCancellationTokens.Count);
        CancellationToken token = Assert.ContainsSingle(fileSystem.OpenCancellationTokens);
        Diagnostics.Assert("open token", cancellation.Token, token);
        Assert.AreEqual(cancellation.Token, token);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_PassesTheContextTokenToTheOpen()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeFileSystem();
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("upload bytes", Content.Length);

        await handler.ExecuteAsync(context);

        Diagnostics.Act("open token count", fileSystem.OpenCancellationTokens.Count);
        CancellationToken token = Assert.ContainsSingle(fileSystem.OpenCancellationTokens);
        Diagnostics.Assert("open token", cancellation.Token, token);
        Assert.AreEqual(cancellation.Token, token);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputRequested_PassesTheContextTokenToTheHeaderWrite()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var headers = new ChunkRecordingStream();
        var output = new ChunkRecordingStream();
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = output,
            HeaderOutput = headers,
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("fake file /dir/f.txt bytes", Content.Length);

        await handler.ExecuteAsync(context);

        Diagnostics.Act("header write token count", headers.WriteCancellationTokens.Count);
        Diagnostics.Act("body write token count", output.WriteCancellationTokens.Count);
        Diagnostics.Assert("header write token count", 4, headers.WriteCancellationTokens.Count);
        Assert.HasCount(4, headers.WriteCancellationTokens);
        Assert.IsTrue(headers.WriteCancellationTokens.All(token => token == cancellation.Token));
        CancellationToken bodyToken = Assert.ContainsSingle(output.WriteCancellationTokens);
        Diagnostics.Assert("body write token", cancellation.Token, bodyToken);
        Assert.AreEqual(cancellation.Token, bodyToken);
    }

    // The header block is written before the body, so a header write that fails ends the
    // transfer there: exit 23 with the output-write line, and not one body byte.
    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputFails_WritesNoBodyAndReportsWriteError()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = output,
            HeaderOutput = FaultingStream.FailingOnWrite(1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("header output", "fails on write 1");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("exit code", CurlExitCode.WriteError, result.ExitCode);
        Diagnostics.Assert("error message", HeaderWriteFailedForFirstLine, result.ErrorMessage);
        Diagnostics.Assert("body write count", 0, output.WriteLengths.Count);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual(HeaderWriteFailedForFirstLine, result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsEmpty(output.WriteLengths);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputFails_ReportsTheWriteErrorAsAnInformationLine()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            HeaderOutput = FaultingStream.FailingOnWrite(1),
            Events = events,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("header output", "fails on write 1");

        await handler.ExecuteAsync(context);

        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert(
            "info lines",
            HeaderWriteFailedForFirstLine + " | closing connection #0",
            string.Join(" | ", events.Info));

        // Exit 23 is premature to libcurl's multi_done, so the connection line that follows
        // is "closing", not "shutting down" (BL-936).
        CollectionAssert.AreEqual(
            new[] { HeaderWriteFailedForFirstLine, "closing connection #0" },
            events.Info);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadSucceeds_DisposesTheDestination()
    {
        var fileSystem = new FakeFileSystem();
        var destination = new TrackedMemoryStream();
        fileSystem.WriteInto(OsPath, destination);
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("upload bytes", Content.Length);

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("destination disposed", destination.WasDisposed);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("destination disposed", true, destination.WasDisposed);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(destination.WasDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationWriteFails_DisposesTheDestinationAndReportsSendError()
    {
        var fileSystem = new FakeFileSystem();
        var destination = FaultingStream.FailingOnWrite(1);
        fileSystem.WriteInto(OsPath, destination);
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("destination", "fails on write 1");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Assert("error message", DestinationWriteFailedMessage, result.ErrorMessage);
        Diagnostics.Assert("destination disposed", true, destination.WasDisposed);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(DestinationWriteFailedMessage, result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsTrue(destination.WasDisposed);
    }

    // A read that fails while -C is being skipped on standard input ends the upload as a
    // success with nothing sent, and the destination it had already opened is still closed.
    [TestMethod]
    public async Task ExecuteAsync_UploadSkipFails_DisposesTheDestination()
    {
        var fileSystem = new FakeFileSystem();
        var destination = new TrackedMemoryStream();
        fileSystem.WriteInto(OsPath, destination);
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = FaultingStream.FailingOnReadWithoutSeeking(Content, 1),
            ResumeFrom = 3,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("resume from", 3);

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Bytes("destination", destination.ToArray());
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("bytes transferred", 0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsEmpty(destination.ToArray());
        Assert.IsTrue(destination.WasDisposed);
    }

    // A stream that faults its own read with TaskCanceledException while the caller's token
    // is untouched has failed, not been cancelled: the download ends there, exit 0.
    [TestMethod]
    public async Task ExecuteAsync_SourceFaultsCancelledWithAnUncancelledToken_EndsTheBodyAndSucceeds()
    {
        var fileSystem = new FakeFileSystem();
        var source = CancellingStream.Reading(Content, 1, StreamCancellationStyle.FaultedValueTask, null);
        fileSystem.AddFileReadingFrom(OsPath, source, Content.Length);
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("source", "read 1 faults as TaskCanceledException, token not cancelled");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("body write count", 0, output.WriteLengths.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsEmpty(output.WriteLengths);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationFaultsCancelledWithAnUncancelledToken_ReportsSendError()
    {
        var fileSystem = new FakeFileSystem();
        var destination = CancellingStream.Writing(1, StreamCancellationStyle.FaultedValueTask, null);
        fileSystem.WriteInto(OsPath, destination);
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("destination", "write 1 faults as TaskCanceledException, token not cancelled");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("exit code", CurlExitCode.SendError, result.ExitCode);
        Diagnostics.Assert("error message", DestinationWriteFailedMessage, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode);
        Assert.AreEqual(DestinationWriteFailedMessage, result.ErrorMessage);
        Assert.IsTrue(destination.WasDisposed);
    }

    // -C 3 against a destination already holding the first three bytes is the resume the
    // option exists for: the destination ends up whole, and only the remainder was sent.
    [TestMethod]
    public async Task ExecuteAsync_UploadWithPositiveResumeFromIntoAPartialDestination_CompletesTheDestination()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Encoding.ASCII.GetBytes("Hel"));
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            ResumeFrom = 3,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("existing destination", "Hel");
        Diagnostics.Arrange("resume from", 3);

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Diff("destination", Content, fileSystem.WrittenBytes(OsPath));
        Diagnostics.Assert("bytes transferred", 7L, result.BytesTransferred);
        CollectionAssert.AreEqual(Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(7L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Seeking skips the offset without reading it, so the copy that follows makes exactly
    // two reads: the remaining six bytes, then the end.
    [TestMethod]
    public async Task ExecuteAsync_UploadWithResumeFromAndASeekableSource_AppendsAndSeeksPastTheOffset()
    {
        var fileSystem = new FakeFileSystem();
        var upload = CancellingStream.Reading(Content, 0, StreamCancellationStyle.None, null);
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = upload,
            ResumeFrom = 4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("resume from", 4);
        Diagnostics.Arrange("upload", "seekable, 10 bytes");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("upload read count", upload.ReadCount);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("upload read count", 2, upload.ReadCount);
        Diagnostics.Diff("destination", Encoding.ASCII.GetBytes("o file"), fileSystem.WrittenBytes(OsPath));
        var call = Assert.ContainsSingle(fileSystem.Calls);
        Assert.AreEqual(FileSystemCall.Write(OsPath, FileWriteMode.Append), call);
        Assert.AreEqual(2, upload.ReadCount);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("o file"), fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(6L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Standard input cannot be seeked, so the offset is consumed by one read of four bytes
    // before the copy's two.
    [TestMethod]
    public async Task ExecuteAsync_UploadWithResumeFromAndANonSeekableSource_SkipsByReadingAndWritesTheRest()
    {
        var fileSystem = new FakeFileSystem();
        var upload = CancellingStream.ReadingNonSeekable(Content, 0, StreamCancellationStyle.None, null);
        var context = new TransferContext
        {
            Output = new ChunkRecordingStream(),
            Url = FileUrl,
            Upload = upload,
            ResumeFrom = 4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("resume from", 4);
        Diagnostics.Arrange("upload", "non-seekable, 10 bytes");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("upload read count", upload.ReadCount);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("upload read count", 3, upload.ReadCount);
        Diagnostics.Diff("destination", Encoding.ASCII.GetBytes("o file"), fileSystem.WrittenBytes(OsPath));
        var call = Assert.ContainsSingle(fileSystem.Calls);
        Assert.AreEqual(FileSystemCall.Write(OsPath, FileWriteMode.Append), call);
        Assert.AreEqual(3, upload.ReadCount);
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("o file"), fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(6L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // NOT upstream conformance: the command line refuses -C with -r (exit 2, BL-013), so no
    // curl invocation reaches this. It pins TryResolveWindow's documented precedence for a
    // context built by hand - ResumeFrom wins and Range is ignored.
    [TestMethod]
    public async Task ExecuteAsync_ResumeFromAndRangeTogetherUnreachableFromTheCommandLine_ResumeFromWins()
    {
        var output = new ChunkRecordingStream();
        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("resume from", 2);
        Diagnostics.Arrange("range", "0-0");

        var result = await DownloadAsync(
            Content,
            new TransferContext
            {
                Url = FileUrl,
                Output = output,
                ResumeFrom = 2,
                Range = ByteRange.Bounded(0, 0),
            });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Diff("body", Encoding.ASCII.GetBytes("llo file"), output.ToArray());
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("llo file"), output.ToArray());
        Assert.AreEqual(8L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Measured on curl 8.21.0 (see FileProtocolHandler's remarks): a suffix range fails once
    // it asks for more than one byte beyond the length, so -r -100 of ten bytes is exit 36
    // with "Could not resume download", not the whole file.
    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeFarLongerThanTheFile_ReportsCouldNotResumeDownload()
    {
        var output = new ChunkRecordingStream();

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("range", "suffix 100 of 10 bytes");

        var result = await DownloadAsync(
            Content,
            new TransferContext { Url = FileUrl, Output = output, Range = ByteRange.Suffix(100) });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("exit code", CurlExitCode.BadDownloadResume, result.ExitCode);
        Diagnostics.Assert("error message", CouldNotResumeDownloadMessage, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(CouldNotResumeDownloadMessage, result.ErrorMessage);
        Assert.IsEmpty(output.WriteLengths);
    }

    [TestMethod]
    public async Task ExecuteAsync_FromOffsetZeroOnAnEmptyFile_SucceedsWithNoBytes()
    {
        var output = new ChunkRecordingStream();

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("range", "from offset 0 of an empty file");

        var result = await DownloadAsync([], new TransferContext { Url = FileUrl, Output = output, Range = ByteRange.FromOffset(0) });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("bytes transferred", 0L, result.BytesTransferred);
        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedZeroToFourOnAnEmptyFile_SucceedsWithNoBytes()
    {
        var output = new ChunkRecordingStream();

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("range", "0-4 of an empty file");

        var result = await DownloadAsync([], new TransferContext { Url = FileUrl, Output = output, Range = ByteRange.Bounded(0, 4) });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("bytes transferred", 0L, result.BytesTransferred);
        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeEndingPastTheFile_WritesToTheLastByte()
    {
        var output = new ChunkRecordingStream();

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("range", "2-99 of 10 bytes");

        var result = await DownloadAsync(Content, new TransferContext { Url = FileUrl, Output = output, Range = ByteRange.Bounded(2, 99) });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Diff("body", Encoding.ASCII.GetBytes("llo file"), output.ToArray());
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("llo file"), output.ToArray());
        Assert.AreEqual(8L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // A start equal to the length is the boundary: equal is a success with nothing sent,
    // one past it is exit 36 (ExecuteAsync_RangeStartPastTheEnd_ReportsFailedToResumeTransfer).
    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeStartingAtTheLength_SucceedsWithNoBytes()
    {
        var output = new ChunkRecordingStream();

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("range", "10-12 of 10 bytes");

        var result = await DownloadAsync(Content, new TransferContext { Url = FileUrl, Output = output, Range = ByteRange.Bounded(10, 12) });

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage);
        Diagnostics.Assert("bytes transferred", 0L, result.BytesTransferred);
        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreNotEqual(ResumeFailedMessage, result.ErrorMessage);
    }

    // A pipe or a character device returns fewer bytes than asked for without being at its
    // end; only a read of zero is the end. The first read returning three proves the short
    // read happened, and the whole file arriving proves it was not taken for the end.
    [TestMethod]
    public async Task ExecuteAsync_SourceReturnsShortReads_StillWritesTheWholeFile()
    {
        var fileSystem = new FakeFileSystem();
        var source = new ShortReadStream(Content, 3);
        fileSystem.AddFileReadingFrom(OsPath, source, Content.Length);
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("source", "short reads of 3 bytes, 10 bytes total");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("first read length", source.ReadLengths[0]);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("first read length", 3, source.ReadLengths[0]);
        Diagnostics.Diff("body", Content, output.ToArray());
        Assert.AreEqual(3, source.ReadLengths[0]);
        CollectionAssert.AreEqual(Content, output.ToArray());
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadSourceReturnsShortReads_StillWritesTheWholeSource()
    {
        var fileSystem = new FakeFileSystem();
        var upload = new ShortReadStream(Content, 3);
        var context = new TransferContext { Output = new ChunkRecordingStream(), Url = FileUrl, Upload = upload };
        var handler = new FileProtocolHandler(fileSystem);

        Diagnostics.Arrange("url", "file:///dir/f.txt");
        Diagnostics.Arrange("upload", "short reads of 3 bytes, 10 bytes total");

        var result = await handler.ExecuteAsync(context);

        Diagnostics.Act("first read length", upload.ReadLengths[0]);
        Diagnostics.Act("bytes transferred", result.BytesTransferred);
        Diagnostics.Assert("first read length", 3, upload.ReadLengths[0]);
        Diagnostics.Diff("destination", Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(3, upload.ReadLengths[0]);
        CollectionAssert.AreEqual(Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static async Task<TransferResult> DownloadAsync(byte[] content, TransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, content);
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }
}
