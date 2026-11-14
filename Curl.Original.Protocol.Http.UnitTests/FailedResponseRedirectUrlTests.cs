using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5115 on <see cref="HttpProtocolHandler" />: with <c>-f</c> a 4xx that ends the
/// transfer leaves the redirect URL empty, as upstream curl does; without <c>-f</c>, and for
/// a 3xx, the absolute Location URL is still reported.
/// </summary>
[TestClass]
public sealed class FailedResponseRedirectUrlTests
{
    private const string Url = "http://127.0.0.1:20000/status/404";

    [TestMethod]
    public async Task ExecuteAsync_FailOn404WithLocation_LeavesRedirectUrlEmpty()
    {
        TransferResult result = await Execute("HTTP/1.1 404 Not Found\r\nLocation: /redir-target\r\nContent-Length: 0\r\n\r\n", HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.IsNull(result.Report?.RedirectUrl);
    }

    [TestMethod]
    public async Task ExecuteAsync_404WithLocationWithoutFail_ReportsAbsoluteLocation()
    {
        TransferResult result = await Execute("HTTP/1.1 404 Not Found\r\nLocation: /redir-target\r\nContent-Length: 0\r\n\r\n", HttpFailMode.None);

        Assert.AreEqual("http://127.0.0.1:20000/redir-target", result.Report?.RedirectUrl);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOn302WithLocation_StillReportsLocation()
    {
        TransferResult result = await Execute("HTTP/1.1 302 Found\r\nLocation: /redir-target\r\nContent-Length: 0\r\n\r\n", HttpFailMode.Fail);

        Assert.AreEqual("http://127.0.0.1:20000/redir-target", result.Report?.RedirectUrl);
    }

    private static ValueTask<TransferResult> Execute(string response, HttpFailMode fail) =>
        new HttpProtocolHandler(QueueConnector.For(new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536)), new SilentAuthenticator())
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse(Url),
                Output = new MemoryStream(),
                Events = new RecordingTransferEvents(),
                Http = new HttpRequestOptions { UserAgent = "curl/8.22.0", Fail = fail },
            });
}
