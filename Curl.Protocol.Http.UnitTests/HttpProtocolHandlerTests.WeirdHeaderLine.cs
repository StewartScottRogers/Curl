using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// Header lines curl 8.21.0 refuses with exit 8 as it reads an HTTP/1.x head: a NUL byte in a
/// line, and a second <c>Location</c> that differs from the first. Measured 2026-10-03 with
/// <c>-sv</c> and <c>Record-CurlExchange.ps1</c> (BL-1331): the offending line is never echoed,
/// the message is a <c>-v</c> line, and nothing reaches the output.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_NulByteInAHeaderLine_FailsWithWeirdServerReplyBeforeReportingIt()
    {
        await AssertWeirdHeaderLineAsync(
            "HTTP/1.1 200 OK\r\nX-A: a\0b\r\nContent-Length: 2\r\n\r\nok",
            "",
            "Nul byte in header",
            ["< HTTP/1.1 200 OK\r\n"]);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondDifferentLocation_FailsWithWeirdServerReplyBeforeReportingIt()
    {
        await AssertWeirdHeaderLineAsync(
            "HTTP/1.1 200 OK\r\nLocation: /a\r\nLocation: /b\r\nContent-Length: 2\r\n\r\nok",
            "",
            "Multiple Location headers",
            ["< HTTP/1.1 200 OK\r\n", "< Location: /a\r\n"]);
    }

    [TestMethod]
    public async Task ExecuteAsync_SecondDifferentLocationInA302WithL_FailsWithoutFollowing()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            ScriptedConnection connection = Connection("HTTP/1.1 302 Found\r\nLocation: /a\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n", chunkSize);
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size, options", $"{chunkSize}, -L");
            Diagnostics.Arrange("scripted response", "302, Location: /a, Location: /b, Content-Length: 0");

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(RefusedHeaderContext(output, null, "-L"));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
            Diagnostics.Assert("error text", "Multiple Location headers", result.ErrorMessage);
            Diagnostics.Assert("requests sent", 1, Latin1(connection.Written).Split("GET ").Length - 1);
            Diagnostics.Assert("output length", 0L, output.Length);
            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Multiple Location headers", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, Latin1(connection.Written).Split("GET ").Length - 1, $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    [DataRow("Location: /a\r\nLocation: /a\r\n", DisplayName = "The same value twice")]
    [DataRow("Location: /a\r\nLocation:\r\n", DisplayName = "An empty value after it")]
    [DataRow("Location:\r\nLocation: /a\r\n", DisplayName = "An empty value before it")]
    public async Task ExecuteAsync_RepeatedOrEmptyLocation_IsAccepted(string locations)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("location lines", OneLine(locations));

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\n" + locations + "Content-Length: 2\r\n\r\nok", chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, null, string.Empty));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Diagnostics.Assert("body", "ok", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    private async Task AssertWeirdHeaderLineAsync(string response, string options, string message, string[] reportedHead)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size, options", $"{chunkSize}, {(options.Length == 0 ? "(none)" : options)}");
            Diagnostics.Arrange("scripted response", OneLine(response.Replace("\0", "\\0", StringComparison.Ordinal)));

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, null, options, events));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
            Diagnostics.Assert("error text", message, result.ErrorMessage);
            WriteExpectedLines("reported head", reportedHead, HeadEvents(events));
            Diagnostics.Assert("output length", 0L, output.Length);
            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
            CollectionAssert.AreEqual(reportedHead, HeadEvents(events), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
        }
    }
}
