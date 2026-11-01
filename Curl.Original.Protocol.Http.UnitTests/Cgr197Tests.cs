using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// CGR-197: a request refused as too large is never sent, so <c>%{size_request}</c> is 0.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_RequestTargetOfOnePointOneMegabytes_ReportsZeroRequestSize()
    {
        ScriptedConnection connection = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok", 65536);
        string url = "http://127.0.0.1/?" + new string('a', 1_100_000);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(url), Output = new MemoryStream(), Http = new HttpRequestOptions() });

        WriteResult(result);
        Assert.AreEqual(CurlExitCode.TooLarge, result.ExitCode);
        Assert.AreEqual(0L, result.Report?.RequestSize);
        Assert.IsEmpty(connection.Written);
    }
}
