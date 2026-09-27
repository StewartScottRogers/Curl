using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s resend without <c>Expect</c> after a
/// <c>417 Expectation Failed</c> through <see cref="TurnTakingConnection" /> and
/// <see cref="QueueConnector" />, never a socket. Every request, header output and report
/// value is what curl 8.21.0 produced against a loopback server on port 18260 that answered
/// the <c>Expect: 100-continue</c> head at once; the commands are in the BL-260 Notes. Each
/// exchange is replayed with 1-byte reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string ExpectUrl = "http://127.0.0.1:18260/p";

    private const string ExpectingHead = "POST /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
        + "Content-Length: 1048577\r\nContent-Type: application/x-www-form-urlencoded\r\nExpect: 100-continue\r\n\r\n";

    private const string ResentHead = "POST /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
        + "Content-Length: 1048577\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n";

    private const string ExpectationFailedHead = "HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\n\r\n";

    private static readonly string BigBody = new('a', 1048577);

    /// <summary>
    /// Measured: <c>curl --data-binary @big.bin -D h -w "%{http_code} %{size_request}
    /// %{size_upload} %{size_header} %{num_connects}"</c> with 1048577 bytes, answered 417 then
    /// 200: the resend drops <c>Expect</c> and follows its head with the body on the same
    /// connection; <c>-D</c> holds both heads, stdout only <c>ok</c>; <c>200 1048909 1048577 92 1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhileWaitingForContinue_ResendsWithoutExpectOnTheSameConnection()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ExpectationFailedHead, OkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(connector).ExecuteAsync(ExpectContext(BigBodyOptions(), output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead + ResentHead + BigBody, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectationFailedHead + OkHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(200, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048909L, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048577L, result.Report.UploadSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(92L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, result.Report.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a 417 with a 4-byte body <c>nope</c> is read and discarded; stdout holds only
    /// the resend's <c>ok</c>, and the report is <c>200 1048909 1048577 92 1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WithABody_DiscardsItsBody()
    {
        const string failedWithBody = "HTTP/1.1 417 Expectation Failed\r\nContent-Length: 4\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, failedWithBody + "nope", OkHead + "ok");
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(ExpectContext(BigBodyOptions(), output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead + ResentHead + BigBody, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(failedWithBody + OkHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(92L, result.Report!.HeaderSize, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a resend answered 417 again is the result, exit 0, with nothing on stdout,
    /// both 417 heads in <c>-D</c> and <c>417 1048909 1048577 108 1</c>; nothing is sent a
    /// third time.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_ResendAlsoAnswered417_ReturnsTheSecond417()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ExpectationFailedHead, ExpectationFailedHead, OkHead + "ok");
            MemoryStream output = new();
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(ExpectContext(BigBodyOptions(), output, headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead + ResentHead + BigBody, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectationFailedHead + ExpectationFailedHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            Assert.AreEqual(417, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(1048909L, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(108L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(1, result.Report.ConnectionCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: a 417 with <c>Connection: close</c> is the result, exit 0, with no resend:
    /// <c>417 177 0 73 1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417ClosesTheConnection_ReturnsItWithoutResending()
    {
        const string closingFailed = "HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, closingFailed, OkHead + "ok");
            QueueConnector connector = QueueConnector.For(connection);
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(connector).ExecuteAsync(ExpectContext(BigBodyOptions(), new MemoryStream(), headerOutput));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(ExpectingHead, connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual(closingFailed, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(417, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(177L, result.Report.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(73L, result.Report.HeaderSize, $"Chunk size {chunkSize}");
            Assert.HasCount(1, connector.Targets, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>-f</c> fails on the 417 itself with exit 22 and no resend
    /// (<c>417 177 0 54 1</c>); <c>--fail-with-body</c> resends and fails on a second 417
    /// (<c>417 1048909 108 1</c>, exit 22).
    /// </summary>
    /// <param name="fail">The fail mode.</param>
    /// <param name="requestSize">The measured <c>%{size_request}</c>.</param>
    [TestMethod]
    [DataRow(HttpFailMode.Fail, 177L, DisplayName = "-f")]
    [DataRow(HttpFailMode.FailWithBody, 1048909L, DisplayName = "--fail-with-body")]
    public async Task ExecuteAsync_417UnderFail_FailsWithExit22(HttpFailMode fail, long requestSize)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ExpectationFailedHead, ExpectationFailedHead);
            HttpRequestOptions options = BigBodyOptions() with { Fail = fail };

            TransferResult result = await Handler(QueueConnector.For(connection))
                .ExecuteAsync(ExpectContext(options, new MemoryStream(), new MemoryStream()));

            Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("The requested URL returned error: 417", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(requestSize, result.Report!.RequestSize, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl -d hi -H "Expect: 100-continue"</c> answered 417 then 200 resends
    /// the same 171-byte head, <c>Expect</c> line included, with <c>hi</c> straight after it
    /// and no wait; <c>200 344 2 92 1</c>.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417ToACustomExpect_ResendsTheCustomLineWithoutWaiting()
    {
        const string head = "POST /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Expect: 100-continue\r\nContent-Length: 2\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n";
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ExpectationFailedHead, OkHead + "ok");
            MemoryStream output = new();
            HttpRequestOptions options = new()
            {
                Headers = ["Expect: 100-continue"],
                Body = new BytesBody("hi"u8.ToArray(), "application/x-www-form-urlencoded"),
            };

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ExpectContext(options, output, null));

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(head + head + "hi", connection.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(344L, result.Report!.RequestSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(2L, result.Report.UploadSize, $"Chunk size {chunkSize}");
        }
    }

    private static HttpRequestOptions BigBodyOptions() =>
        new() { Body = new BytesBody(System.Text.Encoding.Latin1.GetBytes(BigBody), "application/x-www-form-urlencoded") };

    private static TransferContext ExpectContext(HttpRequestOptions options, Stream output, Stream? headerOutput) =>
        new()
        {
            Url = new Uri(ExpectUrl),
            Output = output,
            HeaderOutput = headerOutput,
            Http = options,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };
}
