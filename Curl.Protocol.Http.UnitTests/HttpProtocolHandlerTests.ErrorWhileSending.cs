using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" /> when a final status other than <c>417</c> arrives
/// once the <c>100 Continue</c> wait ran out, while the body is being sent. The connection is
/// a <see cref="GatedConnection" /> that answers once the head and one 64 KiB piece of body
/// have arrived and then takes no more; every request, header output and report value is what
/// curl 8.21.0 produced against <c>Record-CurlExchange.ps1 -RespondAfterBodyBytes 1000</c>
/// (BL-395 Notes). Each exchange is replayed with 1-byte reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ServerErrorHead = "HTTP/1.1 500 Internal Server Error\r\nContent-Length: 4\r\n\r\n";

    private const string MovedHead = "HTTP/1.1 301 Moved Permanently\r\nLocation: /v\r\nContent-Length: 0\r\n\r\n";

    private const string ShuttingDown = "shutting down connection #0";

    /// <summary>
    /// Measured: <c>curl --data-binary @big.bin</c> (1048577 bytes) answered 500 while the body
    /// is sent: <c>HTTP error before end of send, stop sending</c>, <c>abort upload after having
    /// sent 524288 bytes</c>, <c>shutting down connection #0</c>; <c>-D</c> holds the 500 head,
    /// stdout <c>fail</c>, <c>500 524465 524288 57 1</c>, exit 0. How many pieces go before the
    /// 500 is seen is timing; the test pins one.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_500WhileSendingTheBody_StopsSendingAndShutsTheConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection connection = FailingWhileSending(ServerErrorHead + "fail", chunkSize, ExpectingHead.Length);
            QueueConnector connector = QueueConnector.For(connection);
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            MemoryStream headerOutput = new();
            Diagnostics.Arrange("url, chunk size", $"{ExpectUrl}, {chunkSize}");
            Diagnostics.Arrange("scripted connection", "500 after first piece");

            TransferResult result = await RunPastTheWaitAsync(connector, ErrorContext(BigBodyOptions(), output, headerOutput, events));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("written length", ExpectingHead.Length + FirstPiece, connection.Written.Length);
            Assert.AreEqual(ExpectingHead + BigBody[..FirstPiece], Latin1(connection.Written), $"Chunk size {chunkSize}");
            Diagnostics.Diff("header output", ServerErrorHead, Latin1(headerOutput.ToArray()));
            Assert.AreEqual(ServerErrorHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Diagnostics.Diff("output", "fail", Latin1(output.ToArray()));
            Assert.AreEqual("fail", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(500, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(177L + FirstPiece, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual((long)FirstPiece, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(57L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, result.Report.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            WriteEvents("info events", events.Info);
            Diagnostics.Assert("info event", ShuttingDown, events.Info.Contains(ShuttingDown) ? ShuttingDown : "(missing)");
            CollectionAssert.Contains(events.Info, ShuttingDown, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -T big.bin</c> answered 500 while the file is sent stops the same way,
    /// a 127-byte <c>PUT</c> head: <c>500 393343 393216 57 1</c>, exit 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_500WhileSendingAFile_StopsSendingAndShutsTheConnection()
    {
        const string expectingPut = "PUT /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 1048577\r\nExpect: 100-continue\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection connection = FailingWhileSending(ServerErrorHead + "fail", chunkSize, expectingPut.Length);
            QueueConnector connector = QueueConnector.For(connection);
            MemoryStream output = new();
            TransferContext context = UploadExpectContext(new MemoryStream(Encoding.Latin1.GetBytes(BigBody)), output);
            Diagnostics.Arrange("url, chunk size", $"{ExpectUrl}, {chunkSize}");
            Diagnostics.Arrange("upload length, scripted connection", "1048577, 500 after first piece");

            TransferResult result = await RunPastTheWaitAsync(connector, context);

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("written length", expectingPut.Length + FirstPiece, connection.Written.Length);
            Assert.AreEqual(expectingPut + BigBody[..FirstPiece], Latin1(connection.Written), $"Chunk size {chunkSize}");
            Diagnostics.Diff("output", "fail", Latin1(output.ToArray()));
            Assert.AreEqual("fail", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(500, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(127L + FirstPiece, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual((long)FirstPiece, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a 301 while the body is sent stops it the same way, with <c>-L</c> or without:
    /// without, <c>301 393393 393216 67 1</c> and the 301 is the result; with, curl logs
    /// <c>close instead of sending 655361 more bytes</c>, shuts the connection and follows to
    /// <c>/v</c> on a second one, which <see cref="HttpProtocolHandler" /> leaves to the
    /// redirect follower through <see cref="TransferReport.RedirectUrl" />.
    /// </summary>
    /// <param name="followRedirects">Whether <c>-L</c> is given.</param>
    [TestMethod]
    [DataRow(false, DisplayName = "no -L")]
    [DataRow(true, DisplayName = "-L")]
    public async Task ExecuteAsync_301WhileSendingTheBody_StopsSendingAndShutsTheConnection(bool followRedirects)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection connection = FailingWhileSending(MovedHead, chunkSize, ExpectingHead.Length);
            QueueConnector connector = QueueConnector.For(connection);
            RecordingTransferEvents events = new();
            MemoryStream headerOutput = new();
            HttpRequestOptions options = BigBodyOptions() with { FollowRedirects = followRedirects };
            Diagnostics.Arrange("url, chunk size", $"{ExpectUrl}, {chunkSize}");
            Diagnostics.Arrange("follow redirects, scripted connection", $"{followRedirects}, 301 after first piece");

            TransferResult result = await RunPastTheWaitAsync(connector, ErrorContext(options, new MemoryStream(), headerOutput, events));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead + BigBody[..FirstPiece], Latin1(connection.Written), $"Chunk size {chunkSize}");
            Diagnostics.Diff("header output", MovedHead, Latin1(headerOutput.ToArray()));
            Assert.AreEqual(MovedHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(301, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Diagnostics.Diff("redirect url", "http://127.0.0.1:18260/v", result.Report!.RedirectUrl ?? "(none)");
            Assert.AreEqual("http://127.0.0.1:18260/v", result.Report.RedirectUrl, $"Chunk size {chunkSize}");
            Assert.AreEqual(177L + FirstPiece, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual((long)FirstPiece, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(67L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
            WriteEvents("info events", events.Info);
            Diagnostics.Assert("info event", ShuttingDown, events.Info.Contains(ShuttingDown) ? ShuttingDown : "(missing)");
            CollectionAssert.Contains(events.Info, ShuttingDown, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a 200 that arrives while the body is sent lets the whole body go and keeps the
    /// connection: <c>upload completely sent off: 1048577 bytes</c>, <c>Connection #0 to host
    /// 127.0.0.1:18395 left intact</c>, <c>200 1048754 1048577 38 1</c>, exit 0.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_200WhileSendingTheBody_SendsTheWholeBody()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection connection = new(Encoding.Latin1.GetBytes(OkHead + "ok"), chunkSize, ExpectingHead.Length + FirstPiece);
            QueueConnector connector = QueueConnector.For(connection);
            RecordingTransferEvents events = new();
            MemoryStream output = new();
            Diagnostics.Arrange("url, chunk size", $"{ExpectUrl}, {chunkSize}");
            Diagnostics.Arrange("scripted connection", "200 ok after first piece");

            TransferResult result = await RunPastTheWaitAsync(connector, ErrorContext(BigBodyOptions(), output, null, events));

            WriteResult(result);
            Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Diagnostics.Assert("written length", ExpectingHead.Length + BigBody.Length, connection.Written.Length);
            Assert.AreEqual(ExpectingHead + BigBody, Latin1(connection.Written), $"Chunk size {chunkSize}");
            Diagnostics.Diff("output", "ok", Latin1(output.ToArray()));
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(1048754L, result.Report!.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048577L, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(38L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            WriteEvents("info events", events.Info);
            Diagnostics.Assert("shutdown event", "absent", events.Info.Contains(ShuttingDown) ? "present" : "absent");
            CollectionAssert.DoesNotContain(events.Info, ShuttingDown, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured (BL-1527 Notes): curl 8.21.0 <c>-v</c>, a 500 or a 301 without <c>-L</c> arriving
    /// while the body is sent writes <c>HTTP error before end of send, stop sending</c> and
    /// <c>abort upload after having sent N bytes</c> after the head's headers, before its empty line.
    /// </summary>
    /// <param name="response">The status's head, sent after the first piece of body.</param>
    /// <param name="headerLines">The head's lines after its status line, as <c>-v</c> writes them.</param>
    [TestMethod]
    [DataRow(ServerErrorHead + "fail", new[] { "< Content-Length: 4\r\n" }, DisplayName = "500")]
    [DataRow(MovedHead, new[] { "< Location: /v\r\n", "< Content-Length: 0\r\n" }, DisplayName = "301 without -L")]
    public async Task ExecuteAsync_ErrorWhileSendingTheBody_WritesStopSendingAndAbortUploadBeforeTheEmptyLine(string response, string[] headerLines)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection connection = FailingWhileSending(response, chunkSize, ExpectingHead.Length);
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", OneLine(response));

            await RunPastTheWaitAsync(QueueConnector.For(connection), ExpectEventsContext(BigBodyOptions(), events));

            string statusLine = response[..(response.IndexOf('\n', StringComparison.Ordinal) + 1)];
            string[] expected = [
                "< " + statusLine,
                .. headerLines,
                "* HTTP error before end of send, stop sending",
                $"* abort upload after having sent {FirstPiece} bytes",
                "< \r\n",
            ];
            string[] lines = VerboseLinesFromStatusToEmptyLine(events, "< " + statusLine);
            Diagnostics.Diff("lines from the status line to the empty line", string.Join(" | ", expected), string.Join(" | ", lines));
            CollectionAssert.AreEqual(expected, lines, $"Chunk size {chunkSize}: {string.Join(" | ", lines)}");
        }
    }

    /// <summary>
    /// Measured (BL-1527 Notes): curl 8.21.0 <c>-v -L</c>, a 301 arriving while the body is sent
    /// writes <c>Need to rewind upload for next request</c> and <c>close instead of sending N more
    /// bytes</c> after the status line, and <c>Keep sending data to get tossed away</c> before the
    /// empty line, with no stop-sending or abort-upload line.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_301FollowedWhileSendingTheBody_WritesCloseInsteadOfSendingAndKeepSending()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection connection = FailingWhileSending(MovedHead, chunkSize, ExpectingHead.Length);
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("follow redirects, scripted response", "true, 301 after first piece");

            await RunPastTheWaitAsync(QueueConnector.For(connection), ExpectEventsContext(BigBodyOptions() with { FollowRedirects = true }, events));

            string[] expected = [
                "< HTTP/1.1 301 Moved Permanently\r\n",
                "* Need to rewind upload for next request",
                $"* close instead of sending {BigBody.Length - FirstPiece} more bytes",
                "< Location: /v\r\n",
                "< Content-Length: 0\r\n",
                "* Keep sending data to get tossed away",
                "< \r\n",
            ];
            string[] lines = VerboseLinesFromStatusToEmptyLine(events, "< HTTP/1.1 301 Moved Permanently\r\n");
            Diagnostics.Diff("lines from the status line to the empty line", string.Join(" | ", expected), string.Join(" | ", lines));
            CollectionAssert.AreEqual(expected, lines, $"Chunk size {chunkSize}: {string.Join(" | ", lines)}");
        }
    }

    private static string[] VerboseLinesFromStatusToEmptyLine(RecordingTransferEvents events, string statusLine)
    {
        string[] lines = events.Events.Where(line => line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("< ", StringComparison.Ordinal)).ToArray();
        int start = Array.IndexOf(lines, statusLine);
        Assert.IsGreaterThanOrEqualTo(0, start, string.Join(" | ", lines));
        int end = Array.IndexOf(lines, "< \r\n", start);
        Assert.IsGreaterThan(start, end, string.Join(" | ", lines));
        return lines[start..(end + 1)];
    }

    private static TransferContext ErrorContext(HttpRequestOptions options, Stream output, Stream? headerOutput, ITransferEvents events) =>
        new()
        {
            Url = CurlUrl.Parse(ExpectUrl),
            Output = output,
            HeaderOutput = headerOutput,
            Http = options,
            Events = events,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };
}
