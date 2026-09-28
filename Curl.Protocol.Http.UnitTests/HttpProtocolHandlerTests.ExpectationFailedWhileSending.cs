using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s resend after a <c>417 Expectation Failed</c>
/// that arrives once the <c>100 Continue</c> wait ran out, while the body is being sent. The
/// first connection is a <see cref="GatedConnection" /> that answers 417 once the head and
/// one 64 KiB piece of body have arrived and then takes no more; every request, header output
/// and report value is what curl 8.21.0 produced against a loopback server that answered 417
/// after reading body bytes (BL-319 Notes). Each exchange is replayed with 1-byte reads and
/// with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const int FirstPiece = 65536;

    /// <summary>
    /// Measured: <c>curl --data-binary @big.bin</c> (1048577 bytes), 417 after the first
    /// 65536 body bytes: <c>Got HTTP failure 417 while sending data</c>, sending stops, the
    /// connection is shut, and the request goes again without <c>Expect</c> and with the
    /// whole body on a second connection; <c>-D</c> holds both heads, stdout <c>ok</c>,
    /// <c>200 1114445 1048577 92 2</c>, exit 0. <c>--fail-with-body</c> does the same.
    /// </summary>
    /// <param name="fail">The fail mode.</param>
    [TestMethod]
    [DataRow(HttpFailMode.None, DisplayName = "no -f")]
    [DataRow(HttpFailMode.FailWithBody, DisplayName = "--fail-with-body")]
    public async Task ExecuteAsync_417WhileSendingTheBody_ResendsWithoutExpectOnANewConnection(HttpFailMode fail)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(ExpectationFailedHead, chunkSize, ExpectingHead.Length);
            TurnTakingConnection second = new(chunkSize, OkHead + "ok");
            QueueConnector connector = QueueConnector.For(first, second);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await RunPastTheWaitAsync(connector, BigBodyOptions() with { Fail = fail }, output, headerOutput);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead + BigBody[..FirstPiece], Latin1(first.Written), $"Chunk size {chunkSize}");
            Assert.AreEqual(ResentHead + BigBody, second.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectationFailedHead + OkHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(1114445L, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048577L, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(92L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(2, result.Report.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.HasCount(2, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>-f</c> stops sending and fails on the 417 itself with exit 22 and no
    /// resend, one connection, <c>417 65713 65536 54 1</c> when the 417 beats the second piece.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhileSendingUnderFail_StopsSendingAndFailsWithExit22()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(ExpectationFailedHead, chunkSize, ExpectingHead.Length);
            QueueConnector connector = QueueConnector.For(first);

            TransferResult result = await RunPastTheWaitAsync(connector, BigBodyOptions() with { Fail = HttpFailMode.Fail }, new MemoryStream(), null);

            Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("The requested URL returned error: 417", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead.Length + FirstPiece, first.Written.Length, $"Chunk size {chunkSize}");
            Assert.AreEqual(65713L, result.Report!.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(65536L, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(54L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a 417 with <c>Connection: close</c> that arrives while the body is sent stops
    /// the sending (<c>we are done reading and this is set to close, stop send</c>) and is the
    /// result with no resend: exit 0, <c>417 65713 65536 73 1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ClosingA417WhileSending_StopsSendingAndReturnsIt()
    {
        const string closingFailed = "HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(closingFailed, chunkSize, ExpectingHead.Length);
            QueueConnector connector = QueueConnector.For(first);
            MemoryStream headerOutput = new();

            TransferResult result = await RunPastTheWaitAsync(connector, BigBodyOptions(), new MemoryStream(), headerOutput);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(closingFailed, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(417, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(65713L, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(65536L, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(73L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, result.Report.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a 417 that arrives only once the whole body is sent is the result, exit 0,
    /// with no resend: <c>417 1048754 1048577 54 1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417AfterTheWholeBody_ReturnsItWithoutResending()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = new(Encoding.Latin1.GetBytes(ExpectationFailedHead), chunkSize, ExpectingHead.Length + BigBody.Length);
            QueueConnector connector = QueueConnector.For(first);

            TransferResult result = await RunPastTheWaitAsync(connector, BigBodyOptions(), new MemoryStream(), null);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(417, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048754L, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048577L, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(54L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -T big.bin</c> answered 417 while the file is sent rewinds the file and
    /// sends it whole again on a second connection, <c>PUT</c> heads of 127 and then 105 bytes;
    /// stdout <c>ok</c>, <c>%{size_upload}</c> 1048577, <c>%{num_connects}</c> 2.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhileSendingAFile_RewindsTheFileForTheResend()
    {
        const string expectingPut = "PUT /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 1048577\r\nExpect: 100-continue\r\n\r\n";
        const string resentPut = "PUT /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Content-Length: 1048577\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(ExpectationFailedHead, chunkSize, expectingPut.Length);
            TurnTakingConnection second = new(chunkSize, OkHead + "ok");
            QueueConnector connector = QueueConnector.For(first, second);
            MemoryStream output = new();
            TransferContext context = UploadExpectContext(new MemoryStream(Encoding.Latin1.GetBytes(BigBody)), output);

            TransferResult result = await RunPastTheWaitAsync(connector, context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expectingPut + BigBody[..FirstPiece], Latin1(first.Written), $"Chunk size {chunkSize}");
            Assert.AreEqual(resentPut + BigBody, second.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(127L + FirstPiece + 105 + 1048577, result.Report!.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048577L, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(2, result.Report.ConnectionCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -T -</c> with 1048576 bytes on stdin, answered 417 after the first
    /// 65532-byte chunk (65524 bytes of data), cannot rewind stdin and does not fail: the resend
    /// on a second connection, without <c>Expect</c>, sends stdin from byte 65524 on, so the
    /// server receives 983052 bytes; exit 0, stdout <c>ok</c>, <c>%{num_connects}</c> 2.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhileSendingStandardInput_ResendsTheRestOfIt()
    {
        const string expectingPut = "PUT /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\nExpect: 100-continue\r\n\r\n";
        const string resentPut = "PUT /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Transfer-Encoding: chunked\r\n\r\n";
        byte[] standardInput = Encoding.Latin1.GetBytes(string.Concat(Enumerable.Range(0, 131072).Select(line => $"{line:D7}\n")));
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = new(Encoding.Latin1.GetBytes(ExpectationFailedHead), chunkSize, expectingPut.Length + 65532)
            {
                StallsWritesOnceReleased = true,
            };
            TurnTakingConnection second = new(chunkSize, OkHead + "ok");
            MemoryStream output = new();
            TransferContext context = UploadExpectContext(StandardInput(standardInput), output);

            TransferResult result = await RunPastTheWaitAsync(QueueConnector.For(first, second), context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            string firstSent = Latin1(first.Written);
            Assert.AreEqual(expectingPut + "fff4\r\n" + Latin1(standardInput[..65524]) + "\r\n", firstSent, $"Chunk size {chunkSize}");
            Assert.StartsWith(resentPut, second.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(Latin1(standardInput[65524..]), Unchunked(second.Written[resentPut.Length..]), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(2, result.Report!.ConnectionCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// A connection that answers <paramref name="response" /> once the head and the first
    /// 64 KiB of body have arrived, and takes nothing more after that.
    /// </summary>
    private static GatedConnection FailingWhileSending(string response, int chunkSize, int headLength) =>
        new(Encoding.Latin1.GetBytes(response), chunkSize, headLength + FirstPiece) { StallsWritesOnceReleased = true };

    private static TransferContext UploadExpectContext(Stream upload, Stream output) =>
        new()
        {
            Url = CurlUrl.Parse(ExpectUrl),
            Output = output,
            Upload = upload,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };

    private static Task<TransferResult> RunPastTheWaitAsync(QueueConnector connector, HttpRequestOptions options, Stream output, Stream? headerOutput) =>
        RunPastTheWaitAsync(connector, ExpectContext(options, output, headerOutput));

    /// <summary>
    /// Runs the transfer and lets the one-second wait for <c>100 Continue</c> run out, so the
    /// body starts to go before any reply arrives.
    /// </summary>
    private static async Task<TransferResult> RunPastTheWaitAsync(QueueConnector connector, TransferContext context)
    {
        FakeTimeProvider time = (FakeTimeProvider)context.TimeProvider;
        Task<TransferResult> transfer = Handler(connector).ExecuteAsync(context).AsTask();
        await time.TimerCreatedAsync(HttpContinueWaitConnection.ContinueWait);
        time.Advance(HttpContinueWaitConnection.ContinueWait);
        return await transfer;
    }

    private static string Unchunked(string chunked)
    {
        StringBuilder data = new();
        int offset = 0;
        while (true)
        {
            int lineEnd = chunked.IndexOf("\r\n", offset, StringComparison.Ordinal);
            int size = Convert.ToInt32(chunked[offset..lineEnd], 16);
            if (size == 0)
            {
                return data.ToString();
            }

            data.Append(chunked, lineEnd + 2, size);
            offset = lineEnd + 2 + size + 2;
        }
    }
}
