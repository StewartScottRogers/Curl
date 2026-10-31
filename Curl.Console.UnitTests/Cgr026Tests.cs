using System.Text;
using Curl.Core;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.File;

namespace Curl.Console;

/// <summary>
/// CGR-026 (BL-3555): an <c>-o</c> file on a full device (<c>/dev/full</c>, ENOSPC) takes the
/// body into its buffer and fails only when it is flushed as it closes. curl 8.18.0 reports that
/// <c>fclose</c> failure as <c>curl: curl: (23) Failed writing body</c>, exit 23, with
/// <c>%{errormsg}</c> <c>Failed writing received data to disk/application</c> (measured in WSL on
/// 2026-10-26), where Curl let the exception escape to exit 2.
/// </summary>
[TestClass]
public sealed class Cgr026Tests
{
    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem files = new();

    [TestMethod]
    public async Task RunAsync_OutputFileWhoseCloseFails_PrintsFailedWritingBodyAndExits23()
    {
        int exitCode = await RunAsync(["-sS", "-o", "full.txt", "file:///source.txt"]);

        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual("curl: curl: (23) Failed writing body" + Environment.NewLine, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.AreEqual(0, standardOutput.Length);
    }

    [TestMethod]
    public async Task RunAsync_OutputFileWhoseCloseFails_WriteOutGetsExitCode23AndItsMessage()
    {
        int exitCode = await RunAsync(["-sS", "-o", "full.txt", "-w", "%{exitcode}:%{errormsg}", "file:///source.txt"]);

        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual("23:Failed writing received data to disk/application", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_OutputFileWhoseCloseFails_UnderSilentPrintsNothing()
    {
        int exitCode = await RunAsync(["-s", "-o", "full.txt", "file:///source.txt"]);

        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task CompleteAsync_FailedTransferWhoseCloseFails_KeepsTheTransfersResultAndDisposesQuietly()
    {
        var output = new DeferredOutputFileStream(new FullDeviceFileSystem(files), "full.txt", FileWriteMode.Truncate);
        await output.WriteAsync(new byte[] { 1 });
        var failure = TransferResult.Failure(CurlExitCode.CouldntConnect, "refused");

        TransferResult completed = await output.CompleteAsync(failure);
        await output.DisposeAsync();

        Assert.AreSame(failure, completed);
    }

    [TestMethod]
    public async Task Dispose_FileWhoseCloseFails_DoesNotThrow()
    {
        var output = new DeferredOutputFileStream(new FullDeviceFileSystem(files), "full.txt", FileWriteMode.Truncate);
        await output.WriteAsync(new byte[] { 1 });

        output.Dispose();

        Assert.IsTrue(output.IsOpen);
    }

    private async Task<int> RunAsync(IReadOnlyList<string> arguments)
    {
        var source = new FileProtocolHandler(new InMemoryFileSystem { ReadContent = Encoding.ASCII.GetBytes("hello world\n") });

        return await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([source])),
                new FullDeviceFileSystem(files),
                files,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: files)
            .RunAsync(arguments);
    }

    // A file system whose files behave as /dev/full does behind a stdio buffer: writes are
    // taken, and flushing or closing them fails with ENOSPC.
    private sealed class FullDeviceFileSystem(InMemoryFileSystem inner) : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            inner.OpenForReadAsync(path, cancellationToken);

        public ValueTask<FileOpenResult> OpenForWriteAsync(
            string path,
            FileWriteMode mode,
            UnixFileMode createMode,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(FileOpenResult.Opened(new FullDeviceStream(), 0, null));
    }

    private sealed class FullDeviceStream : MemoryStream
    {
        public override void Flush() => throw NoSpace();

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.FromException(NoSpace());

        public override ValueTask DisposeAsync() => ValueTask.FromException(NoSpace());

        protected override void Dispose(bool disposing) => throw NoSpace();

        private static IOException NoSpace() => new("No space left on device : '/dev/full'");
    }
}
