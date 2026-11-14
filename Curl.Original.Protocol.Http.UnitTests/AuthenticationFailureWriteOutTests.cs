using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins that an authentication failure (exit 94) after a 401 was received still reports that 401 and
/// its stored header count, as upstream curl's <c>%{http_code}</c>, <c>%{response_code}</c> and
/// <c>%{num_headers}</c> do (BL-5118), and that a failure before any response reports none.
/// </summary>
[TestClass]
public sealed class AuthenticationFailureWriteOutTests
{
    private const string Url = "http://127.0.0.1:18183/a";
    private const string Message = "An authentication function returned an error";

    private const string DigestHead =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: Digest realm=\"r@x\", nonce=\"abc\", algorithm=SHA-256, qop=\"auth\"\r\nContent-Type: text/plain\r\nContent-Length: 5\r\n\r\nnope\n";

    private const string NtlmHead =
        "HTTP/1.1 401 Unauthorized\r\nWWW-Authenticate: NTLM AAAA\r\nContent-Length: 0\r\n\r\n";

    [TestMethod]
    public async Task ExecuteAsync_DigestChallengeCannotBeAnswered_ReportsThe401AndItsFourHeaders()
    {
        TransferResult result = await ExecuteAsync(DigestHead, HttpAuthSchemes.Digest, new RefusingAuthenticator(refusesFirstRequest: false));

        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual(401, result.Report!.ResponseCode);
        Assert.AreEqual(4, result.Report.ResponseHeadersStored);
    }

    [TestMethod]
    public async Task ExecuteAsync_NtlmType2IsInvalid_ReportsThe401()
    {
        TransferResult result = await ExecuteAsync(NtlmHead, HttpAuthSchemes.Ntlm, new RefusingAuthenticator(refusesFirstRequest: false));

        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual(401, result.Report!.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_AuthenticatorRefusesBeforeAnyResponse_ReportsNoResponseCode()
    {
        TransferResult result = await ExecuteAsync(DigestHead, HttpAuthSchemes.Digest, new RefusingAuthenticator(refusesFirstRequest: true));

        Assert.AreEqual(CurlExitCode.AuthError, result.ExitCode);
        Assert.AreEqual(0, result.Report!.ResponseCode);
        Assert.AreEqual(0, result.Report.ResponseHeadersStored);
    }

    private static async Task<TransferResult> ExecuteAsync(string response, HttpAuthSchemes scheme, IHttpAuthenticator authenticator)
    {
        TurnTakingConnection connection = new(65536, response);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(Url),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential("u", "p"),
            Http = new HttpRequestOptions { AuthSchemes = scheme },
        };
        return await new HttpProtocolHandler(QueueConnector.For(connection), authenticator).ExecuteAsync(context);
    }

    /// <summary>An authenticator that refuses with exit 94, on the first request or on a challenge.</summary>
    private sealed class RefusingAuthenticator(bool refusesFirstRequest) : IHttpAuthenticator
    {
        public string? CreateAuthorization(HttpAuthRequest request, IReadOnlyList<string> challenges) =>
            challenges.Count == 0 && !refusesFirstRequest
                ? null
                : throw new HttpAuthenticationFailedException(CurlExitCode.AuthError, Message);
    }
}
