using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What <c>-w</c> reports when the peer sends only a 1xx head and closes (BL-5336): the head's
/// code and size, with the transfer still failing with exit 52, as curl does (outside review STD-086).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_Lone100ThenClose_ReportsCode100AndHeaderSize25()
    {
        await AssertLoneInterimReportAsync("HTTP/1.1 100 Continue\r\n\r\n", 100, 25);
    }

    [TestMethod]
    public async Task ExecuteAsync_Lone103ThenClose_ReportsCode103AndHeaderSize34()
    {
        await AssertLoneInterimReportAsync("HTTP/1.1 103 Early\r\nLink: </a>\r\n\r\n", 103, 34);
    }

    [TestMethod]
    public async Task ExecuteAsync_CloseWithNoBytes_ReportsCode0AndHeaderSize0()
    {
        await AssertLoneInterimReportAsync(string.Empty, 0, 0);
    }

    private async Task AssertLoneInterimReportAsync(string response, int responseCode, int headerSize)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = output, HeaderOutput = output });

            Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(responseCode, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(headerSize, result.Report!.HeaderSize, $"Chunk size {chunkSize}");
        }
    }
}
