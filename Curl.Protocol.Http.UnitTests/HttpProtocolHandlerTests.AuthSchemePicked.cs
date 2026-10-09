using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

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

    private static TransferContext PickedSchemeContext(HttpAuthSchemes picked) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = new MemoryStream(),
        Credentials = new NetworkCredential("u", "p"),
        Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Any, AuthSchemePicked = picked },
    };
}
