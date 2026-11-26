using System.Net;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins that a Digest transfer failing with exit 94 reports the header count of the last 401 head (BL-5331).
/// </summary>
[TestClass]
public sealed class DigestFailureNumHeadersTests
{
    private const string Url = "http://127.0.0.1:18184/d256";

    private const string DigestHead =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r@x\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", algorithm=SHA-256, qop=\"auth\"\r\nContent-Length: 4\r\n\r\nnope";

    [TestMethod]
    public async Task ExecuteAsync_DigestFailsWithExit94_ReportsThe401AndItsTwoHeaders()
    {
        TransferResult result = await ExecuteAsync(DigestHead, new RefusingAuthenticator());

        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual(401, result.Report!.ResponseCode);
        Assert.AreEqual(2, result.Report.ResponseHeaders.Count);
    }


    [TestMethod]
    public async Task ExecuteAsync_NtlmFailsWithExit94_ReportsThe401AndItsTwoHeaders()
    {
        const string head = "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM AAAA\r\nContent-Length: 0\r\n\r\n";

        TransferResult result = await ExecuteAsync(head, new RefusingAuthenticator(), HttpAuthSchemes.Ntlm);

        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual(401, result.Report!.ResponseCode);
        Assert.AreEqual(2, result.Report.ResponseHeaders.Count);
    }

    [TestMethod]
    public async Task ExecuteAsync_ChallengeIsAnswered_ReportsTheFinalResponsesHeaders()
    {
        TurnTakingConnection connection = new(65536, DigestHead, "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nX-A: b\r\nX-B: c\r\n\r\nok");
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("user", "pass"),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Digest },
        };

        TransferResult result = await new HttpProtocolHandler(QueueConnector.For(connection), new AnsweringAuthenticator()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(3, result.Report!.ResponseHeaders.Count);
    }

    private sealed class AnsweringAuthenticator : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            challenges.Count == 0 ? null : "Digest answer";
    }

    private static async Task<TransferResult> ExecuteAsync(string response, IHttpAuthenticator authenticator, HttpAuthSchemes scheme = HttpAuthSchemes.Digest)
    {
        TurnTakingConnection connection = new(65536, response);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("user", "pass"),
            Http = new HttpRequestOptions { AuthSchemes = scheme },
        };
        return await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);
    }

    private sealed class RefusingAuthenticator : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            challenges.Count == 0
                ? null
                : throw new HttpAuthenticationFailedException(CurlExitCode.AuthError, "An authentication function returned an error");
    }
}
