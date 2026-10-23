using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/digest256</c>.</summary>
[TestClass]
public sealed class Digest256RouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    private const string Denied =
        $"HTTP/1.1 401 Unauthorized\r\n{Date}"
        + "WWW-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", algorithm=SHA-256\r\n"
        + "WWW-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", algorithm=MD5\r\n"
        + "Content-Length: 7\r\n\r\ndenied\n";

    [TestMethod]
    public async Task RespondAsync_RightSha256Answer_AnswersSha256OkAndKeepsTheConnection()
    {
        var ok = $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 10\r\n\r\nsha256 ok\n";
        Assert.AreEqual(
            ok + ok,
            await RespondAsync(Request("algorithm=SHA-256") + Request("algorithm=sha256")));
    }

    [TestMethod]
    public async Task RespondAsync_WrongAlgorithmAnswerOrNoAuthorization_AnswersBothChallenges()
    {
        Assert.AreEqual(
            Denied + Denied + Denied,
            await RespondAsync(
                Request("algorithm=MD5")
                + "GET /digest256 HTTP/1.1\r\nAuthorization: Digest algorithm=SHA-256, response=\"bad\"\r\n\r\n"
                + "GET /digest256 HTTP/1.1\r\n\r\n"));
    }

    private static string Request(string algorithm)
    {
        var answer = Sha256($"{Sha256("u:r:p")}:abc:00000001:cn:auth:{Sha256("GET:/digest256")}");
        return $"GET /digest256 HTTP/1.1\r\nAuthorization: Digest uri=\"/digest256\", {algorithm}, nc=00000001, cnonce=\"cn\", response=\"{answer}\"\r\n\r\n";
    }

    private static Task<string> RespondAsync(string requests) =>
        DifferentialHttpResponderTests.RespondAsync([new Digest256Route()], new DifferentialResponderState(), requests);

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
