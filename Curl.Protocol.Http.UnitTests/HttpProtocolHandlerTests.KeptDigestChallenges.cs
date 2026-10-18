using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins how the challenges a 401 of a Libcurl handle's previous transfer offered are answered on
/// the next transfer's first request, as curl 8.22.0 keeps its Digest nonce on the easy handle
/// (upstream test2024, BL-2747): given as <see cref="HttpRequestOptions.KeptDigestChallenges" />,
/// they are answered before any new challenge when Digest is the one scheme allowed.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string KeptChallenge = "Digest realm=\"testrealm\", nonce=\"1\"";

    [TestMethod]
    public async Task ExecuteAsync_KeptDigestChallengesUnderDigestAlone_AnswersThemOnTheFirstRequest()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        const string answer = "Digest username=\"u\", nonce=\"1\"";
        Diagnostics.Arrange("kept challenges", KeptChallenge);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator(answer)).ExecuteAsync(KeptChallengesContext([KeptChallenge, "Basic realm=\"testrealm\""], HttpAuthSchemes.Digest, credentials: true));

        WriteResult(result);
        string expected = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + answer + "\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n";
        Diagnostics.Diff("connection written", expected, connection.Written);
        Assert.AreEqual(expected, connection.Written);
    }

    [TestMethod]
    [DataRow(true, HttpAuthSchemes.Digest, false, DisplayName = "no credential")]
    [DataRow(true, HttpAuthSchemes.Basic, true, DisplayName = "Digest not allowed")]
    [DataRow(true, HttpAuthSchemes.Any, true, DisplayName = "Digest not alone")]
    [DataRow(false, HttpAuthSchemes.Digest, true, DisplayName = "no challenges kept")]
    public async Task ExecuteAsync_KeptDigestChallengesThatMayNotBeAnswered_SendsNoAuthorization(bool kept, HttpAuthSchemes allowed, bool credentials)
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        Diagnostics.Arrange("kept, allowed, credentials", $"{kept}, {allowed}, {credentials}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator("Digest x")).ExecuteAsync(KeptChallengesContext(kept ? [KeptChallenge] : [], allowed, credentials));

        WriteResult(result);
        Diagnostics.Diff("connection written", PlainRequest, connection.Written);
        Assert.AreEqual(PlainRequest, connection.Written);
    }

    private static TransferContext KeptChallengesContext(string[] kept, HttpAuthSchemes allowed, bool credentials) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = new MemoryStream(),
        Credentials = credentials ? new NetworkCredential("u", "p") : null,
        Http = new HttpRequestOptions { AuthSchemes = allowed, KeptDigestChallenges = kept },
    };
}
