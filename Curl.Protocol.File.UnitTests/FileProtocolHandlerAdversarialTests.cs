using System.Collections.Concurrent;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File.Fakes;
using Curl.Testing;

namespace Curl.Protocol.File;

/// <summary>
/// Adversarial black-box tests of <see cref="FileProtocolHandler" /> (BL-1507), by the
/// method in <c>Documentation/Wiki/Adversarial-Testing.md</c>: ranges and resume offsets
/// at, beyond and before the file's length up to <see cref="long.MaxValue" />, chunk
/// boundaries, encoded, non-ASCII and very long names, a non-<c>file</c> URL, and repeated,
/// reused and concurrent transfers on one handler. Every exit code and message pinned here
/// was measured against curl 8.21.0 (Schannel build) on a ten-byte and an empty file, or is
/// the handler's documented contract. Every URL is drive-less.
/// </summary>
[TestClass]
public sealed class FileProtocolHandlerAdversarialTests
{
    private const string ResumeFailedMessage = "failed to resume file:// transfer";

    private const string CouldNotResumeDownloadMessage = "Could not resume download";

    /// <summary>
    /// The write size curl uses for a <c>file://</c> download body.
    /// </summary>
    private const int ChunkSize = 16384;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private static byte[] Ten => Encoding.ASCII.GetBytes("0123456789");

    // Boundaries.

