using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins AF-0513: curl 8.22.0 sizes the first URL's <c>-T</c> file before its trace file opens, so
/// <c>-T f --trace f</c> expects the 9 bytes <c>f</c> held before the trace truncated it, reads none and
/// fails with exit 26, <c>client read function EOF fail, only 0/9 of needed bytes read</c> (ADR-1175).
/// The handler here records the upload's size and what it reads, as the HTTP handler would size it.
/// </summary>
[TestClass]
public sealed class UploadSizedBeforeTraceTests
{
    private readonly InMemoryFileSystem files = new();

    private readonly MemoryStream standardOutput = new();

    private readonly MemoryStream standardError = new();

    private readonly List<(long Length, int Read)> uploads = [];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("--trace", DisplayName = "--trace")]
    [DataRow("--trace-ascii", DisplayName = "--trace-ascii")]
    public async Task RunAsync_TraceFileIsTheUploadFile_SizesTheUploadBeforeTheTraceTruncatesIt(string traceOption)
    {
        files.ExistingContent["f.txt"] = "sub body\n"u8.ToArray();

        await RunAsync("-T", "f.txt", traceOption, "f.txt", "http://127.0.0.1:22000/");

        Diagnostics.Assert("uploads (length, read)", "(9, 0)", string.Join(' ', uploads));
        CollectionAssert.AreEqual(new[] { (9L, 0) }, uploads);
    }

    [TestMethod]
    public async Task RunAsync_TraceFileIsAnotherFile_UploadsTheFileAsItIs()
    {
        files.ExistingContent["f.txt"] = "sub body\n"u8.ToArray();

        await RunAsync("-T", "f.txt", "--trace", "t.txt", "http://127.0.0.1:22000/");

        Diagnostics.Assert("uploads (length, read)", "(9, 9)", string.Join(' ', uploads));
        CollectionAssert.AreEqual(new[] { (9L, 9) }, uploads);
    }

    [TestMethod]
    public async Task RunAsync_SecondUrlUploadsAnotherFile_KeepsTheSecondFilesOwnSize()
    {
        files.ExistingContent["a.txt"] = "aaaa"u8.ToArray();
        files.ExistingContent["b.txt"] = "bb"u8.ToArray();

        await RunAsync("-T", "a.txt", "-T", "b.txt", "--trace", "t.txt", "http://127.0.0.1:22000/1", "http://127.0.0.1:22000/2");

        Diagnostics.Assert("uploads (length, read)", "(4, 4) (2, 2)", string.Join(' ', uploads));
        CollectionAssert.AreEqual(new[] { (4L, 4), (2L, 2) }, uploads);
    }

    [TestMethod]
    public async Task RunAsync_UploadFileCannotBeOpened_RefusesItAsBefore()
    {
        files.UnreadablePaths.Add("missing.txt");

        int exitCode = await RunAsync("-T", "missing.txt", "--trace", "t.txt", "http://127.0.0.1:22000/");

        Diagnostics.Assert("exit code", 26, exitCode);
        Assert.AreEqual(26, exitCode);
        Assert.IsEmpty(uploads);
    }

    [TestMethod]
    public async Task RunAsync_UploadFromStandardInputWithTrace_ReadsStandardInputOnce()
    {
        await RunAsync("-T", "-", "--trace", "t.txt", "http://127.0.0.1:22000/");

        Diagnostics.Assert("uploads (length, read)", "(-1, 5)", string.Join(' ', uploads));
        CollectionAssert.AreEqual(new[] { (-1L, 5) }, uploads);
    }

    private async Task<int> RunAsync(params string[] arguments)
    {
        RecordingProtocolHandler http = new("http", async context =>
        {
            Stream upload = context.Upload!;
            long length = upload.CanSeek ? upload.Length : -1;
            byte[] buffer = new byte[64];
            int read = await upload.ReadAsync(buffer, context.CancellationToken);
            uploads.Add((length, read));
            context.Events.ReportResponseHeader("HTTP/1.1 200 OK\r\n"u8);
            return TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 200 } };
        });
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                new TruncatingFileSystem(files),
                files,
                standardOutput,
                standardError,
                new MemoryStream("stdin"u8.ToArray()),
                runsOnWindows: false,
                outputPaths: files)
            .RunAsync(arguments);
        Diagnostics.Act("exit code, stderr", $"{exitCode} [{Encoding.UTF8.GetString(standardError.ToArray())}]");
        return exitCode;
    }

    /// <summary>
    /// An <see cref="InMemoryFileSystem" /> whose truncating open empties the file a later read open
    /// sees, as a real file system's does.
    /// </summary>
    private sealed class TruncatingFileSystem(InMemoryFileSystem inner) : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            inner.OpenForReadAsync(path, cancellationToken);

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken)
        {
            if (mode == FileWriteMode.Truncate && inner.ExistingContent.ContainsKey(path))
            {
                inner.ExistingContent[path] = [];
            }

            return inner.OpenForWriteAsync(path, mode, createMode, cancellationToken);
        }
    }
}
