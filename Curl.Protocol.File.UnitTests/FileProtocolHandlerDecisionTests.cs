using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

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
    private const string OutputWriteFailedForHeaderBlock =
        "Failure writing output to destination, passed 90 returned 0";

    private const string ResumeFailedMessage = "failed to resume file:// transfer";

    private const string CouldNotResumeDownloadMessage = "Could not resume download";

    private const string DestinationWriteFailedMessage = "Failed sending data to the peer";

    private static Uri FileUrl => new("file:///C:/dir/f.txt");

    private static string OsPath => "C:/dir/f.txt".Replace('/', Path.DirectorySeparatorChar);

    private static byte[] Content => Encoding.ASCII.GetBytes("Hello file");

    [TestMethod]
    public async Task ExecuteAsync_Download_PassesTheContextTokenToTheOpen()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        CancellationToken token = Assert.ContainsSingle(fileSystem.OpenCancellationTokens);
        Assert.AreEqual(cancellation.Token, token);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_PassesTheContextTokenToTheOpen()
    {
        using var cancellation = new CancellationTokenSource();
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        CancellationToken token = Assert.ContainsSingle(fileSystem.OpenCancellationTokens);
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
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            HeaderOutput = headers,
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        CancellationToken headerToken = Assert.ContainsSingle(headers.WriteCancellationTokens);
        Assert.AreEqual(cancellation.Token, headerToken);
        CancellationToken bodyToken = Assert.ContainsSingle(output.WriteCancellationTokens);
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
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            HeaderOutput = FaultingStream.FailingOnWrite(1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual(OutputWriteFailedForHeaderBlock, result.ErrorMessage);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.IsEmpty(output.WriteLengths);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadSucceeds_DisposesTheDestination()
    {
        var fileSystem = new FakeFileSystem();
        var destination = new TrackedMemoryStream();
        fileSystem.WriteInto(OsPath, destination);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsTrue(destination.WasDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationWriteFails_DisposesTheDestinationAndReportsSendError()
    {
        var fileSystem = new FakeFileSystem();
        var destination = FaultingStream.FailingOnWrite(1);
        fileSystem.WriteInto(OsPath, destination);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = FaultingStream.FailingOnReadWithoutSeeking(Content, 1),
            ResumeFrom = 3,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            ResumeFrom = 3,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = upload,
            ResumeFrom = 4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = upload,
            ResumeFrom = 4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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

        var result = await DownloadAsync(
            Content,
            new FakeTransferContext
            {
                Output = output,
                ResumeFrom = 2,
                Range = ByteRange.Bounded(0, 0),
            });

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

        var result = await DownloadAsync(
            Content,
            new FakeTransferContext { Output = output, Range = ByteRange.Suffix(100) });

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(CouldNotResumeDownloadMessage, result.ErrorMessage);
        Assert.IsEmpty(output.WriteLengths);
    }

    [TestMethod]
    public async Task ExecuteAsync_FromOffsetZeroOnAnEmptyFile_SucceedsWithNoBytes()
    {
        var output = new ChunkRecordingStream();

        var result = await DownloadAsync([], new FakeTransferContext { Output = output, Range = ByteRange.FromOffset(0) });

        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedZeroToFourOnAnEmptyFile_SucceedsWithNoBytes()
    {
        var output = new ChunkRecordingStream();

        var result = await DownloadAsync([], new FakeTransferContext { Output = output, Range = ByteRange.Bounded(0, 4) });

        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeEndingPastTheFile_WritesToTheLastByte()
    {
        var output = new ChunkRecordingStream();

        var result = await DownloadAsync(Content, new FakeTransferContext { Output = output, Range = ByteRange.Bounded(2, 99) });

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

        var result = await DownloadAsync(Content, new FakeTransferContext { Output = output, Range = ByteRange.Bounded(10, 12) });

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
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

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
        var context = new FakeTransferContext { Url = FileUrl, Upload = upload };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(3, upload.ReadLengths[0]);
        CollectionAssert.AreEqual(Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    private static async Task<TransferResult> DownloadAsync(byte[] content, FakeTransferContext context)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, content);
        context.Url = FileUrl;
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }
}
