using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// The <c>-v</c> lines BL-1387 added - a Content-Length too large for a signed 64-bit integer,
/// and bytes left after a chunked body - against curl 8.21.0 (mingw, Schannel) measured on
/// 2026-10-03 with <c>Record-CurlExchange.ps1</c>; the responses are in the BL-1387 Context.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string OverflowResponse = "HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello";

    private const string ChunkedWithLeftovers = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\nEXTRA";

    [TestMethod]
    public async Task ExecuteAsync_OverflowingContentLength_ReportsOverflowBeforeTheHeaderAndShutsTheConnectionDown()
    {
        // curl -s -v against a 200 whose Content-Length is 99999999999999999999.
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection(OverflowResponse, chunkSize);
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            Diagnostics.Arrange("response, chunk size", $"{OneLine(OverflowResponse)}, {chunkSize}");

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(connection, null, connectionNumber: 0)))
                .ExecuteAsync(ReuseContext(events, output));

            WriteResult(result);
            WriteEvents("events", events.Events);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff("output", "hello", Encoding.ASCII.GetString(output.ToArray()));
            Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()), $"Chunk size {chunkSize}");
            WriteExpectedLines(
                "events 3 to 6",
                ["< HTTP/1.1 200 OK\r\n", "* Overflow Content-Length: value", "< Content-Length: 99999999999999999999\r\n", "< \r\n"],
                events.Events.Skip(3).Take(4));
            CollectionAssert.AreEqual(
                new[] { "< HTTP/1.1 200 OK\r\n", "* Overflow Content-Length: value", "< Content-Length: 99999999999999999999\r\n", "< \r\n" },
                events.Events.Skip(3).Take(4).ToArray(),
                $"Chunk size {chunkSize}");
            Diagnostics.Assert("connection marked reusable", false, connection.IsMarkedReusable);
            Assert.IsFalse(connection.IsMarkedReusable, $"Chunk size {chunkSize}");
            WriteExpectedLines("info lines", InfoLines("Overflow Content-Length: value", "shutting down connection #0"), events.Info);
            CollectionAssert.AreEqual(
                InfoLines("Overflow Content-Length: value", "shutting down connection #0"), events.Info, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_OverflowingContentLengthUnderMaxFileSize_FailsWithExit63BeforeTheHeaderLine()
    {
        // curl -s -v --max-filesize 10 against the same response.
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            Diagnostics.Arrange("response, max file size, chunk size", $"{OneLine(OverflowResponse)}, 10, {chunkSize}");

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(OverflowResponse, chunkSize), null, connectionNumber: 0)))
                .ExecuteAsync(ReuseContext(events, output, maxFileSize: 10));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.FilesizeExceeded, result.ExitCode);
            Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("error message", "Maximum file size exceeded", result.ErrorMessage ?? "(none)");
            Assert.AreEqual("Maximum file size exceeded", result.ErrorMessage, $"Chunk size {chunkSize}");
            Diagnostics.Assert("output length", 0, output.Length);
            Assert.AreEqual(0, output.Length, $"Chunk size {chunkSize}");
            Diagnostics.Assert("a Content-Length header line was reported", false, events.Events.Any(line => line.StartsWith("< Content-Length", StringComparison.Ordinal)));
            Assert.IsFalse(events.Events.Any(line => line.StartsWith("< Content-Length", StringComparison.Ordinal)), $"Chunk size {chunkSize}");
            CollectionAssert.DoesNotContain(events.Info, "Overflow Content-Length: value", $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello", true, DisplayName = "--ignore-content-length")]
    [DataRow("HTTP/1.1 204 No Content\r\nContent-Length: 99999999999999999999\r\n\r\n", false, DisplayName = "204")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", false, DisplayName = "Length that fits")]
    public async Task ExecuteAsync_ContentLengthNotReadOrThatFits_ReportsNoOverflow(string response, bool ignoreContentLength)
    {
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("response, max file size, ignore content length", $"{OneLine(response)}, 10, {ignoreContentLength}");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(ReuseContext(events, maxFileSize: 10, http: new HttpRequestOptions { IgnoreContentLength = ignoreContentLength }));

        WriteResult(result);
        Diagnostics.Assert("info has overflow line", false, events.Info.Contains("Overflow Content-Length: value"));
        Diagnostics.Assert("info has file size line", false, events.Info.Contains("Maximum file size exceeded"));
        CollectionAssert.DoesNotContain(events.Info, "Overflow Content-Length: value");
        CollectionAssert.DoesNotContain(events.Info, "Maximum file size exceeded");
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedBodyWithBytesAfterItInTheSameRead_ReportsLeftoversBeforeTheConnectionEnd()
    {
        // curl -s -v against a chunked 200 followed by EXTRA, all in one write.
        RecordingTransferEvents events = new();
        MemoryStream output = new();
        Diagnostics.Arrange("response, chunk size", $"{OneLine(ChunkedWithLeftovers)}, 65536");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(ChunkedWithLeftovers, 65536), null, connectionNumber: 0)))
            .ExecuteAsync(ReuseContext(events, output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Diff("output", "hello", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        WriteExpectedLines(
            "last 3 events",
            ["{ 5\r\nhello\r\n0\r\n\r\nEXTRA", "* Leftovers after chunking: 5 bytes", "* Connection #0 to host 127.0.0.1:18977 left intact"],
            events.Events.TakeLast(3));
        CollectionAssert.AreEqual(
            new[] { "{ 5\r\nhello\r\n0\r\n\r\nEXTRA", "* Leftovers after chunking: 5 bytes", "* Connection #0 to host 127.0.0.1:18977 left intact" },
            events.Events.TakeLast(3).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_ChunkedBodyEndingItsRead_ReportsNoLeftovers()
    {
        // One byte a read: the bytes after the body arrive in later reads than its last byte,
        // so none is left in the read that completes it.
        RecordingTransferEvents events = new();
        ScriptedConnection connection = Connection(ChunkedWithLeftovers, 1);
        Diagnostics.Arrange("response, chunk size", $"{OneLine(ChunkedWithLeftovers)}, 1");

        TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ReuseContext(events));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("a leftovers line was reported", false, events.Info.Any(line => line.StartsWith("Leftovers after chunking", StringComparison.Ordinal)));
        Assert.IsFalse(events.Info.Any(line => line.StartsWith("Leftovers after chunking", StringComparison.Ordinal)));
    }
}
