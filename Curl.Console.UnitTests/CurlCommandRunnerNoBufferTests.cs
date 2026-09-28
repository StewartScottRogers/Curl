using Curl.Core;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    [DataRow("-N")]
    [DataRow("--no-buffer")]
    public async Task RunAsync_NoBuffer_FlushesEachBlockBeforeTheNext(string spelling)
    {
        int exitCode = await RunTwoBlocksAsync(spelling);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[] { "write:first", "flush", "write:second", "flush" },
            standardOutput.Events.Take(4).ToArray());
    }

    [TestMethod]
    [DataRow("-s")]
    [DataRow("--buffer")]
    public async Task RunAsync_Buffered_DoesNotFlushPerWrite(string spelling)
    {
        int exitCode = await RunTwoBlocksAsync(spelling);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "write:first", "write:second" }, standardOutput.Events.Take(2).ToArray());
    }

    [TestMethod]
    public async Task RunAsync_NoBuffer_WritesTheSameBytesInTheSameOrder()
    {
        await RunTwoBlocksAsync("--buffer");
        byte[] buffered = standardOutput.ToArray();
        standardOutput.SetLength(0);

        await RunTwoBlocksAsync("-N");

        CollectionAssert.AreEqual("firstsecond"u8.ToArray(), buffered);
        CollectionAssert.AreEqual(buffered, standardOutput.ToArray());
    }

    private Task<int> RunTwoBlocksAsync(string spelling)
    {
        RecordingProtocolHandler handler = new("http", async context =>
        {
            await context.Output.WriteAsync("first"u8.ToArray(), context.CancellationToken);
            await context.Output.WriteAsync("second"u8.ToArray(), context.CancellationToken);

            return TransferResult.Success(11);
        });

        return new CurlCommandRunner(
            _ => new TransferDispatch(new ProtocolDispatcher([handler])),
            new InMemoryFileSystem(),
            new InMemoryFileSystem(),
            standardOutput,
            new MemoryStream(),
            new MemoryStream(),
            runsOnWindows: false)
            .RunAsync(["-s", spelling, "http://example.test/"]);
    }
}
