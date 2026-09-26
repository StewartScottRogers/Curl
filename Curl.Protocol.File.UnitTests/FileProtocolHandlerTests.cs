using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;

namespace Curl.Protocol.File;

/// <summary>
/// Pins the <c>file://</c> transfer against curl 8.21.0: the bytes moved, the calls made
/// to the file system, the synthesised headers and the exit codes. Nothing here touches a
/// disk — the whole of the handler's behaviour is observable through
/// <see cref="FakeFileSystem.Calls" /> and the fake streams.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerTests
{
    /// <summary>
    /// The path as written in the URL, still percent-encoded. curl's exit 37 message
    /// echoes this form, not the decoded one.
    /// </summary>
    private const string EncodedUrlPath = "C:/dir/my%20file.txt";

    /// <summary>
    /// The chunk size curl uses for a <c>file://</c> body, measured at 16 kilobytes.
    /// </summary>
    private const int ChunkSize = 16384;

    private const string ExpectedHeaders =
        "Content-Length: 10\r\n"
        + "Accept-ranges: bytes\r\n"
        + "Last-Modified: Wed, 24 Jun 2026 12:34:56 GMT\r\n"
        + "\r\n";

    /// <summary>
    /// The exit 36 message for a resume offset or a range start past the end of the file.
    /// </summary>
    private const string ResumeFailedMessage = "failed to resume file:// transfer";

    /// <summary>
    /// The exit 36 message for a suffix range asking for more trailing bytes than the file
    /// can bear. curl 8.21.0 prints this and not <see cref="ResumeFailedMessage" /> for
    /// that one case, so every exit 36 test below asserts which of the two it got and that
    /// it did not get the other: collapsing them would be invisible otherwise.
    /// </summary>
    private const string CouldNotResumeDownloadMessage = "Could not resume download";

    /// <summary>
    /// The exit 26 message for a source that opened and then failed to be read.
    /// </summary>
    private const string ReadFailedMessage =
        "Failed to open/read local data from file/application";

    /// <summary>
    /// The exit 23 message for an upload destination that opened and then failed to be
    /// written to.
    /// </summary>
    private const string DestinationWriteFailedMessage =
        "Failed writing received data to disk/application";

    private static Uri FileUrl => new("file:///C:/dir/my%20file.txt");

    private static string OsPath => NativePath("C:/dir/my file.txt");

    private static byte[] Content => Encoding.ASCII.GetBytes("Hello file");

    [TestMethod]
    public void SupportedSchemes_Always_IsExactlyTheLowercaseFileScheme()
    {
        var handler = new FileProtocolHandler(new FakeFileSystem());

        var schemes = handler.SupportedSchemes;

        string scheme = Assert.ContainsSingle(schemes);
        Assert.AreEqual("file", scheme);
    }

    [TestMethod]
    public void Constructor_NullFileSystem_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new FileProtocolHandler(null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_NullContext_ThrowsArgumentNullException()
    {
        var handler = new FileProtocolHandler(new FakeFileSystem());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => handler.ExecuteAsync(null!).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_ExistingFile_WritesTheFileToTheOutputAndSucceeds()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Content, output.ToArray());
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ZeroByteFile_SucceedsWithoutWriting()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, []);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // curl reads a file:// body in 16 kilobyte chunks; the first write pins the size.
    [TestMethod]
    public async Task ExecuteAsync_FileLargerThanOneChunk_WritesAFullChunkFirst()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, LargeContent());
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsNotEmpty(output.WriteLengths);
        Assert.AreEqual(ChunkSize, output.WriteLengths[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_FileLargerThanOneChunk_WritesEveryByteInOrder()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = LargeContent();
        fileSystem.AddFile(OsPath, content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(content, output.ToArray());
        Assert.AreEqual((long)content.Length, result.BytesTransferred);
    }

    // There is no text handling anywhere in this path: a null byte and an invalid UTF-8
    // sequence come out exactly as they went in.
    [TestMethod]
    public async Task ExecuteAsync_BinaryFile_RoundTripsTheBytesUnchanged()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = [0x00, 0xFF, 0xFE, 0x00, 0x80, 0xC3, 0x28, 0x00, 0xED, 0xA0, 0x80];
        fileSystem.AddFile(OsPath, content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(content, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceNotFound_ReportsExitThirtySevenWithTheEncodedPath()
    {
        var result = await ReadFailureResultAsync(FileAccessStatus.NotFound);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual($"Could not open file {EncodedUrlPath}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceIsDirectory_ReportsExitThirtySevenWithTheEncodedPath()
    {
        var result = await ReadFailureResultAsync(FileAccessStatus.IsDirectory);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual($"Could not open file {EncodedUrlPath}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceAccessDenied_ReportsExitThirtySevenWithTheEncodedPath()
    {
        var result = await ReadFailureResultAsync(FileAccessStatus.AccessDenied);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual($"Could not open file {EncodedUrlPath}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceIoError_ReportsExitThirtySevenWithTheEncodedPath()
    {
        var result = await ReadFailureResultAsync(FileAccessStatus.IoError);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual($"Could not open file {EncodedUrlPath}", result.ErrorMessage);
    }

    // Exit 78 is the remote-protocol "file not found"; file:// never reports it, however
    // the open failed. Verified against curl 8.21.0.
    [TestMethod]
    [DataRow(FileAccessStatus.NotFound)]
    [DataRow(FileAccessStatus.IsDirectory)]
    [DataRow(FileAccessStatus.AccessDenied)]
    [DataRow(FileAccessStatus.IoError)]
    public async Task ExecuteAsync_SourceOpenFails_NeverReportsRemoteFileNotFound(
        FileAccessStatus status)
    {
        var result = await ReadFailureResultAsync(status);

        Assert.AreNotEqual(CurlExitCode.RemoteFileNotFound, result.ExitCode);
    }

    // Likewise exit 9: a local permission failure is still exit 37, not remote access
    // denied.
    [TestMethod]
    [DataRow(FileAccessStatus.NotFound)]
    [DataRow(FileAccessStatus.IsDirectory)]
    [DataRow(FileAccessStatus.AccessDenied)]
    [DataRow(FileAccessStatus.IoError)]
    public async Task ExecuteAsync_SourceOpenFails_NeverReportsRemoteAccessDenied(
        FileAccessStatus status)
    {
        var result = await ReadFailureResultAsync(status);

        Assert.AreNotEqual(CurlExitCode.RemoteAccessDenied, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RejectedHost_ReportsUrlMalformat()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext { Url = new Uri("file://example.com/x") };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.AreEqual("URL rejected: Bad file:// URL", result.ErrorMessage);
    }

    // A host curl will not accept has to be refused before any open: an unsupported URL
    // must not turn into a file system access.
    [TestMethod]
    public async Task ExecuteAsync_RejectedHost_NeverTouchesTheFileSystem()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext { Url = new Uri("file://example.com/x") };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_OpensTheDestinationTruncatedExactlyOnce()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        var call = Assert.ContainsSingle(fileSystem.Calls);
        Assert.AreEqual(FileSystemCall.Write(OsPath, FileWriteMode.Truncate), call);
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_WritesTheSourceBytesToTheDestination()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // -T - uploads from standard input, which cannot be seeked or measured.
    [TestMethod]
    public async Task ExecuteAsync_UploadFromNonSeekableSource_Succeeds()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new NonSeekableStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationNotFound_ReportsWriteError()
    {
        var result = await WriteFailureResultAsync(FileAccessStatus.NotFound);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"cannot open {OsPath} for writing", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationIsDirectory_ReportsWriteError()
    {
        var result = await WriteFailureResultAsync(FileAccessStatus.IsDirectory);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"cannot open {OsPath} for writing", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationAccessDenied_ReportsWriteError()
    {
        var result = await WriteFailureResultAsync(FileAccessStatus.AccessDenied);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"cannot open {OsPath} for writing", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationIoError_ReportsWriteError()
    {
        var result = await WriteFailureResultAsync(FileAccessStatus.IoError);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual($"cannot open {OsPath} for writing", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputStreamFails_ReportsWriteError()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = FaultingStream.FailingOnWrite(1);
        var context = new FakeTransferContext { Url = FileUrl, Output = output };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual("Failure writing output to destination", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceReadFails_ReportsReadError()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = Content;
        fileSystem.AddFileReadingFrom(OsPath, FaultingStream.FailingOnRead(content, 1), content.Length);
        var context = new FakeTransferContext { Url = FileUrl, Output = new ChunkRecordingStream() };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadSourceReadFails_ReportsReadError()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = FaultingStream.FailingOnRead(Content, 1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExistingFile_DisposesTheSourceStream()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext { Url = FileUrl, Output = new ChunkRecordingStream() };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsTrue(fileSystem.ReadStreamFor(OsPath).WasDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceReadFails_DisposesTheSourceStream()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = Content;
        var source = FaultingStream.FailingOnRead(content, 1);
        fileSystem.AddFileReadingFrom(OsPath, source, content.Length);
        var context = new FakeTransferContext { Url = FileUrl, Output = new ChunkRecordingStream() };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsTrue(source.WasDisposed);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputStreamFails_DisposesTheSourceStream()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = FaultingStream.FailingOnWrite(1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsTrue(fileSystem.ReadStreamFor(OsPath).WasDisposed);
    }

    // The exception alone proves nothing about the guard at the top of ExecuteAsync: with
    // that guard deleted the open would succeed and the copy loop would throw the same
    // exception a line later. What pins it is that nothing was opened - the fake file
    // system records every call and no longer honours the token itself, so an empty Calls
    // is the handler's refusal rather than the fake's.
    [TestMethod]
    public async Task ExecuteAsync_PreCancelledToken_ThrowsOperationCanceledExceptionBeforeOpening()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());

        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreCancelledTokenOnAnUpload_ThrowsOperationCanceledExceptionBeforeOpening()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());

        Assert.IsEmpty(fileSystem.Calls);
    }

    // Cancellation arriving in the middle of the body, which is where a user's interrupt
    // arrives. Neither stream here inspects the token, so the only thing that can end this
    // transfer is the handler's own ThrowIfCancellationRequested at the top of the copy
    // loop: delete that guard and the download runs to completion and exits 0.
    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledMidDownloadBody_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        byte[] content = LargeContent();
        var source = CancellingStream.Reading(
            content,
            triggerOperationNumber: 1,
            StreamCancellationStyle.None,
            cancellation);
        fileSystem.AddFileReadingFrom(OsPath, source, content.Length);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = CancellingStream.Writing(0, StreamCancellationStyle.None, null),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());

        Assert.AreEqual(1, source.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledMidUploadBody_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        fileSystem.WriteInto(
            OsPath,
            CancellingStream.Writing(0, StreamCancellationStyle.None, null));
        var upload = CancellingStream.Reading(
            LargeContent(),
            triggerOperationNumber: 1,
            StreamCancellationStyle.None,
            cancellation);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = upload,
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());

        Assert.AreEqual(1, upload.ReadCount);
    }

    // A resume offset on a non-seekable upload source is a loop of reads rather than a
    // seek, and that loop has a guard of its own. The exception does not tell the two loops
    // apart - the copy loop would throw it too - so the read count is what pins this one:
    // with the skip loop's guard deleted the skip finishes and a second read happens.
    [TestMethod]
    public async Task ExecuteAsync_TokenCancelledMidUploadSkip_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        var upload = CancellingStream.ReadingNonSeekable(
            LargeContent(),
            triggerOperationNumber: 1,
            StreamCancellationStyle.None,
            cancellation);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = upload,
            ResumeFrom = ChunkSize + 1,
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());

        Assert.AreEqual(1, upload.ReadCount);
    }

    // A real FileStream or MemoryStream cancelled during a read returns a cancelled
    // ValueTask, which surfaces at the await as TaskCanceledException. ThrowsExactly demands
    // the exact type, so this fails unless the handler narrows that back to
    // OperationCanceledException by rethrowing through the token.
    [TestMethod]
    public async Task ExecuteAsync_SourceReadCancelledWithAFaultedValueTask_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        byte[] content = Content;
        fileSystem.AddFileReadingFrom(
            OsPath,
            CancellingStream.Reading(
                content,
                triggerOperationNumber: 1,
                StreamCancellationStyle.FaultedValueTask,
                cancellation),
            content.Length);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_SourceReadCancelledByThrowingTaskCanceled_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        byte[] content = Content;
        fileSystem.AddFileReadingFrom(
            OsPath,
            CancellingStream.Reading(
                content,
                triggerOperationNumber: 1,
                StreamCancellationStyle.ThrownTaskCanceledException,
                cancellation),
            content.Length);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());
    }

    // The other half of that catch: a stream that reports cancellation while the token is
    // untouched cancelled itself, which is a read failure and not the caller's
    // cancellation. It stays a returned exit 26 rather than becoming a thrown exception.
    [TestMethod]
    public async Task ExecuteAsync_SourceThrowsTaskCanceledWithAnUncancelledToken_ReportsReadError()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = Content;
        fileSystem.AddFileReadingFrom(
            OsPath,
            CancellingStream.Reading(
                content,
                triggerOperationNumber: 1,
                StreamCancellationStyle.ThrownTaskCanceledException,
                cancels: null),
            content.Length);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.ReadError, result.ExitCode);
        Assert.AreEqual(ReadFailedMessage, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_OutputWriteCancelledWithAFaultedValueTask_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        using var cancellation = new CancellationTokenSource();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = CancellingStream.Writing(
                triggerOperationNumber: 1,
                StreamCancellationStyle.FaultedValueTask,
                cancellation),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationCancelledWithAFaultedValueTask_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        fileSystem.WriteInto(
            OsPath,
            CancellingStream.Writing(
                triggerOperationNumber: 1,
                StreamCancellationStyle.FaultedValueTask,
                cancellation));
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationCancelledByThrowingTaskCanceled_ThrowsOperationCanceledException()
    {
        var fileSystem = new FakeFileSystem();
        using var cancellation = new CancellationTokenSource();
        fileSystem.WriteInto(
            OsPath,
            CancellingStream.Writing(
                triggerOperationNumber: 1,
                StreamCancellationStyle.ThrownTaskCanceledException,
                cancellation));
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            CancellationToken = cancellation.Token,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context).AsTask());
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadDestinationThrowsTaskCanceledWithAnUncancelledToken_ReportsWriteError()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.WriteInto(
            OsPath,
            CancellingStream.Writing(
                triggerOperationNumber: 1,
                StreamCancellationStyle.ThrownTaskCanceledException,
                cancels: null));
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
        Assert.AreEqual(DestinationWriteFailedMessage, result.ErrorMessage);
    }

    // -r 0-4: both positions are inclusive, so this is five bytes.
    [TestMethod]
    public async Task ExecuteAsync_BoundedRange_WritesOnlyThatRange()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Bounded(0, 4),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("Hello"), output.ToArray());
        Assert.AreEqual(5L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // A bounded range whose end is far past the file must deliver the whole file, not an
    // empty one. ByteRange.Bounded(0, long.MaxValue) is legal - the factory only requires
    // the end not to precede the start - and the earlier arithmetic overflowed on it,
    // reporting exit 0 with nothing written. A CLI layer that normalises "-r 0-" to a
    // long.MaxValue upper bound would have produced empty output and a success code.
    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeEndingBeyondLongMaxValue_WritesTheWholeFile()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Bounded(0, long.MaxValue),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Content, output.ToArray());
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // The same overflow, reached from a non-zero start, so the clamp is exercised against
    // a start offset rather than only against zero.
    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeFromAnOffsetEndingBeyondLongMaxValue_WritesTheRemainder()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Bounded(6, long.MaxValue),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Content[6..], output.ToArray());
        Assert.AreEqual((long)(Content.Length - 6), result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // An empty file has no last byte for the clamp to land on, so the count must be
    // forced to zero rather than computed as one.
    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeOnAnEmptyFile_SucceedsWithNoBytes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, []);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Bounded(0, long.MaxValue),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.IsEmpty(output.ToArray());
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeFromOffset_WritesFromThatOffsetToTheEnd()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.FromOffset(6),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("file"), output.ToArray());
        Assert.AreEqual(4L, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuffixRange_WritesTheLastBytes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Suffix(5),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(" file"), output.ToArray());
        Assert.AreEqual(5L, result.BytesTransferred);
    }

    // The degenerate inclusive range: first equals last, so one byte, not zero.
    [TestMethod]
    public async Task ExecuteAsync_SingleByteRangeOfASingleByteFile_WritesThatByte()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, [0x41]);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Bounded(0, 0),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(new byte[] { 0x41 }, output.ToArray());
        Assert.AreEqual(1L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_RangeStartingPastTheEndOfTheFile_ReportsBadDownloadResume()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            Range = ByteRange.FromOffset(Content.Length + 1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual("failed to resume file:// transfer", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromInsideTheFile_WritesOnlyTheRemainder()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            ResumeFrom = 4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("o file"), output.ToArray());
        Assert.AreEqual(6L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Resuming exactly at the end is "already complete", not an error: only an offset
    // strictly past the end fails.
    [TestMethod]
    public async Task ExecuteAsync_ResumeFromEqualToTheLength_SucceedsWithNoBytes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            ResumeFrom = Content.Length,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromBeyondTheLength_ReportsBadDownloadResume()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            ResumeFrom = Content.Length + 1,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual("failed to resume file:// transfer", result.ErrorMessage);
    }

    // -I still opens the file: curl reports exit 37 for a directory with -I exactly as it
    // does without it, which it could not do if it skipped the open.
    [TestMethod]
    public async Task ExecuteAsync_NoBody_StillOpensTheFile()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            NoBody = true,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        var call = Assert.ContainsSingle(fileSystem.Calls);
        Assert.AreEqual(FileSystemCall.Read(OsPath), call);
    }

    [TestMethod]
    public async Task ExecuteAsync_NoBody_WritesNoBodyBytes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            NoBody = true,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Two details measured from curl 8.21.0: Accept-ranges carries a lowercase r, and the
    // three lines are followed by a blank one.
    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputRequested_WritesCurlsSynthesisedHeaders()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var headers = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            HeaderOutput = headers,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.AreEqual(ExpectedHeaders, Encoding.ASCII.GetString(headers.ToArray()));
    }

    // Content-Length describes the whole file, not the part a range asks for.
    [TestMethod]
    public async Task ExecuteAsync_HeaderOutputWithARange_ReportsTheWholeFileLength()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var headers = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            HeaderOutput = headers,
            Range = ByteRange.Bounded(0, 4),
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.AreEqual(ExpectedHeaders, Encoding.ASCII.GetString(headers.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_Upload_WritesNothingToTheHeaderOutput()
    {
        var fileSystem = new FakeFileSystem();
        var headers = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            HeaderOutput = headers,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsEmpty(headers.ToArray());
    }

    // -z <date> asks for the file only when it is newer. A skipped transfer is a success
    // in curl, not an error.
    [TestMethod]
    public async Task ExecuteAsync_IfModifiedSinceNotMet_SucceedsWithoutWritingTheBody()
    {
        var output = new ChunkRecordingStream();

        var result = await TimeConditionResultAsync(
            new TimeCondition(Later, TimeConditionKind.IfModifiedSince),
            output);

        Assert.IsEmpty(output.ToArray());
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_IfModifiedSinceMet_TransfersTheBody()
    {
        var output = new ChunkRecordingStream();

        var result = await TimeConditionResultAsync(
            new TimeCondition(Earlier, TimeConditionKind.IfModifiedSince),
            output);

        CollectionAssert.AreEqual(Content, output.ToArray());
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // -z -<date> is the mirror: the file is wanted only when it is not newer.
    [TestMethod]
    public async Task ExecuteAsync_IfUnmodifiedSinceNotMet_SucceedsWithoutWritingTheBody()
    {
        var output = new ChunkRecordingStream();

        var result = await TimeConditionResultAsync(
            new TimeCondition(Earlier, TimeConditionKind.IfUnmodifiedSince),
            output);

        Assert.IsEmpty(output.ToArray());
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_IfUnmodifiedSinceMet_TransfersTheBody()
    {
        var output = new ChunkRecordingStream();

        var result = await TimeConditionResultAsync(
            new TimeCondition(Later, TimeConditionKind.IfUnmodifiedSince),
            output);

        CollectionAssert.AreEqual(Content, output.ToArray());
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Measured on curl 8.21.0 against a ten-byte file: -r -10 is the whole file and exit 0.
    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeEqualToTheLength_WritesTheWholeFile()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = Content;
        fileSystem.AddFile(OsPath, content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Suffix(content.Length),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(content, output.ToArray());
        Assert.AreEqual((long)content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // And -r -11 of ten bytes is also the whole file and also exit 0: the cutoff is one
    // byte more than the length, not the length. Measured, not reasoned about.
    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeOneBeyondTheLength_WritesTheWholeFile()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = Content;
        fileSystem.AddFile(OsPath, content);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Suffix(content.Length + 1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(content, output.ToArray());
        Assert.AreEqual((long)content.Length, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // -r -12 of ten bytes is where it breaks, and it breaks with a different line from
    // every other exit 36 the handler can report.
    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeTwoBeyondTheLength_ReportsCouldNotResumeDownload()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = Content;
        fileSystem.AddFile(OsPath, content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            Range = ByteRange.Suffix(content.Length + 2),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(CouldNotResumeDownloadMessage, result.ErrorMessage);
        Assert.AreNotEqual(ResumeFailedMessage, result.ErrorMessage);
    }

    // The other half of that pair, kept beside it so the two strings cannot drift into one:
    // a range whose start is past the end still reports the older line.
    [TestMethod]
    public async Task ExecuteAsync_RangeStartPastTheEnd_ReportsFailedToResumeTransfer()
    {
        var fileSystem = new FakeFileSystem();
        byte[] content = Content;
        fileSystem.AddFile(OsPath, content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            Range = ByteRange.FromOffset(content.Length + 1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(ResumeFailedMessage, result.ErrorMessage);
        Assert.AreNotEqual(CouldNotResumeDownloadMessage, result.ErrorMessage);
    }

    // Inference from the length-plus-one rule rather than a measurement: -r -1 of an empty
    // file was not run against curl 8.21.0. If it is ever measured and disagrees, the rule
    // is what is wrong, not this test.
    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeOfOneOnAnEmptyFile_SucceedsWithNoBytes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, []);
        var output = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            Range = ByteRange.Suffix(1),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.IsEmpty(output.WriteLengths);
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Also inference from the same rule, and not measured.
    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeOfTwoOnAnEmptyFile_ReportsCouldNotResumeDownload()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, []);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            Range = ByteRange.Suffix(2),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(CouldNotResumeDownloadMessage, result.ErrorMessage);
        Assert.AreNotEqual(ResumeFailedMessage, result.ErrorMessage);
    }

    // -C 0 is not the same as no -C on the command line, but it is the same at the open:
    // curl tests the value and not whether one was given, so zero truncates. The no -C case
    // is ExecuteAsync_Upload_OpensTheDestinationTruncatedExactlyOnce above; this is the one
    // that fails if the handler asks "was a resume offset supplied" instead of "is it
    // positive".
    [TestMethod]
    public async Task ExecuteAsync_UploadWithResumeFromZero_OpensTheDestinationTruncated()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            ResumeFrom = 0,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        var call = Assert.ContainsSingle(fileSystem.Calls);
        Assert.AreEqual(FileSystemCall.Write(OsPath, FileWriteMode.Truncate), call);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithResumeFromZero_WritesTheWholeSource()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            ResumeFrom = 0,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Content, fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual((long)Content.Length, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadWithPositiveResumeFrom_OpensTheDestinationAppending()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            ResumeFrom = 1,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        var call = Assert.ContainsSingle(fileSystem.Calls);
        Assert.AreEqual(FileSystemCall.Write(OsPath, FileWriteMode.Append), call);
    }

    // The skip is relative to wherever the caller left the upload source, so a source
    // already positioned at three with -C 2 starts at five. Seeking to the offset absolutely
    // would silently discard the position the caller had set.
    [TestMethod]
    public async Task ExecuteAsync_UploadSourceAlreadyPositioned_SkipsRelativeToThatPosition()
    {
        var fileSystem = new FakeFileSystem();
        var upload = new TrackedMemoryStream(Content)
        {
            Position = 3,
        };
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = upload,
            ResumeFrom = 2,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes(" file"), fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(5L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // Measured against curl 8.21.0: an upload resume offset past the end of the upload
    // source exits 0 having written nothing. It is NOT exit 36 - the download direction
    // reports that, the upload direction does not, and the asymmetry is upstream's. A code
    // review has already proposed making the two directions agree; do not.
    [TestMethod]
    public async Task ExecuteAsync_UploadResumeFromPastTheEndOfTheSource_SucceedsWithNothingWritten()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            ResumeFrom = Content.Length + 1,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.IsEmpty(fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // The same measurement through the other skip path: -T - cannot be seeked, so the offset
    // is consumed by reading, and running out of source part way through is still exit 0.
    [TestMethod]
    public async Task ExecuteAsync_NonSeekableUploadResumeFromPastTheEndOfTheSource_SucceedsWithNothingWritten()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new NonSeekableStream(Content),
            ResumeFrom = Content.Length + 1,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.IsEmpty(fileSystem.WrittenBytes(OsPath));
        Assert.AreEqual(0L, result.BytesTransferred);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    // A negative -C is refused with the same exit 36 and the same line as an offset past the
    // end, in both directions, and it is returned rather than thrown: a bad option ends a
    // transfer, not the process.
    [TestMethod]
    public async Task ExecuteAsync_NegativeResumeFromOnADownload_ReportsBadDownloadResume()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            ResumeFrom = -4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(ResumeFailedMessage, result.ErrorMessage);
    }

    // The refusal happens before any open, so no file is touched and no pseudo-header is
    // emitted: the transfer never started.
    [TestMethod]
    public async Task ExecuteAsync_NegativeResumeFromOnADownload_OpensNothingAndWritesNoHeaders()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var headers = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = new ChunkRecordingStream(),
            HeaderOutput = headers,
            ResumeFrom = -4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsEmpty(fileSystem.Calls);
        Assert.IsEmpty(headers.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeResumeFromOnAnUpload_ReportsBadDownloadResume()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            ResumeFrom = -4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(ResumeFailedMessage, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NegativeResumeFromOnAnUpload_OpensNothingAndWritesNoHeaders()
    {
        var fileSystem = new FakeFileSystem();
        var headers = new ChunkRecordingStream();
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
            HeaderOutput = headers,
            ResumeFrom = -4,
        };
        var handler = new FileProtocolHandler(fileSystem);

        await handler.ExecuteAsync(context);

        Assert.IsEmpty(fileSystem.Calls);
        Assert.IsEmpty(headers.ToArray());
    }

    // A drive-letter authority is path text, so this reaches the open and fails there with
    // exit 37 rather than being rejected as a host with exit 3. Two caveats. The bare
    // file://C: the measured rule implies cannot be written as a test at all: new Uri throws
    // UriFormatException ("A Dos path must be rooted") before the parser is reached, exactly
    // as it does for file://ab:/x, so file://C:/ is the nearest expressible form. And the
    // exit 37 itself is inference from the drive-letter rule rather than an observation -
    // this URL was never run against curl 8.21.0.
    [TestMethod]
    public async Task ExecuteAsync_DriveLetterAuthorityWithNoFileName_ReportsExitThirtySeven()
    {
        var fileSystem = new FakeFileSystem();
        var context = new FakeTransferContext
        {
            Url = new Uri("file://C:/"),
            Output = new ChunkRecordingStream(),
        };
        var handler = new FileProtocolHandler(fileSystem);

        var result = await handler.ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file C:/", result.ErrorMessage);
    }

    private static DateTimeOffset Later => FakeFileSystem.DefaultLastWriteTimeUtc.AddDays(1);

    private static DateTimeOffset Earlier => FakeFileSystem.DefaultLastWriteTimeUtc.AddDays(-1);

    private static string NativePath(string slashedPath) =>
        slashedPath.Replace('/', Path.DirectorySeparatorChar);

    private static byte[] LargeContent()
    {
        byte[] content = new byte[40000];

        for (int index = 0; index < content.Length; index++)
        {
            content[index] = (byte)(index % 251);
        }

        return content;
    }

    private static async Task<TransferResult> ReadFailureResultAsync(FileAccessStatus status)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForRead(OsPath, status);
        var context = new FakeTransferContext { Url = FileUrl, Output = new ChunkRecordingStream() };
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }

    private static async Task<TransferResult> WriteFailureResultAsync(FileAccessStatus status)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForWrite(OsPath, status);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Upload = new TrackedMemoryStream(Content),
        };
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }

    private static async Task<TransferResult> TimeConditionResultAsync(
        TimeCondition condition,
        Stream output)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(OsPath, Content);
        var context = new FakeTransferContext
        {
            Url = FileUrl,
            Output = output,
            TimeCondition = condition,
        };
        var handler = new FileProtocolHandler(fileSystem);

        return await handler.ExecuteAsync(context);
    }
}
