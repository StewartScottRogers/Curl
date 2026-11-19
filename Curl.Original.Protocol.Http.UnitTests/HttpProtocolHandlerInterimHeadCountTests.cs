using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// What <c>-w</c> reports when 1xx heads come and the peer closes before a final head (BL-5401):
/// <c>num_headers</c> counts only the last head's lines, as curl does (outside review STD-109).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_100Then103ThenClose_ReportsOneHeaderAndSize59()
    {
        await AssertInterimHeadCountAsync("HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 103 Early\r\nLink: </a>\r\n\r\n", 103, 59, 1);
    }

    [TestMethod]
    public async Task ExecuteAsync_Lone100ThenClose_ReportsNoHeaders()
    {
        await AssertInterimHeadCountAsync("HTTP/1.1 100 Continue\r\n\r\n", 100, 25, 0);
    }

    [TestMethod]
    public async Task ExecuteAsync_Lone103ThenClose_ReportsOneHeader()
    {
        await AssertInterimHeadCountAsync("HTTP/1.1 103 Early\r\nLink: </a>\r\n\r\n", 103, 34, 1);
    }

    [TestMethod]
    public async Task ExecuteAsync_Three102HeadsThenClose_ReportsTheMiddleHeadsHeader()
    {
        await AssertInterimHeadCountAsync("HTTP/1.1 102 Processing\r\n\r\nHTTP/1.1 102 Processing\r\nA: b\r\n\r\nHTTP/1.1 102 Processing\r\n\r\n", 102, 87, 1);
    }

    private async Task AssertInterimHeadCountAsync(string response, int responseCode, int headerSize, int headerCount)
    {
        foreach (int chunkSize in ChunkSizes)
        {
            MemoryStream output = new();
            TransferResult result = await Handler(QueueConnector.For(Connection(response, chunkSize)))
                .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = output, HeaderOutput = output });

            Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(responseCode, result.Report!.ResponseCode, $"Chunk size {chunkSize}");
            Assert.AreEqual(headerSize, result.Report!.HeaderSize, $"Chunk size {chunkSize}");
            Assert.AreEqual(headerCount, result.Report!.ResponseHeaders.Count + result.Report!.InformationalHeaderCount, $"Chunk size {chunkSize}");
            Assert.AreEqual(0, result.Report!.ResponseHeaders.Count, $"Chunk size {chunkSize}: a 1xx head's fields are not response headers");
        }
    }
}
