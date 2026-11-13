using Curl.Engine;
using Curl.Cli;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that <c>--manual</c> into a pipe that closed early ends quietly with exit 0 (BL-5042), while
/// other information output still reports the write failure.
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerManualClosedPipeTests
{
    private readonly InMemoryFileSystem fileSystem = new();
    private readonly RecordingProtocolHandler file = RecordingProtocolHandler.WritingPath("file");

    [TestMethod]
    public async Task RunAsync_ManualWithAClosedPipe_ExitsZeroWithNoStandardError()
    {
        MemoryStream standardError = new();
        int exitCode = await RunAsync(["--manual"], new ClosedPipeStream(), standardError);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_ManualWithAWorkingOutput_PrintsTheWholeManual()
    {
        MemoryStream standardOutput = new();
        MemoryStream standardError = new();
        int exitCode = await RunAsync(["--manual"], standardOutput, standardError);

        Assert.AreEqual(0, exitCode);
        Assert.IsGreaterThan(0, standardOutput.Length);
        Assert.AreEqual(0, standardError.Length);
    }

    [TestMethod]
    public async Task RunAsync_VersionWithAClosedPipe_StillReportsTheWriteFailure()
    {
        MemoryStream standardError = new();
        await Assert.ThrowsExactlyAsync<IOException>(() => RunAsync(["--version"], new ClosedPipeStream(), standardError));
    }

    private Task<int> RunAsync(IReadOnlyList<string> arguments, Stream standardOutput, Stream standardError) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([file])),
                fileSystem,
                fileSystem,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                terminalColumns: TerminalColumns.Default)
            .RunAsync(arguments);

    private sealed class ClosedPipeStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new IOException("The pipe has been ended.");
    }
}

