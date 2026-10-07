using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// The limit of 5000 response headers. Every case was measured on curl 8.21.0 with
/// <c>-sv -i</c> against a loopback server with <c>Record-CurlExchange.ps1</c> (BL-1431 Notes):
/// the 5001st header of a transfer's heads, 1xx heads' included and continuation lines folded
/// into the header before them, fails with exit 100; it is still written as the last <c>-v</c>
/// <c>&lt;</c> line but not to the header output, which ends with the 5000th.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string TooManyResponseHeaders = "Too many response headers, 5000 is max";

    private static readonly int[] ManyHeaderChunkSizes = [7, 65536];

    [TestMethod]
    public async Task ExecuteAsync_FinalHeadOf5000Headers_Succeeds()
    {
        string response = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n" + NumberedHeaders(1, 4999) + "\r\nabc";

        foreach (int chunkSize in ManyHeaderChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headers = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", "200, Content-Length: 3, X: 1 to X: 4999, body abc (5000 headers)");

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, headers, string.Empty));

            WriteResult(result);
            Diagnostics.Assert("header output length", response.Length - 3, headers.Length);
            Diagnostics.Assert("body", "abc", Latin1(output.ToArray()));
            Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(response[..^3], Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual("abc", Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_FinalHeadOf5001Headers_FailsWithTooLargeAfterWritingTheFirst5000()
    {
        string accepted = "HTTP/1.1 200 OK\r\nContent-Length: 3\r\n" + NumberedHeaders(1, 4999);

        await AssertTooManyHeadersAsync(accepted + "X: 5000\r\n\r\nabc", accepted, "X: 5000\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadersOfA100HeadAndTheFinalHead_CountTogether()
    {
        string accepted = "HTTP/1.1 100 Continue\r\nA: 1\r\nB: 2\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\n" + NumberedHeaders(1, 4997);

        await AssertTooManyHeadersAsync(accepted + "X: 4998\r\n\r\nabc", accepted, "X: 4998\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinuationLines_DoNotCountAsHeaders()
    {
        StringBuilder folded = new("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n");
        for (int number = 1; number <= 4999; number++)
        {
            folded.Append("X: ").Append(number).Append("\r\n folded\r\n");
        }

        string response = folded.Append("\r\nabc").ToString();
        MemoryStream output = new();
        Diagnostics.Arrange("scripted response", "200, Content-Length: 3, X: 1 to X: 4999 each with a continuation line, body abc");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(RefusedHeaderContext(output, new MemoryStream(), string.Empty));

        WriteResult(result);
        Diagnostics.Assert("body", "abc", Latin1(output.ToArray()));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("abc", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_A100HeadOf5001Headers_FailsWithTooLarge()
    {
        string response = "HTTP/1.1 100 Continue\r\n" + NumberedHeaders(1, 5001) + "\r\nHTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";
        RecordingTransferEvents events = new();
        Diagnostics.Arrange("scripted response", "100 Continue with X: 1 to X: 5001, then 200, Content-Length: 0");

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536)))
            .ExecuteAsync(RefusedHeaderContext(new MemoryStream(), new MemoryStream(), string.Empty, events));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.TooLarge, result.ExitCode);
        Diagnostics.Assert("error text", TooManyResponseHeaders, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual(TooManyResponseHeaders, result.ErrorMessage);
        string[] received = events.Events.Where(line => line[0] == '<').ToArray();
        Diagnostics.Assert("last received line", OneLine("< X: 5001\r\n"), OneLine(received[^1]));
        Assert.AreEqual("< X: 5001\r\n", received[^1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeaderPastTheLimitAfterAnEarlierRefusedHeader_FailsWithTheEarlierRefusal()
    {
        string response = "HTTP/1.1 200 OK\r\nContent-Length: x\r\n" + NumberedHeaders(1, 5000) + "\r\nabc";

        await AssertRefusedHeaderAsync(response, string.Empty, "HTTP/1.1 200 OK\r\n", CurlExitCode.WeirdServerReply, InvalidContentLength);
    }

    private async Task AssertTooManyHeadersAsync(string response, string headerOutput, string refusedLine)
    {
        foreach (int chunkSize in ManyHeaderChunkSizes)
        {
            MemoryStream output = new();
            MemoryStream headers = new();
            RecordingTransferEvents events = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response length", response.Length);
            Diagnostics.Arrange("refused line", OneLine(refusedLine));

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(RefusedHeaderContext(output, headers, string.Empty, events));

            WriteResult(result);
            Diagnostics.Assert("error text", TooManyResponseHeaders, result.ErrorMessage);
            Diagnostics.Assert("header output length", headerOutput.Length, headers.Length);
            Diagnostics.Assert("output length", 0L, output.Length);
            Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(TooManyResponseHeaders, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(headerOutput, Latin1(headers.ToArray()), $"Chunk size {chunkSize}");
            Assert.AreEqual(0L, output.Length, $"Chunk size {chunkSize}");
            string[] received = events.Events.Where(line => line[0] == '<').ToArray();
            Assert.AreEqual("< " + refusedLine, received[^1], $"Chunk size {chunkSize}");
            Assert.AreEqual(1, received.Count(line => line == "< " + refusedLine), $"Chunk size {chunkSize}");
        }
    }

    private static string NumberedHeaders(int first, int last)
    {
        StringBuilder headers = new();
        for (int number = first; number <= last; number++)
        {
            headers.Append("X: ").Append(number).Append("\r\n");
        }

        return headers.ToString();
    }
}