    [TestMethod]
    [DataRow(9L, 9L, "9")]
    [DataRow(0L, 0L, "0")]
    [DataRow(9L, long.MaxValue, "9")]
    [DataRow(10L, 10L, "")]
    public async Task ExecuteAsync_BoundedRangeAtTheLastByteAndBeyond_WritesWhatCurlWrites(
        long first,
        long last,
        string expected)
    {
        var (result, output) = await DownloadTenAsync(range: ByteRange.Bounded(first, last));

        Diagnostics.Arrange("range", $"{first}-{last}");
        Diagnostics.Assert("output", expected, Encoding.ASCII.GetString(output));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(output));
    }

    [TestMethod]
    [DataRow(10L)]
    [DataRow(11L)]
    [DataRow(long.MaxValue)]
    public async Task ExecuteAsync_RangeFromOffsetAtOrPastTheLength_SucceedsEmptyOnlyAtTheLength(long first)
    {
        var (result, output) = await DownloadTenAsync(range: ByteRange.FromOffset(first));

        Diagnostics.Arrange("range", $"{first}-");
        Diagnostics.Act("exit code", result.ExitCode);
        Assert.IsEmpty(output);
        if (first == 10)
        {
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
            return;
        }

        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(ResumeFailedMessage, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(9L, "9")]
    [DataRow(10L, "")]
    public async Task ExecuteAsync_ResumeFromTheLastByteOrTheLength_WritesTheRest(long offset, string expected)
    {
        var (result, output) = await DownloadTenAsync(resumeFrom: offset);

        Diagnostics.Assert("output", expected, Encoding.ASCII.GetString(output));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(expected, Encoding.ASCII.GetString(output));
    }

    [TestMethod]
    public async Task ExecuteAsync_ResumeFromLongMaxValue_ReportsFailedToResumeWithoutOverflowing()
    {
        var (result, output) = await DownloadTenAsync(resumeFrom: long.MaxValue);

        Diagnostics.Act("exit code", result.ExitCode);
        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(ResumeFailedMessage, result.ErrorMessage);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeOfLongMaxValue_ReportsCouldNotResumeDownload()
    {
        var (result, output) = await DownloadTenAsync(range: ByteRange.Suffix(long.MaxValue));

        Diagnostics.Act("exit code", result.ExitCode);
        Assert.AreEqual(CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.AreEqual(CouldNotResumeDownloadMessage, result.ErrorMessage);
        Assert.IsEmpty(output);
    }

    [TestMethod]
    public async Task ExecuteAsync_SuffixRangeOfOne_WritesOnlyTheLastByte()
    {
        var (result, output) = await DownloadTenAsync(range: ByteRange.Suffix(1));

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("9", Encoding.ASCII.GetString(output));
    }

    [TestMethod]
    [DataRow(0L, true)]
    [DataRow(1L, false)]
    public async Task ExecuteAsync_ResumeOnAnEmptyFile_SucceedsOnlyAtZero(long offset, bool succeeds)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/empty"), []);
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = CurlUrl.Parse("file:///d/empty"), Output = output, ResumeFrom = offset };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Assert.AreEqual(succeeds ? CurlExitCode.Ok : CurlExitCode.BadDownloadResume, result.ExitCode);
        Assert.IsEmpty(output.ToArray());
    }

    [TestMethod]
    [DataRow(ChunkSize - 1, new[] { ChunkSize - 1 })]
    [DataRow(ChunkSize, new[] { ChunkSize })]
    [DataRow(ChunkSize + 1, new[] { ChunkSize, 1 })]
    [DataRow((2 * ChunkSize) + 1, new[] { ChunkSize, ChunkSize, 1 })]
    public async Task ExecuteAsync_FileAtAndAroundTheChunkSize_WritesInWholeChunksThenTheRemainder(
        int length,
        int[] expectedWrites)
    {
        byte[] content = Pattern(length);
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/big"), content);
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = CurlUrl.Parse("file:///d/big"), Output = output };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Act("write lengths", string.Join(",", output.WriteLengths));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(expectedWrites, output.WriteLengths.ToArray());
        CollectionAssert.AreEqual(content, output.ToArray());
        Assert.AreEqual((long)length, result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_BoundedRangeCrossingAChunkBoundary_WritesExactlyTheWindow()
    {
        byte[] content = Pattern((2 * ChunkSize) + 10);
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/big"), content);
        var output = new ChunkRecordingStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("file:///d/big"),
            Output = output,
            Range = ByteRange.Bounded(ChunkSize - 1, ChunkSize + 1),
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(content[(ChunkSize - 1)..(ChunkSize + 2)], output.ToArray());
    }

    // Malformed input.

    [TestMethod]
    public async Task ExecuteAsync_EncodedSlashInThePath_OpensTheDecodedNestedPath()
    {
        // curl 8.21.0 opened d/a b.txt for d%2Fa%20b.txt and exited 0.
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/a b.txt"), Ten);
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = CurlUrl.Parse("file:///d%2Fa%20b.txt"), Output = output };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Ten, output.ToArray());
    }

    [TestMethod]
    [DataRow("file:///d/%FF%FE", "/d/%FF%FE")]
    [DataRow("file:///d/%%41", "/d/%%41")]
    [DataRow("file:///d/a%4", "/d/a%4")]
    [DataRow("file:///d/%c3%a9.txt", "/d/%C3%A9.txt")]
    [DataRow("file:///d/é.txt", "/d/%C3%A9.txt")]
    public async Task ExecuteAsync_MissingFileWithAMalformedOrNonAsciiName_QuotesTheEncodedPathAsCurlDoes(
        string url,
        string quoted)
    {
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new ChunkRecordingStream() };

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Diagnostics.Act("message", result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file " + quoted, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_NonAsciiName_OpensTheDecodedCharacters()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/日本\U0001F600"), Ten);
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = CurlUrl.Parse("file:///d/%E6%97%A5本\U0001F600"), Output = output };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Ten, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_VeryLongMissingName_ReportsExitThirtySevenWithTheWholeName()
    {
        string name = new('n', 65536);
        var context = new TransferContext { Url = CurlUrl.Parse("file:///d/" + name), Output = new ChunkRecordingStream() };

        var result = await new FileProtocolHandler(new FakeFileSystem()).ExecuteAsync(context);

        Diagnostics.Act("message length", result.ErrorMessage?.Length);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.AreEqual("Could not open file /d/" + name, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadToAnEncodedSlashPath_WritesTheDecodedNestedPath()
    {
        var fileSystem = new FakeFileSystem();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("file:///d%2Fup%20load"),
            Output = new ChunkRecordingStream(),
            Upload = new MemoryStream(Ten),
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Ten, fileSystem.WrittenBytes(NativePath("/d/up load")));
    }

    // Invalid partitions.

    [TestMethod]
    [DataRow("http://localhost/d/x")]
    [DataRow("ftp://localhost/d/x")]
    public async Task ExecuteAsync_UrlOfAnotherScheme_FailsWithUrlMalformatAndOpensNothing(string url)
    {
        var fileSystem = new FakeFileSystem();
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = new ChunkRecordingStream() };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Assert.AreEqual(CurlExitCode.UrlMalformat, result.ExitCode);
        Assert.IsEmpty(fileSystem.Calls);
    }

    [TestMethod]
    [DataRow(FileAccessStatus.NotFound)]
    [DataRow(FileAccessStatus.AccessDenied)]
    [DataRow(FileAccessStatus.IoError)]
    [DataRow(FileAccessStatus.IsDirectory)]
    [DataRow(FileAccessStatus.AlreadyExists)]
    public async Task ExecuteAsync_SourceOpenFailingWithAnyStatus_ReportsExitThirtySeven(FileAccessStatus status)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForRead(NativePath("/d/x"), status);
        var output = new ChunkRecordingStream();
        var context = new TransferContext { Url = CurlUrl.Parse("file:///d/x"), Output = output };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Arrange("status", status);
        Assert.AreEqual(CurlExitCode.FileCouldntReadFile, result.ExitCode);
        Assert.IsEmpty(output.ToArray());
    }

    [TestMethod]
    [DataRow(FileAccessStatus.NotFound)]
    [DataRow(FileAccessStatus.AccessDenied)]
    [DataRow(FileAccessStatus.IoError)]
    [DataRow(FileAccessStatus.IsDirectory)]
    [DataRow(FileAccessStatus.AlreadyExists)]
    public async Task ExecuteAsync_UploadDestinationOpenFailingWithAnyStatus_ReportsWriteError(FileAccessStatus status)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.FailOpenForWrite(NativePath("/d/x"), status);
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("file:///d/x"),
            Output = new ChunkRecordingStream(),
            Upload = new MemoryStream(Ten),
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Diagnostics.Arrange("status", status);
        Assert.AreEqual(CurlExitCode.WriteError, result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_UploadOverAnExistingLongerFile_LeavesOnlyTheNewBytes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/x"), Pattern(1000));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("file:///d/x"),
            Output = new ChunkRecordingStream(),
            Upload = new MemoryStream(Ten),
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Ten, fileSystem.WrittenBytes(NativePath("/d/x")));
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyUpload_CreatesAnEmptyDestination()
    {
        var fileSystem = new FakeFileSystem();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("file:///d/x"),
            Output = new ChunkRecordingStream(),
            Upload = new MemoryStream([]),
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(fileSystem.WrittenBytes(NativePath("/d/x")));
    }

    // State and concurrency.

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerTwice_WritesTheFileBothTimes()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/x"), Ten);
        var handler = new FileProtocolHandler(fileSystem);
        var first = new ChunkRecordingStream();
        var second = new ChunkRecordingStream();

        var firstResult = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("file:///d/x"), Output = first });
        var secondResult = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("file:///d/x"), Output = second });

        Assert.AreEqual(CurlExitCode.Ok, firstResult.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, secondResult.ExitCode);
        CollectionAssert.AreEqual(Ten, first.ToArray());
        CollectionAssert.AreEqual(Ten, second.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterEachKindOfFailure_TheSameHandlerStillDownloads()
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/x"), Ten);
        var handler = new FileProtocolHandler(fileSystem);
        TransferContext[] failing =
        [
            new TransferContext { Url = CurlUrl.Parse("file:///d/missing"), Output = new ChunkRecordingStream() },
            new TransferContext { Url = CurlUrl.Parse("file:///d/x"), Output = new ChunkRecordingStream(), ResumeFrom = 11 },
            new TransferContext { Url = CurlUrl.Parse("file:///d/x"), Output = new ChunkRecordingStream(), ResumeFrom = -1 },
            new TransferContext { Url = CurlUrl.Parse("file:///d/%00"), Output = new ChunkRecordingStream() },
            new TransferContext { Url = CurlUrl.Parse("http://localhost/"), Output = new ChunkRecordingStream() },
        ];

        foreach (var context in failing)
        {
            var failed = await handler.ExecuteAsync(context);
            Assert.IsFalse(failed.IsSuccess, context.Url.OriginalString);
        }

        var output = new ChunkRecordingStream();
        var result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("file:///d/x"), Output = output });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(Ten, output.ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ManyConcurrentRangedDownloadsOnOneHandler_EachGetsItsOwnWindow()
    {
        byte[] content = Pattern(3 * ChunkSize);
        var fileSystem = new ConcurrentReadOnlyFileSystem(NativePath("/d/big"), content);
        var handler = new FileProtocolHandler(fileSystem);

        var transfers = Enumerable.Range(0, 32).Select(async index =>
        {
            var output = new MemoryStream();
            long first = index * 1000L;
            var context = new TransferContext
            {
                Url = CurlUrl.Parse("file:///d/big"),
                Output = output,
                Range = ByteRange.Bounded(first, first + 20000),
            };
            var result = await Task.Run(async () => await handler.ExecuteAsync(context));
            return (first, result, bytes: output.ToArray());
        }).ToArray();

        var results = await Task.WhenAll(transfers);

        foreach (var (first, result, bytes) in results)
        {
            int end = (int)Math.Min(first + 20001, content.Length);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
            CollectionAssert.AreEqual(content[(int)first..end], bytes, $"range starting {first}");
        }

        Assert.AreEqual(32, fileSystem.Opens);
    }

    private async Task<(TransferResult Result, byte[] Output)> DownloadTenAsync(
        ByteRange? range = null,
        long? resumeFrom = null)
    {
        var fileSystem = new FakeFileSystem();
        fileSystem.AddFile(NativePath("/d/ten"), Ten);
        var output = new ChunkRecordingStream();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("file:///d/ten"),
            Output = output,
            Range = range,
            ResumeFrom = resumeFrom,
        };

        var result = await new FileProtocolHandler(fileSystem).ExecuteAsync(context);
        Diagnostics.Act("exit code", result.ExitCode);
        return (result, output.ToArray());
    }

    private static byte[] Pattern(int length)
    {
        byte[] content = new byte[length];

        for (int index = 0; index < length; index++)
        {
            content[index] = (byte)(index % 251);
        }

        return content;
    }

    private static string NativePath(string slashedPath) =>
        slashedPath.Replace('/', Path.DirectorySeparatorChar);

    /// <summary>
    /// A file system holding one file, safe to open from many threads at once, that hands
    /// each open its own stream and refuses every write.
    /// </summary>
    private sealed class ConcurrentReadOnlyFileSystem(string path, byte[] content) : IFileSystem
    {
        private readonly ConcurrentQueue<string> opened = new();

        public int Opens => opened.Count;

        public ValueTask<FileOpenResult> OpenForReadAsync(string requestedPath, CancellationToken cancellationToken)
        {
            opened.Enqueue(requestedPath);

            return ValueTask.FromResult(
                requestedPath == path
                    ? FileOpenResult.Opened(new MemoryStream(content, writable: false), content.Length, FakeFileSystem.DefaultLastWriteTimeUtc)
                    : FileOpenResult.Failed(FileAccessStatus.NotFound));
        }

        public ValueTask<FileOpenResult> OpenForWriteAsync(
            string requestedPath,
            FileWriteMode mode,
            UnixFileMode createMode,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(FileOpenResult.Failed(FileAccessStatus.AccessDenied));
    }
}
