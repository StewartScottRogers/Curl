using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5039: an HTTP/1.1 chunked response that ends with trailers reports their count in
/// <see cref="TransferReport.TrailerCount" />, which <c>%{num_headers}</c> prints (curl 8.22.0 and 8.18.0 print 1
/// for two head headers and one trailer); without trailers the count stays 0 and the head's headers are counted.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [DataRow("0\r\nX-T: 1\r\n\r\n", 1)]
    [DataRow("0\r\nX-T: 1\r\nX-U: 2\r\n\r\n", 2)]
    [DataRow("0\r\n\r\n", 0)]
    public async Task ExecuteAsync_ChunkedResponse_ReportsTheTrailerCountAndKeepsTheHeadHeaders(string lastChunk, int trailers)
    {
        string response = "HTTP/1.1 200 OK\r\nX-A: 1\r\nX-B: 2\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n" + lastChunk;
        Diagnostics.Arrange("last chunk", lastChunk);

        TransferResult result = await Handler(QueueConnector.For(Connection(response, 65536))).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("http://127.0.0.1:41000/"),
            Output = new MemoryStream(),
            HeaderOutput = new MemoryStream(),
            Http = new HttpRequestOptions { UserAgent = "curl/8.22.0" },
        });

        Diagnostics.Act("exit code, trailer count", $"{result.ExitCode}, {result.Report?.TrailerCount}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(trailers, result.Report!.TrailerCount);
        Assert.AreEqual(3, result.Report.ResponseHeaders.Count);
    }
}
