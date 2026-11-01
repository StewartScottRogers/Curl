using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins that the request answering a <c>401</c> Digest challenge sends the query's <c>%xx</c>
/// escapes in upper case in its request line and Digest <c>uri</c>, while the first request keeps
/// them as typed: curl 8.22.0 sent <c>GET /a%2Fb?q=%2f</c>, then <c>GET /a%2Fb?q=%2F</c> with
/// <c>uri="/a%2Fb?q=%2F"</c> (measured, BL-3856).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_DigestRetry_SendsTheQueryEscapesInUpperCase()
    {
        TurnTakingConnection connection = new(65536, KeepAliveDigestChallenge, OkHead + "ok");
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18183/a%2fb?q=%2f"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest },
        };
        Diagnostics.Arrange("url", "http://127.0.0.1:18183/a%2fb?q=%2f");

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), new ScriptedTokenSource()).ExecuteAsync(context);

        WriteResult(result);
        string[] requestLines = [.. connection.Written.Split("\r\n").Where(line => line.StartsWith("GET ", StringComparison.Ordinal))];
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "GET /a%2Fb?q=%2f HTTP/1.1", "GET /a%2Fb?q=%2F HTTP/1.1" }, requestLines);
        StringAssert.Contains(connection.Written, "uri=\"/a%2Fb?q=%2F\"");
    }

    [TestMethod]
    public async Task ExecuteAsync_DigestRetryWithRequestTarget_KeepsTheTargetAsGiven()
    {
        TurnTakingConnection connection = new(65536, KeepAliveDigestChallenge, OkHead + "ok");
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:18183/x"),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest, RequestTarget = "/a%2fb?q=%2f" },
        };
        Diagnostics.Arrange("request target", "/a%2fb?q=%2f");

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), new ScriptedTokenSource()).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        StringAssert.Contains(connection.Written, "uri=\"/a%2fb?q=%2f\"");
    }
}
