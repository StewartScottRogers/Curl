using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-203 (BL-3732): <c>--http0.9 -I</c> with an HTTP/0.9 reply
/// of one line or less gives exit 52 <c>Empty reply from server</c>; with bytes after the
/// first line feed, exit 8 <c>Weird server reply</c>. Measured on curl 8.22.0 (Schannel) with a
/// loopback server: <c>hello\r\n</c>, <c>hello</c> and a 147-byte line without a line feed
/// give 52; <c>hello world this is long\r\nmore\r\n</c> and <c>hello\r\nmore</c> give 8.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("hello\r\n", CurlExitCode.GotNothing, "Empty reply from server", DisplayName = "One line")]
    [DataRow("hello", CurlExitCode.GotNothing, "Empty reply from server", DisplayName = "No line feed")]
    [DataRow("hello\r\nmore", CurlExitCode.WeirdServerReply, "Weird server reply", DisplayName = "Bytes after the first line")]
    [DataRow("hello\nmore\n", CurlExitCode.WeirdServerReply, "Weird server reply", DisplayName = "Two lines")]
    public async Task ExecuteAsync_Http09ReplyToHead_ExitsByWhetherBytesFollowTheFirstLine(string response, CurlExitCode exitCode, string message)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            Diagnostics.Arrange("response, chunk size", $"{OneLine(response)}, {chunkSize}");

            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(new TransferContext
                {
                    Url = CurlUrl.Parse(LoopbackUrl),
                    Output = new MemoryStream(),
                    NoBody = true,
                    Http = new Curl.Protocol.Abstractions.HttpRequestOptions { AllowHttp09Reply = true },
                });

            WriteResult(result);
            Diagnostics.Assert("exit code", exitCode, result.ExitCode);
            Assert.AreEqual(exitCode, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(message, result.ErrorMessage, $"Chunk size {chunkSize}");
        }
    }
}
