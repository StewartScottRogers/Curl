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

    /// <summary>
    /// Upstream case 2027 (BL-2745): after a perform whose server picked Digest, a 401 offering Digest to the
    /// kept challenges' answer is answered once more with that answer, as libcurl ignores the 401's Digest
    /// header as a duplicate; the next 401 ends the transfer.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_KeptDigestAnswerRefusedAfterAPerformThatPickedDigest_SendsTheSameAnswerOnceMore()
    {
        const string refused = "HTTP/1.1 401 Wrong\r\nWWW-Authenticate: Digest realm=\"testrealm\", nonce=\"7\"\r\nContent-Length: 0\r\n\r\n";
        TurnTakingConnection connection = new(65536, refused, refused);
        const string answer = "Digest username=\"u\", nonce=\"5\"";
        Diagnostics.Arrange("kept challenges", KeptChallenge);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator(answer)).ExecuteAsync(KeptChallengesContext([KeptChallenge], HttpAuthSchemes.Digest, credentials: true, picked: HttpAuthSchemes.Digest));

        WriteResult(result);
        string expected = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + answer + "\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n"
            + "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + answer + " again for GET /a\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n";
        Diagnostics.Diff("connection written", expected, connection.Written);
        Assert.AreEqual(expected, connection.Written);
    }

    /// <summary>
    /// Upstream case 2024 (BL-2745): after a perform that picked another scheme, or for a 401 offering no Digest,
    /// the kept challenges' refused answer ends the transfer.
    /// </summary>
    [TestMethod]
    [DataRow(HttpAuthSchemes.Basic, "Digest realm=\"r\", nonce=\"7\"", DisplayName = "previous perform picked Basic")]
    [DataRow(HttpAuthSchemes.None, "Digest realm=\"r\", nonce=\"7\"", DisplayName = "previous perform picked none")]
    [DataRow(HttpAuthSchemes.Digest, "Basic realm=\"r\"", DisplayName = "401 offers no Digest")]
    public async Task ExecuteAsync_KeptDigestAnswerRefusedOtherwise_SendsItOnlyOnce(HttpAuthSchemes picked, string challenge)
    {
        string refused = "HTTP/1.1 401 Wrong\r\nWWW-Authenticate: " + challenge + "\r\nContent-Length: 0\r\n\r\n";
        TurnTakingConnection connection = new(65536, refused, refused);
        const string answer = "Digest username=\"u\", nonce=\"5\"";
        Diagnostics.Arrange("picked, challenge", $"{picked}, {challenge}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator(answer)).ExecuteAsync(KeptChallengesContext([KeptChallenge], HttpAuthSchemes.Digest, credentials: true, picked));

        WriteResult(result);
        string expected = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + answer + "\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n";
        Diagnostics.Diff("connection written", expected, connection.Written);
        Assert.AreEqual(expected, connection.Written);
    }

    /// <summary>
    /// Upstream case 2030 (BL-2820): a transfer whose last 401 offers Digest without <c>stale=true</c> while it holds a
    /// kept Digest nonce ends with Digest still marked available; a stale nonce, no kept nonce (the first Digest
    /// challenge), a 401 offering no Digest or a final response that is no 401 leaves it unmarked.
    /// </summary>
    [TestMethod]
    [DataRow(true, "401 Wrong", "Digest realm=\"r\", nonce=\"7\"", true, DisplayName = "kept nonce, Digest offered again")]
    [DataRow(true, "401 Wrong", "Digest realm=\"r\", nonce=\"7\", stale=true", false, DisplayName = "stale")]
    [DataRow(false, "401 Wrong", "Digest realm=\"r\", nonce=\"7\"", false, DisplayName = "first Digest challenge only")]
    [DataRow(true, "401 Wrong", "Basic realm=\"r\"", false, DisplayName = "401 offers no Digest")]
    [DataRow(true, "200 OK", "Digest realm=\"r\", nonce=\"7\"", false, DisplayName = "no 401")]
    public async Task ExecuteAsync_LastResponseAfterKeptDigestNonce_ReportsWhetherDigestStaysAvailable(bool kept, string status, string challenge, bool expected)
    {
        TurnTakingConnection connection = new(65536, "HTTP/1.1 " + status + "\r\nWWW-Authenticate: " + challenge + "\r\nContent-Length: 0\r\n\r\n");
        Diagnostics.Arrange("kept, status, challenge", $"{kept}, {status}, {challenge}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator(null)).ExecuteAsync(KeptChallengesContext(kept ? [KeptChallenge] : [], HttpAuthSchemes.Digest, credentials: true));

        WriteResult(result);
        Assert.AreEqual(expected, result.Report!.DigestStaysAvailable);
    }

    /// <summary>
    /// Upstream case 2030 (BL-2820): a 401 offering Digest after an earlier 401 of the same transfer gave a Digest nonce
    /// ends the transfer with Digest still marked available.
    /// </summary>
    [TestMethod]
    public async Task ExecuteAsync_SecondDigest401OfOneTransfer_ReportsDigestStaysAvailable()
    {
        const string refused = "HTTP/1.1 401 Wrong\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"7\"\r\nContent-Length: 0\r\n\r\n";
        TurnTakingConnection connection = new(65536, refused, refused);
        Diagnostics.Arrange("responses", refused + refused);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator("Digest x")).ExecuteAsync(KeptChallengesContext([], HttpAuthSchemes.Digest, credentials: true));

        WriteResult(result);
        Assert.IsTrue(result.Report!.DigestStaysAvailable);
    }

    private static TransferContext KeptChallengesContext(string[] kept, HttpAuthSchemes allowed, bool credentials, HttpAuthSchemes picked = HttpAuthSchemes.None) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = new MemoryStream(),
        Credentials = credentials ? new NetworkCredential("u", "p") : null,
        Http = new HttpRequestOptions { AuthSchemes = allowed, KeptDigestChallenges = kept, AuthSchemePicked = picked },
    };
}
