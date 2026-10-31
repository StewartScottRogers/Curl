using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What <c>-i</c> shows of a bare <c>HTTP/1.1 100 Continue</c> response (BL-3184): the 100 head
/// alone before the peer closes, and a 100 head followed by the final 200. The expected bytes
/// were measured 2026-10-22 with <c>Record-CurlExchange.ps1</c> against curl 8.21.0 (Schannel)
/// with <c>-i -s</c>: the 100 head is written whole, then the transfer fails with exit 52, or
/// the final head and body follow it.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_IncludeWithABare100ThenClose_ShowsThe100HeadAndFailsWithEmptyReply()
    {
        await AssertIncludeOutputAsync(
            "HTTP/1.1 100 Continue\r\n\r\n",
            CurlExitCode.GotNothing,
            "HTTP/1.1 100 Continue\r\n\r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_IncludeWithA100ThenA200_ShowsBothHeadsAndTheBody()
    {
        await AssertIncludeOutputAsync(
            "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc",
            CurlExitCode.Ok,
            "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc");
    }

    private async Task AssertIncludeOutputAsync(string response, CurlExitCode exitCode, string expected)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", OneLine(response));

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = output, HeaderOutput = output });

            WriteResult(result);
            Diagnostics.Assert("exit code", exitCode, result.ExitCode);
            Diagnostics.Assert("-i output", OneLine(expected), OneLine(Latin1(output.ToArray())));
            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}: {result.ErrorMessage}");
            Assert.AreEqual(expected, Latin1(output.ToArray()), $"Chunk size {chunkSize}");
        }
    }
}
