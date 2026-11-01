using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Third-party gap report issue CGR-214 (BL-3743): after <c>--fail-with-body</c> on a 404,
/// <c>%{errormsg}</c> is curl 8.22.0's generic text of exit 22, <c>HTTP response code said error</c>,
/// while the <c>curl: (22)</c> line keeps <c>The requested URL returned error: 404</c>; the body's
/// bytes reach the progress meter.
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string NotFoundWithBody = "HTTP/1.1 404 Not Found\r\nContent-Length: 9\r\n\r\nnot found";

    [TestMethod]
    public async Task ExecuteAsync_FailWithBodyOn404_GivesGenericTextForWriteOutErrorMessage()
    {
        Diagnostics.Arrange("scripted response", OneLine(NotFoundWithBody));

        TransferResult result = await Handler(QueueConnector.For(Connection(NotFoundWithBody, 65536, RootRequest)))
            .ExecuteAsync(FailContext("http://example.com/", HttpFailMode.FailWithBody, new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("write-out error message", "HTTP response code said error", result.WriteOutErrorMessage);
        Assert.AreEqual("HTTP response code said error", result.WriteOutErrorMessage);
        Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOn404_KeepsTheRequestedUrlTextForWriteOut()
    {
        Diagnostics.Arrange("scripted response", OneLine(NotFoundWithBody));

        TransferResult result = await Handler(QueueConnector.For(Connection(NotFoundWithBody, 65536, RootRequest)))
            .ExecuteAsync(FailContext("http://example.com/", HttpFailMode.Fail, new MemoryStream()));

        WriteResult(result);
        Assert.IsNull(result.WriteOutErrorMessage);
        Assert.AreEqual("The requested URL returned error: 404", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailWithBodyOn404_ReportsTheBodyBytesToTheMeter()
    {
        RecordingTransferProgress progress = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://example.com/"),
            Output = new MemoryStream(),
            Progress = progress,
            Http = new Curl.Protocol.Abstractions.HttpRequestOptions { Fail = HttpFailMode.FailWithBody },
        };
        Diagnostics.Arrange("scripted response", OneLine(NotFoundWithBody));

        await Handler(QueueConnector.For(Connection(NotFoundWithBody, 65536, RootRequest))).ExecuteAsync(context);

        Diagnostics.Act("reports", string.Join(" | ", progress.Reports));
        Assert.IsTrue(progress.Downloads.Contains("down 9/9"), string.Join(" | ", progress.Reports));
    }
}
