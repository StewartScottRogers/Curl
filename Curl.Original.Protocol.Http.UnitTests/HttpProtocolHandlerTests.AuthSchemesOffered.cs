using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins what the report says about an authentication retry (BL-3256): every scheme a 401 or 407
/// offered stays on the report after the retry ends 200, as libcurl ORs each challenge into
/// <c>httpauthavail</c> and <c>proxyauthavail</c>, and the retry counts as one redirect. Measured
/// with <c>Record-CurlExchange.ps1 -Connections 2</c>: curl 8.22.0 (and 8.21.0)
/// <c>--digest -u a:b -w "%{num_redirects} %{http_code}"</c> against a 401 offering Basic and
/// Digest and then a 200 prints <c>1 200</c>, and with <c>-L --max-redirs 0</c> prints
/// <c>0 401</c> and exits 47.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string BasicAndDigestChallengeHead =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"r\"\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"abc\"\r\nContent-Length: 4\r\n\r\n";

    private const string ProxyBasicAndDigestChallengeHead =
        "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"r\"\r\nProxy-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nContent-Length: 3\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_401OfferingBasicAndDigestRetriedTo200_ReportsBothOfferedAndOneRedirect()
    {
        TurnTakingConnection connection = new(65536, BasicAndDigestChallengeHead + "nope", OkHead + "ok");
        Diagnostics.Arrange("scripted responses", "401 Basic + Digest, 200; --digest");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Digest x"))
            .ExecuteAsync(OfferedSchemesContext(new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest }));

        WriteResult(result);
        Diagnostics.Assert("schemes offered", HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, result.Report!.AuthSchemesOffered);
        Assert.AreEqual(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, result.Report.AuthSchemesOffered);
        Assert.AreEqual(HttpAuthSchemes.None, result.Report.ProxyAuthSchemesOffered);
        Assert.AreEqual(200, result.Report.ResponseCode);
        Diagnostics.Assert("redirect count", 1, result.Report.RedirectCount);
        Assert.AreEqual(1, result.Report.RedirectCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_407OfferingBasicAndDigestRetriedTo200_ReportsBothOfferedForTheProxy()
    {
        TurnTakingConnection connection = new(65536, ProxyBasicAndDigestChallengeHead + "PPP", ProxyOkHead + "ok");
        Diagnostics.Arrange("scripted responses", "407 Basic + Digest, 200; --proxy-digest");

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Digest, "e395f7bf9cdabe6947113bd005a4ce2a")
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse(ProxyAuthUrl),
                Output = new MemoryStream(),
                Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy },
            });

        WriteResult(result);
        Diagnostics.Assert("proxy schemes offered", HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, result.Report!.ProxyAuthSchemesOffered);
        Assert.AreEqual(HttpAuthSchemes.Basic | HttpAuthSchemes.Digest, result.Report.ProxyAuthSchemesOffered);
        Assert.AreEqual(HttpAuthSchemes.None, result.Report.AuthSchemesOffered);
        Assert.AreEqual(1, result.Report.RedirectCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthRetryWithMaxRedirsZero_FailsWithTooManyRedirects()
    {
        TurnTakingConnection connection = new(65536, BasicAndDigestChallengeHead + "nope", OkHead + "ok");
        Diagnostics.Arrange("max redirects", 0);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Digest x"))
            .ExecuteAsync(OfferedSchemesContext(new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest, MaxRedirects = 0 }));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.TooManyRedirects, result.ExitCode);
        Assert.AreEqual(CurlExitCode.TooManyRedirects, result.ExitCode);
    }

    private static TransferContext OfferedSchemesContext(HttpRequestOptions http) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = new MemoryStream(),
        Credentials = new NetworkCredential("a", "b"),
        Http = http,
    };
}
