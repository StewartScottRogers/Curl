using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Drives <see cref="HttpProtocolHandler" />'s resends after <c>417 Expectation Failed</c>
/// against the <c>--max-redirs</c> limit: curl 8.21.0 counts every resend as a followed
/// redirect, and an <c>-H "Expect: 100-continue"</c> upload answered 417 while its body is sent
/// waits again on every resend, so it loops until the limit ends it with exit 47. Every count,
/// message and header output is what curl produced against a loopback server that answered
/// every request with the 54-byte 417 head once 65536 body bytes arrived (BL-396 Notes). Each
/// exchange is replayed with 1-byte reads and with one read.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    /// <summary>
    /// Measured: <c>curl -T big.bin -H "Expect: 100-continue"</c>, with and without <c>-L</c>:
    /// 51 connections, each waiting for <c>100 Continue</c> and drawing the 417; exit 47
    /// <c>Maximum (50) redirects followed</c>, <c>-D</c> 51 heads (2754 bytes),
    /// <c>%{num_connects}</c> 51, <c>%{num_redirects}</c> 50. With <c>--max-redirs 3</c>: 4
    /// connections, 216 bytes, 3 redirects. With <c>--max-redirs 0</c>: 1 connection, 54 bytes,
    /// no resend.
    /// </summary>
    /// <param name="maxRedirects">The <c>--max-redirs</c> limit.</param>
    /// <param name="followRedirects">Whether <c>-L</c> is given; it changes nothing.</param>
    [TestMethod]
    [DataRow(50, false, DisplayName = "default limit")]
    [DataRow(50, true, DisplayName = "default limit, -L")]
    [DataRow(3, false, DisplayName = "--max-redirs 3")]
    [DataRow(3, true, DisplayName = "--max-redirs 3, -L")]
    [DataRow(0, false, DisplayName = "--max-redirs 0")]
    public async Task ExecuteAsync_CustomExpect417WhileSending_ResendsUntilTheRedirectLimitThenExits47(int maxRedirects, bool followRedirects)
    {
        int connections = maxRedirects + 1;
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection[] sent = [.. Enumerable.Range(0, connections).Select(_ => FailingWhileSending(ExpectationFailedHead, chunkSize, CustomExpectPut.Length))];
            QueueConnector connector = QueueConnector.For(sent);
            MemoryStream headerOutput = new();
            HttpRequestOptions options = new() { Headers = ["Expect: 100-continue"], MaxRedirects = maxRedirects, FollowRedirects = followRedirects };
            TransferContext context = CustomExpectUploadContext(options, new MemoryStream(), headerOutput);

            TransferResult result = await RunPastEveryWaitAsync(connector, context);

            Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual($"Maximum ({maxRedirects}) redirects followed", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.HasCount(connections, connector.Targets, $"Chunk size {chunkSize}");
            foreach (GatedConnection connection in sent)
            {
                Assert.AreEqual(CustomExpectPut + BigBody[..FirstPiece], Latin1(connection.Written), $"Chunk size {chunkSize}");
            }

            Assert.AreEqual(string.Concat(Enumerable.Repeat(ExpectationFailedHead, connections)), Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(54L * connections, result.Report!.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(417, result.Report.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(connections, result.Report.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.AreEqual(maxRedirects, result.Report.RedirectCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// <c>--max-redirs -1</c> sets no limit, so the loop goes on while 417s come; here a 200
    /// on the third connection, answered during its wait so its body is never sent, ends it
    /// with two resends counted as redirects.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_CustomExpect417WhileSendingWithoutALimit_ResendsUntilAnotherStatus()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(ExpectationFailedHead, chunkSize, CustomExpectPut.Length);
            GatedConnection second = FailingWhileSending(ExpectationFailedHead, chunkSize, CustomExpectPut.Length);
            TurnTakingConnection third = new(chunkSize, OkHead + "ok");
            MemoryStream output = new();
            HttpRequestOptions options = new() { Headers = ["Expect: 100-continue"], MaxRedirects = -1 };
            TransferContext context = CustomExpectUploadContext(options, output, null);

            TransferResult result = await RunPastEveryWaitAsync(QueueConnector.For(first, second, third), context);

            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(CustomExpectPut, third.Written, $"Chunk size {chunkSize}");
            Assert.AreEqual("ok", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(3, result.Report!.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.AreEqual(2, result.Report.RedirectCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: without <c>-H</c>, curl's own <c>Expect</c> upload answered 417 while sending
    /// and then 200 resends once without the wait, and <c>%{num_redirects}</c> is 1; with
    /// <c>--max-redirs 0</c> it does not resend: exit 47 <c>Maximum (0) redirects followed</c>,
    /// one connection, 54 header bytes.
    /// </summary>
    /// <param name="maxRedirects">The <c>--max-redirs</c> limit.</param>
    /// <param name="exitCode">The exit code curl returned.</param>
    /// <param name="connections">The connections curl made.</param>
    /// <param name="redirects">curl's <c>%{num_redirects}</c>.</param>
    [TestMethod]
    [DataRow(50, CurlExitCode.Ok, 2, 1, DisplayName = "default limit")]
    [DataRow(0, CurlExitCode.TooManyRedirects, 1, 0, DisplayName = "--max-redirs 0")]
    public async Task ExecuteAsync_OwnExpect417WhileSending_CountsTheResendAsARedirect(int maxRedirects, CurlExitCode exitCode, int connections, int redirects)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            GatedConnection first = FailingWhileSending(ExpectationFailedHead, chunkSize, ExpectingHead.Length);
            TurnTakingConnection second = new(chunkSize, OkHead + "ok");
            QueueConnector connector = QueueConnector.For(first, second);

            TransferResult result = await RunPastTheWaitAsync(connector, BigBodyOptions() with { MaxRedirects = maxRedirects }, new MemoryStream(), null);

            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.HasCount(connections, connector.Targets, $"Chunk size {chunkSize}");
            Assert.AreEqual(connections, result.Report!.ConnectionCount, $"Chunk size {chunkSize}");
            Assert.AreEqual(redirects, result.Report.RedirectCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// Measured: <c>curl --data-binary @big.bin --max-redirs 0</c> answered 417 during the wait
    /// for <c>100 Continue</c> does not resend: exit 47 <c>Maximum (0) redirects followed</c>,
    /// only the head sent, <c>417 176 0 54 1 0</c>. The resend it makes without the limit is
    /// counted as a redirect the same way.
    /// </summary>
    /// <param name="maxRedirects">The <c>--max-redirs</c> limit.</param>
    [TestMethod]
    [DataRow(0, DisplayName = "--max-redirs 0")]
    [DataRow(50, DisplayName = "default limit")]
    public async Task ExecuteAsync_417WhileWaitingForContinue_CountsTheResendAgainstTheRedirectLimit(int maxRedirects)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ExpectationFailedHead, OkHead + "ok");
            MemoryStream headerOutput = new();

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ExpectContext(BigBodyOptions() with { MaxRedirects = maxRedirects }, new MemoryStream(), headerOutput));

            if (maxRedirects == 0)
            {
                Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode, $"Chunk size {chunkSize}");
                Assert.AreEqual("Maximum (0) redirects followed", result.ErrorMessage, $"Chunk size {chunkSize}");
                Assert.AreEqual(ExpectingHead, connection.Written, $"Chunk size {chunkSize}");
                Assert.AreEqual(ExpectationFailedHead, Latin1(headerOutput.ToArray()), $"Chunk size {chunkSize}");
                Assert.AreEqual(0L, result.Report!.UploadSize, $"Chunk size {chunkSize}");
                Assert.AreEqual(0, result.Report.RedirectCount, $"Chunk size {chunkSize}");
            }
            else
            {
                Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
                Assert.AreEqual(1, result.Report!.RedirectCount, $"Chunk size {chunkSize}");
            }
        }
    }

    /// <summary>
    /// A resend <c>-L</c> hands on after hops it already followed shares their count: with
    /// <c>--max-redirs 3</c> and three redirects followed before this request, its first 417
    /// ends the transfer with exit 47 and no resend.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_417WhenEarlierHopsUsedTheLimit_Exits47WithoutResending()
    {
        foreach (int chunkSize in ChunkSizes)
        {
            TurnTakingConnection connection = new(chunkSize, ExpectationFailedHead, OkHead + "ok");
            HttpRequestOptions options = BigBodyOptions() with { MaxRedirects = 3, RedirectsFollowed = 3 };

            TransferResult result = await Handler(QueueConnector.For(connection)).ExecuteAsync(ExpectContext(options, new MemoryStream(), null));

            Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual("Maximum (3) redirects followed", result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(0, result.Report!.RedirectCount, $"Chunk size {chunkSize}");
        }
    }

    /// <summary>
    /// The <c>-T big.bin -H "Expect: 100-continue"</c> head: curl sends no <c>Expect</c> of its
    /// own, so the <c>-H</c> line takes its place among the custom headers.
    /// </summary>
    private static string CustomExpectPut => "PUT /p HTTP/1.1\r\nHost: 127.0.0.1:18260\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
        + "Expect: 100-continue\r\nContent-Length: 1048577\r\n\r\n";

    /// <summary>
    /// A <c>-T</c> upload of <see cref="BigBody" /> from a seekable stream, sent with
    /// <paramref name="options" />.
    /// </summary>
    private static TransferContext CustomExpectUploadContext(HttpRequestOptions options, Stream output, Stream? headerOutput) =>
        new()
        {
            Url = CurlUrl.Parse(ExpectUrl),
            Output = output,
            HeaderOutput = headerOutput,
            Upload = new MemoryStream(Encoding.Latin1.GetBytes(BigBody)),
            Http = options,
            TimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch),
        };

    /// <summary>
    /// Runs the transfer and lets every wait for <c>100 Continue</c> run out in turn, one per
    /// connection, until the transfer ends.
    /// </summary>
    private static async Task<TransferResult> RunPastEveryWaitAsync(QueueConnector connector, TransferContext context)
    {
        FakeTimeProvider time = (FakeTimeProvider)context.TimeProvider;
        Task<TransferResult> transfer = Handler(connector).ExecuteAsync(context).AsTask();
        for (int wait = 1; await Task.WhenAny(transfer, time.TimerCreatedAsync(HttpContinueWaitConnection.ContinueWait, wait)) != transfer; wait++)
        {
            time.Advance(HttpContinueWaitConnection.ContinueWait);
        }

        return await transfer;
    }
}
