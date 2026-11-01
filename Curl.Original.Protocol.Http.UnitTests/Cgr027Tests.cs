using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-027 (BL-3556): a peer that closes in the middle of a header
/// line after a Content-Length has shown that Content-Length whole, so curl 8.22.0 (mingw,
/// Schannel) frames the body by it and fails with exit 18; a lone carriage return as the
/// unfinished line is written after the head. Measured on 2026-10-26 with
/// <c>Record-CurlExchange.ps1</c> (BL-3556 Notes).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\n", "Content-Length: 3\r\n", "X", "", "3", DisplayName = "unfinished X")]
    [DataRow("HTTP/1.1 200 OK\r\n", "Content-Length: 10\r\n", "X-Foo: ba", "", "10", DisplayName = "unfinished header")]
    [DataRow("HTTP/1.1 200 OK\r\n", "Content-Length: 3\r\n", "\r", "\r", "3", DisplayName = "unfinished CR, written")]
    [DataRow("HTTP/1.1 404 NF\r\n", "Content-Length: 3\r\n", "X", "", "3", DisplayName = "404 with -f")]
    public async Task ExecuteAsync_PeerClosesInAHeaderLineAfterAContentLength_FailsWithTransferClosed(
        string statusLine, string contentLength, string unfinishedLine, string writtenAfterHead, string remaining)
    {
        string response = statusLine + contentLength + unfinishedLine;
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream headers = new();
            TransferContext context = new()
            {
                Url = CurlUrl.Parse(ReuseUrl),
                Output = new MemoryStream(),
                HeaderOutput = headers,
                Events = new RecordingTransferEvents(),
                Http = new Curl.Protocol.Abstractions.HttpRequestOptions { Fail = HttpFailMode.Fail },
            };
            Diagnostics.Arrange("chunk size", chunkSize);
            Diagnostics.Arrange("scripted response", OneLine(response) + " (peer then closes), -f");

            TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(response, chunkSize), null, connectionNumber: 0)))
                .ExecuteAsync(context);

            WriteResult(result);
            string expectedMessage = $"transfer closed with {remaining} bytes remaining to read";
            string expectedHeaders = statusLine + contentLength + writtenAfterHead;
            Diagnostics.Assert("exit code", CurlExitCode.PartialFile, result.ExitCode);
            Diagnostics.Assert("error message", expectedMessage, result.ErrorMessage);
            Diagnostics.Diff("header output", OneLine(expectedHeaders), OneLine(Latin1(headers.ToArray())));
            Assert.AreEqual(CurlExitCode.PartialFile, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(expectedMessage, result.ErrorMessage, $"Chunk size {chunkSize}");
            Assert.AreEqual(OneLine(expectedHeaders), OneLine(Latin1(headers.ToArray())), $"Chunk size {chunkSize}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_PeerClosesInAHeaderLineWithNoContentLength_Succeeds()
    {
        const string response = "HTTP/1.1 200 OK\r\nX-Foo: 1\r\nX";
        MemoryStream headers = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(ReuseUrl),
            Output = new MemoryStream(),
            HeaderOutput = headers,
            Events = new RecordingTransferEvents(),
        };
        Diagnostics.Arrange("scripted response", OneLine(response) + " (peer then closes)");

        TransferResult result = await Handler(new QueueConnector(ConnectResult.Connected(Connection(response, 65536), null, connectionNumber: 0)))
            .ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual("HTTP/1.1 200 OK\\r\\nX-Foo: 1\\r\\n", OneLine(Latin1(headers.ToArray())));
    }
}
