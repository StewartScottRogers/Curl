using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins how the scheme a server picked travels between the hops of a redirect, as libcurl
/// 8.21.0 keeps its picked scheme across the redirects it follows (upstream test1088,
/// BL-1819): <c>--anyauth</c> answers a Basic challenge, the report says Basic, and the next
/// hop, given Basic as <see cref="HttpRequestOptions.AuthSchemePicked" />, sends Basic before
/// any challenge.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string BasicChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Basic realm=\"r\"\r\nContent-Length: 4\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_AnyAuthAnswersABasicChallenge_ReportsBasicPicked()
    {
        TurnTakingConnection connection = new(65536, BasicChallengeHead + "nope", OkHead + "ok");
        ScriptedAuthenticator authenticator = new(null, "Basic dTpw");
        Diagnostics.Arrange("scripted responses", "401 Basic, 200; --anyauth");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(PickedSchemeContext(HttpAuthSchemes.None));

        WriteResult(result);
        Diagnostics.Diff("connection written", PlainRequest + BasicRequest, connection.Written);
        Assert.AreEqual(PlainRequest + BasicRequest, connection.Written);
        Diagnostics.Assert("scheme picked", HttpAuthSchemes.Basic, result.Report!.AuthSchemePicked);
        Assert.AreEqual(HttpAuthSchemes.Basic, result.Report.AuthSchemePicked);
    }

    [TestMethod]
    public async Task ExecuteAsync_AnyAuthWithBasicPickedByAnEarlierHop_SendsBasicBeforeAnyChallengeAndReportsItPicked()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        ScriptedAuthenticator authenticator = new("Basic dTpw", null);
        Diagnostics.Arrange("earlier hop picked", HttpAuthSchemes.Basic);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(PickedSchemeContext(HttpAuthSchemes.Basic));

        WriteResult(result);
        Diagnostics.Diff("connection written", BasicRequest, connection.Written);
        Assert.AreEqual(BasicRequest, connection.Written);
        Diagnostics.Assert("schemes allowed", HttpAuthSchemes.Basic, authenticator.Calls.Single().Request.AllowedSchemes);
        Assert.AreEqual(HttpAuthSchemes.Basic, authenticator.Calls.Single().Request.AllowedSchemes);
        Assert.AreEqual(HttpAuthSchemes.Basic, result.Report!.AuthSchemePicked);
    }

    [TestMethod]
    public async Task ExecuteAsync_BasicSentBeforeAnyChallenge_ReportsBasicPicked()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        ScriptedAuthenticator authenticator = new("Basic dTpw", null);
        Diagnostics.Arrange("authenticator answers first request", "Basic, no earlier hop");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(PickedSchemeContext(HttpAuthSchemes.None));

        WriteResult(result);
        Assert.AreEqual(BasicRequest, connection.Written);
        Diagnostics.Assert("scheme picked", HttpAuthSchemes.Basic, result.Report!.AuthSchemePicked);
        Assert.AreEqual(HttpAuthSchemes.Basic, result.Report.AuthSchemePicked);
    }

    [TestMethod]
    public async Task ExecuteAsync_EarlierHopPickedASchemeNotAllowed_AllowsEverySchemeGiven()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(null, null);
        Diagnostics.Arrange("earlier hop picked, allowed", "Bearer, --anyauth");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(PickedSchemeContext(HttpAuthSchemes.Bearer));

        WriteResult(result);
        Assert.AreEqual(PlainRequest, connection.Written);
        Diagnostics.Assert("schemes allowed", HttpAuthSchemes.Any, authenticator.Calls.Single().Request.AllowedSchemes);
        Assert.AreEqual(HttpAuthSchemes.Any, authenticator.Calls.Single().Request.AllowedSchemes);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Ntlm)]
    [DataRow(HttpAuthSchemes.Negotiate)]
    public async Task ExecuteAsync_EarlierHopPickedAConnectionScheme_ProbesAgainWithEverySchemeAllowed(HttpAuthSchemes picked)
    {
        // Measured: curl 8.21.0 --anyauth -L against NTLM 401s sends GET /one, type 1, ...,
        // then GET /two with no Authorization (Record-CurlExchange.ps1; upstream test90).
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        ScriptedAuthenticator authenticator = new(null, null);
        Diagnostics.Arrange("earlier hop picked", picked);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(PickedSchemeContext(picked));

        WriteResult(result);
        Assert.AreEqual(PlainRequest, connection.Written);
        Diagnostics.Assert("schemes allowed", HttpAuthSchemes.Any, authenticator.Calls.Single().Request.AllowedSchemes);
        Assert.AreEqual(HttpAuthSchemes.Any, authenticator.Calls.Single().Request.AllowedSchemes);
    }

    [TestMethod]
    [DataRow("Digest x", HttpAuthSchemes.Digest)]
    [DataRow("NTLM x", HttpAuthSchemes.Ntlm)]
    [DataRow("Negotiate x", HttpAuthSchemes.Negotiate)]
    [DataRow("Other x", HttpAuthSchemes.None)]
    public async Task ExecuteAsync_ChallengeAnsweredWithAScheme_ReportsThatSchemePicked(string answer, HttpAuthSchemes expected)
    {
        TurnTakingConnection connection = new(65536, BasicChallengeHead + "nope", OkHead + "ok");
        ScriptedAuthenticator authenticator = new(null, answer);
        Diagnostics.Arrange("answer", answer);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(PickedSchemeContext(HttpAuthSchemes.None));

        WriteResult(result);
        Diagnostics.Assert("scheme picked", expected, result.Report!.AuthSchemePicked);
        Assert.AreEqual(expected, result.Report.AuthSchemePicked);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProxyDigestChallengeAnswered_ReportsDigestPickedForTheProxyOnly()
    {
        TurnTakingConnection connection = new(65536, KeepAliveProxyDigestChallengeHead + "PPP", ProxyOkHead + "ok");
        Diagnostics.Arrange("url, proxy, auth", $"{ProxyAuthUrl}, {ChallengingProxy.Host}:{ChallengingProxy.Port}, Digest");

        TransferResult result = await ProxyChallengeHandler(QueueConnector.For(connection), HttpAuthSchemes.Digest, "e395f7bf9cdabe6947113bd005a4ce2a")
            .ExecuteAsync(new TransferContext
            {
                Url = CurlUrl.Parse(ProxyAuthUrl),
                Output = new MemoryStream(),
                Http = new HttpRequestOptions { ForwardProxy = ChallengingProxy },
            });

        WriteResult(result);
        Diagnostics.Assert("proxy scheme picked", HttpAuthSchemes.Digest, result.Report!.ProxyAuthSchemePicked);
        Assert.AreEqual(HttpAuthSchemes.Digest, result.Report.ProxyAuthSchemePicked);
        Assert.AreEqual(HttpAuthSchemes.None, result.Report.AuthSchemePicked);
    }

    [TestMethod]
    public async Task ExecuteAsync_OriginChallengeAnsweredWithoutAProxy_ReportsNoProxySchemePicked()
    {
        TurnTakingConnection connection = new(65536, BasicChallengeHead + "nope", OkHead + "ok");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new ScriptedAuthenticator(null, "Basic dTpw")).ExecuteAsync(PickedSchemeContext(HttpAuthSchemes.None));

        WriteResult(result);
        Assert.AreEqual(HttpAuthSchemes.None, result.Report!.ProxyAuthSchemePicked);
    }

    private static TransferContext PickedSchemeContext(HttpAuthSchemes picked) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = new MemoryStream(),
        Credentials = new NetworkCredential("u", "p"),
        Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Any, AuthSchemePicked = picked },
    };
}
