using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
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

    // BL-6021: curl 8.21.0 --raw neither stores nor counts chunked trailers, so %{num_headers} is the
    // head's count and more than the 5000 header limit of them pass.
    [TestMethod]
    [DataRow(1)]
    [DataRow(5001)]
    public async Task ExecuteAsync_RawChunkedResponse_CountsOnlyTheHeadHeaders(int trailers)
    {
        string lastChunk = "0\r\n" + string.Concat(Enumerable.Repeat("X-T: 1\r\n", trailers)) + "\r\n";
        const string head = "HTTP/1.1 200 OK\r\nX-A: 1\r\nX-B: 2\r\nTransfer-Encoding: chunked\r\n\r\n";
        string body = "2\r\nhi\r\n" + lastChunk;
        MemoryStream output = new();
        MemoryStream headerOutput = new();
        Diagnostics.Arrange("trailers", trailers);

        TransferResult result = await Handler(QueueConnector.For(Connection(head + body, 65536))).ExecuteAsync(new TransferContext
        {
            Url = CurlUrl.Parse("http://127.0.0.1:41000/"),
            Output = output,
            HeaderOutput = headerOutput,
            Http = new HttpRequestOptions { UserAgent = "curl/8.22.0", PassesContentCoding = true, PassesTransferCoding = true },
        });

        Diagnostics.Act("exit code, trailer count", $"{result.ExitCode}, {result.Report?.TrailerCount}");
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(0, result.Report!.TrailerCount);
        Assert.AreEqual(3, result.Report.ResponseHeaders.Count);
        Assert.AreEqual(3, result.Report.ResponseHeadersStored);
        Assert.AreEqual(head, System.Text.Encoding.Latin1.GetString(headerOutput.ToArray()));
        Assert.AreEqual(body, System.Text.Encoding.Latin1.GetString(output.ToArray()));
    }
}
