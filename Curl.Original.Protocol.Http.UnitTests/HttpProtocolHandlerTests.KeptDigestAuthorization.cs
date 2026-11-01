using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <content>
/// Pins how a Digest answer travels between the hops of a redirect, as curl 8.22.0 keeps its
/// Digest nonce across the redirects it follows and counts it on (upstream test1286, BL-2252):
/// the report gives the Digest value sent, and the next hop, given it as
/// <see cref="HttpRequestOptions.KeptDigestAuthorization" />, sends it again counted on for its
/// own request before any challenge.
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    private const string DigestChallengeHead = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r\", nonce=\"n\", qop=\"auth\"\r\nContent-Length: 4\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_KeptDigestAuthorization_SendsItCountedOnBeforeAnyChallengeAndReportsIt()
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        const string sent = "Digest nc=00000001 again for GET /a";
        Diagnostics.Arrange("kept", "Digest nc=00000001");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator(null)).ExecuteAsync(KeptDigestContext("Digest nc=00000001", HttpAuthSchemes.Digest, credentials: true));

        WriteResult(result);
        string expected = "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18183\r\nAuthorization: " + sent + "\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n";
        Diagnostics.Diff("connection written", expected, connection.Written);
        Assert.AreEqual(expected, connection.Written);
        Diagnostics.Assert("Digest sent", sent, result.Report!.DigestAuthorizationSent);
        Assert.AreEqual(sent, result.Report.DigestAuthorizationSent);
    }

    [TestMethod]
    [DataRow(HttpAuthSchemes.Digest, false, DisplayName = "no credential")]
    [DataRow(HttpAuthSchemes.Basic, true, DisplayName = "Digest not allowed")]
    public async Task ExecuteAsync_KeptDigestAuthorizationThatMayNotBeSent_SendsNoAuthorization(HttpAuthSchemes allowed, bool credentials)
    {
        TurnTakingConnection connection = new(65536, OkHead + "ok");
        Diagnostics.Arrange("allowed, credentials", $"{allowed}, {credentials}");

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator(null)).ExecuteAsync(KeptDigestContext("Digest nc=00000001", allowed, credentials));

        WriteResult(result);
        Diagnostics.Diff("connection written", PlainRequest, connection.Written);
        Assert.AreEqual(PlainRequest, connection.Written);
        Assert.IsNull(result.Report!.DigestAuthorizationSent);
    }

    [TestMethod]
    [DataRow("Digest username=\"u\"", "Digest username=\"u\"", DisplayName = "Digest")]
    [DataRow("Basic dTpw", null, DisplayName = "Basic")]
    public async Task ExecuteAsync_ChallengeAnswered_ReportsTheDigestValueSentOnly(string answer, string? expected)
    {
        TurnTakingConnection connection = new(65536, DigestChallengeHead + "nope", OkHead + "ok");
        Diagnostics.Arrange("answer", answer);

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new RepeatingAuthenticator(answer)).ExecuteAsync(KeptDigestContext(null, HttpAuthSchemes.Any, credentials: true));

        WriteResult(result);
        Diagnostics.Assert("Digest sent", expected, result.Report!.DigestAuthorizationSent);
        Assert.AreEqual(expected, result.Report.DigestAuthorizationSent);
    }

    private static TransferContext KeptDigestContext(string? kept, HttpAuthSchemes allowed, bool credentials) => new()
    {
        Url = CurlUrl.Parse(AuthUrl),
        Output = new MemoryStream(),
        Credentials = credentials ? new NetworkCredential("u", "p") : null,
        Http = new HttpRequestOptions { AuthSchemes = allowed, KeptDigestAuthorization = kept },
    };
}
