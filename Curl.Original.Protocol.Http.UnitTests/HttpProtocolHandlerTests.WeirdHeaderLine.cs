using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

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

    /// <summary>
    /// libcurl 8.22.0 writes each header line once it accepts it, so the head before the second
    /// <c>Location</c> reaches the header output and the refused line does not (upstream test580, BL-2587).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_SecondDifferentLocationWithHeaderOutput_WritesTheHeadLinesBeforeIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "302, X-Before, Location: /a, Location: /b; header output is the output");

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 302 eat this!\r\nX-Before: 1\r\nLocation: /a\r\nLocation: /b\r\nContent-Length: 0\r\n\r\n", chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, output, string.Empty));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
            Diagnostics.Diff("output", OneLine("HTTP/1.1 302 eat this!\r\nX-Before: 1\r\nLocation: /a\r\n"), OneLine(Latin1(output.ToArray())));
            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Multiple Location headers", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 302 eat this!\r\nX-Before: 1\r\nLocation: /a\r\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// curl 8.22.0 writes each header line once its line ends, so a header held in case the next
    /// line folds into it still reaches the header output when that next line fails the head
    /// (upstream test415, BL-2867; measured with <c>-i</c>: the status and <c>Date</c> lines, exit 8).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_HeaderLineFailsAfterAHeldHeaderWithHeaderOutput_WritesTheLinesBeforeIt()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "200, Date, a NUL byte line; header output is the output");

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 200 OK\r\nDate: today\r\nX-A: a\0b\r\nContent-Length: 2\r\n\r\nok", chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, output, string.Empty));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
            Diagnostics.Diff("output", OneLine("HTTP/1.1 200 OK\r\nDate: today\r\n"), OneLine(Latin1(output.ToArray())));
            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: today\r\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// A peer that closes after a 1xx head ends the transfer with exit 52, and curl 8.22.0 has
    /// already written that head to <c>-i</c>'s output (upstream test158, BL-2867).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ClosedAfterAnInformationalHeadWithHeaderOutput_WritesThatHead()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "100 Continue with one header, then close; header output is the output");

            TransferResult result = await Handler(QueueConnector.For(Connection("HTTP/1.1 100 Continue\r\nSilly-header: yes\r\n\r\n", chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, output, string.Empty));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
            Diagnostics.Diff("output", OneLine("HTTP/1.1 100 Continue\r\nSilly-header: yes\r\n\r\n"), OneLine(Latin1(output.ToArray())));
            Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("HTTP/1.1 100 Continue\r\nSilly-header: yes\r\n\r\n", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// A 1xx head followed by a line that is no status line - a bare <c>100</c> answering with
    /// a body - ends the transfer with exit 8, and curl 8.22.0 has already written that head to
    /// <c>-i</c>'s output (differential case status-100, measured, BL-3280).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_BodyAfterAnInformationalHeadWithHeaderOutput_WritesThatHead()
    {
        const string head = "HTTP/1.1 100 Status\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "100 with Content-Length and a body; header output is the output");

            TransferResult result = await Handler(QueueConnector.For(Connection(head + "status 100\n", chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, output, string.Empty));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, result.ExitCode);
            Diagnostics.Diff("output", OneLine(head), OneLine(Latin1(output.ToArray())));
            Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(head, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// The same failure keeps the 1xx head's code, version, size and Content-Type in its report, as libcurl 8.22.0
    /// answers <c>CURLINFO_RESPONSE_CODE</c>, <c>CURLINFO_HEADER_SIZE</c> and <c>CURLINFO_CONTENT_TYPE</c> after it
    /// (differential case get/status/100, BL-3301).
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_BodyAfterAnInformationalHead_ReportsThatHeadsCodeSizeAndContentType()
    {
        const string head = "HTTP/1.1 100 Status\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n\r\n";
        Diagnostics.Arrange("scripted response", "100 with Content-Type and a body");

        TransferResult result = await Handler(QueueConnector.For(Connection(head + "status 100\n", 1024)))
            .ExecuteAsync(RefusedHeaderContext(new MemoryStream(), null, string.Empty));

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, result.ExitCode);
        Assert.AreEqual(100, result.Report!.ResponseCode);
        Assert.AreEqual(head.Length, result.Report!.HeaderSize);
        Assert.AreEqual("text/plain", result.Report!.ContentType);
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
