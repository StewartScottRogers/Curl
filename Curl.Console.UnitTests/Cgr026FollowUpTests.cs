using System.Diagnostics.CodeAnalysis;
using System.Text;
using Curl.Cookies;
using Curl.Core;
using Curl.Engine;
using Curl.Output;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.File;

namespace Curl.Console;

/// <summary>
/// CGR-026's other files (BL-3830): a <c>-D</c>, <c>--trace</c>, <c>-c</c>, <c>%output{}</c> or
/// <c>--stderr</c> file on a full device (<c>/dev/full</c>, ENOSPC). curl 8.18.0, measured in WSL
/// on 2026-10-26 with <c>file:///tmp/s.txt</c> (12 bytes) and <c>-sS</c>: <c>-D /dev/full</c>
/// prints <c>curl: Failed writing headers to /dev/full</c> and
/// <c>curl: (23) client returned ERROR on write of 20 bytes</c>, exit 23; <c>--trace</c>,
/// <c>--trace-ascii</c>, <c>-c</c> and <c>%output{/dev/full}</c> exit 0 with nothing on standard
/// error; <c>--stderr /dev/full</c> keeps the transfer's exit code. Curl let each close failure
/// escape to exit 2.
/// </summary>
[TestClass]
public sealed class Cgr026FollowUpTests
{
    private const string FullFile = "full.txt";

    private const string WriteFailingFile = "large.txt";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    [TestMethod]
    public async Task RunAsync_HeaderFileOnAFullDevice_PrintsFailedWritingHeadersAndExits23()
    {
        int exitCode = await RunAsync(["-sS", "-D", FullFile, "-o", "out.txt", "file:///source.txt"]);

        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            $"curl: Failed writing headers to {FullFile}{Environment.NewLine}curl: (23) client returned ERROR on write of 20 bytes{Environment.NewLine}",
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    [DataRow("--trace")]
    [DataRow("--trace-ascii")]
    public async Task RunAsync_TraceFileOnAFullDevice_ExitsZeroWithNothingOnStandardError(string traceOption)
    {
        int exitCode = await RunAsync(["-sS", traceOption, FullFile, "-o", "out.txt", "file:///source.txt"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_WriteOutFileOnAFullDevice_ExitsZeroWithNothingOnStandardError()
    {
        int exitCode = await RunAsync(["-sS", "-o", "out.txt", "-w", "%output{" + FullFile + "}x", "file:///source.txt"]);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_StandardErrorFileOnAFullDevice_KeepsTheTransfersExitCode()
    {
        int exitCode = await RunAsync(["-sS", "--stderr", FullFile, "nosuch://host/"]);

        Assert.AreEqual((int)CurlExitCode.UnsupportedProtocol, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }


    [TestMethod]
    public async Task RunAsync_LargeBodyToAnOutputFileWhoseWritesFail_PassesCurlsReadSize()
    {
        int exitCode = await RunAsync(["-sS", "-o", WriteFailingFile, "file:///source.txt"], new byte[20000]);

        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(
            "curl: (23) Failure writing output to destination, passed 16384 returned 0" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }
    [TestMethod]
    public async Task SaveCookieJarAsync_JarOnAFullDevice_ReturnsFalseWithoutThrowing()
    {
        bool saved = await new CookieStore().SaveCookieJarAsync(new FullDeviceFileSystem(files), FullFile, DateTimeOffset.UnixEpoch, CancellationToken.None);

        Assert.IsFalse(saved);
    }

    [TestMethod]
    public void TryOpen_InnerOpenerRefuses_Refuses()
    {
        bool opened = new WriteFailureIgnoringWriteOutFileOpener(new RefusingWriteOutFileOpener()).TryOpen(FullFile, append: false, out Stream? stream);

        Assert.IsFalse(opened);
        Assert.IsNull(stream);
    }

    [TestMethod]
    public void Dispose_InnerStreamWhoseCloseFails_DoesNotThrow()
    {
        var stream = new WriteFailureIgnoringStream(new FullDeviceStream());

        stream.Dispose();

        Assert.IsFalse(stream.CanSeek);
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments) => RunAsync(arguments, Encoding.ASCII.GetBytes("hello world\n"));

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, byte[] sourceContent)
    {
        var source = new FileProtocolHandler(new InMemoryFileSystem { ReadContent = sourceContent });

        return await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([source])),
                new FullDeviceFileSystem(files),
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                writeOutFileOpener: new FullDeviceWriteOutFileOpener(),
                outputPaths: files)
            .RunAsync(arguments);
    }

    // A file system whose full.txt behaves as /dev/full does behind a stdio buffer, whose
    // large.txt fails every write as /dev/full does a write too large to buffer, and whose
    // every other file is the inner one's.
    private sealed class FullDeviceFileSystem(InMemoryFileSystem inner) : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            inner.OpenForReadAsync(path, cancellationToken);

        public ValueTask<FileOpenResult> OpenForWriteAsync(
            string path,
            FileWriteMode mode,
            UnixFileMode createMode,
            CancellationToken cancellationToken) =>
            path switch
            {
                FullFile => ValueTask.FromResult(FileOpenResult.Opened(new FullDeviceStream(), 0, null)),
                WriteFailingFile => ValueTask.FromResult(FileOpenResult.Opened(new FullDeviceStream(failsWrites: true), 0, null)),
                _ => inner.OpenForWriteAsync(path, mode, createMode, cancellationToken),
            };
    }

    private sealed class FullDeviceWriteOutFileOpener : IWriteOutFileOpener
    {
        public bool TryOpen(string path, bool append, [NotNullWhen(true)] out Stream? stream)
        {
            stream = new FullDeviceStream();
            return true;
        }
    }

    // Writes are taken into the buffer, unless failsWrites; flushing or closing fails with ENOSPC.
    private sealed class FullDeviceStream(bool failsWrites = false) : MemoryStream
    {
        public override void Flush() => throw NoSpace();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (failsWrites)
            {
                throw NoSpace();
            }

            base.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            failsWrites ? ValueTask.FromException(NoSpace()) : base.WriteAsync(buffer, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.FromException(NoSpace());

        public override ValueTask DisposeAsync() => ValueTask.FromException(NoSpace());

        protected override void Dispose(bool disposing) => throw NoSpace();

        private static IOException NoSpace() => new("No space left on device : '/dev/full'");
    }
}
