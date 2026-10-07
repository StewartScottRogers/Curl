using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins <c>-N</c> / <c>--no-buffer</c> end to end: a handler that writes two blocks of body bytes
/// to standard output has each block flushed before the next is written, as curl 8.21.0 flushes
/// after each write; without it the blocks are written back to back and flushed once the
/// transfer ends. The bytes and their order are the same either way (BL-491).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerNoBufferTests
{
    private readonly WriteAndFlushRecordingStream standardOutput = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-N")]
    [DataRow("--no-buffer")]
    public async Task RunAsync_NoBuffer_FlushesEachBlockBeforeTheNext(string spelling)
    {
        int exitCode = await RunTwoBlocksAsync(spelling, "run");

        string[] expectedEvents = ["write:first", "flush", "write:second", "flush"];
        string[] actualEvents = standardOutput.Events.Take(4).ToArray();
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first four stdout events", string.Join(", ", expectedEvents), string.Join(", ", actualEvents));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            expectedEvents,
            actualEvents);
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--buffer")]
    public async Task RunAsync_Buffered_DoesNotFlushPerWrite(string spelling)
    {
        int exitCode = await RunTwoBlocksAsync(spelling, "run");

        string[] expectedEvents = ["write:first", "write:second"];
        string[] actualEvents = standardOutput.Events.Take(2).ToArray();
        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("first two stdout events", string.Join(", ", expectedEvents), string.Join(", ", actualEvents));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(expectedEvents, actualEvents);
    }

    [TestMethod]
    public async Task RunAsync_NoBuffer_WritesTheSameBytesInTheSameOrder()
    {
        await RunTwoBlocksAsync("--buffer", "buffered run");
        byte[] buffered = standardOutput.ToArray();
        standardOutput.SetLength(0);

        await RunTwoBlocksAsync("-N", "no-buffer run");

        byte[] expectedBuffered = "firstsecond"u8.ToArray();
        byte[] noBuffer = standardOutput.ToArray();
        Diagnostics.Diff("buffered stdout", expectedBuffered, buffered);
        Diagnostics.Diff("no-buffer stdout against buffered stdout", buffered, noBuffer);
        CollectionAssert.AreEqual(expectedBuffered, buffered);
        CollectionAssert.AreEqual(buffered, noBuffer);
    }

    private async Task<int> RunTwoBlocksAsync(string spelling, string phase)
    {
        RecordingProtocolHandler handler = new("http", async context =>
        {
            await context.Output.WriteAsync("first"u8.ToArray(), context.CancellationToken);
            await context.Output.WriteAsync("second"u8.ToArray(), context.CancellationToken);

            return TransferResult.Success(11);
        });

        string[] arguments = ["-s", spelling, "http://example.test/"];
        Diagnostics.Arrange("arguments", string.Join(' ', arguments));
        Diagnostics.Arrange("handler", "writes \"first\" then \"second\" to the output, reports 11 bytes");
        int exitCode;
        using (Diagnostics.Phase(phase))
        {
            exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                new InMemoryFileSystem(),
                new InMemoryFileSystem(),
                standardOutput,
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false)
                .RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("stdout events", string.Join(", ", standardOutput.Events));
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        return exitCode;
    }
}
