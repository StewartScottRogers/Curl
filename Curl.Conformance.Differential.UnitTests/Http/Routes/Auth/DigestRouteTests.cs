using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/digest</c>.</summary>
[TestClass]
public sealed class DigestRouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    private const string Denied =
        $"HTTP/1.1 401 Unauthorized\r\n{Date}WWW-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\", opaque=\"o\", algorithm=MD5\r\n"
        + "Content-Length: 7\r\n\r\ndenied\n";

    [TestMethod]
    public async Task RespondAsync_RightMd5Answer_AnswersDigestOkAndKeepsTheConnection()
    {
        var answer = Md5($"{Md5("u:r:p")}:abc:00000001:cn:auth:{Md5("GET:/digest")}");
        var authorization = $"Authorization: Digest username=\"u\", uri=\"/digest\", nc=00000001, cnonce=\"cn\", response=\"{answer}\"\r\n";
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 10\r\n\r\ndigest ok\n" + Denied,
            await RespondAsync($"GET /digest HTTP/1.1\r\n{authorization}\r\nGET /digest HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task RespondAsync_WrongAnswerOrNotDigest_AnswersTheChallenge()
    {
        Assert.AreEqual(
            Denied + Denied,
            await RespondAsync(
                "GET /digest HTTP/1.1\r\nAuthorization: Digest response=\"bad\"\r\n\r\n"
                + "GET /digest HTTP/1.1\r\nAuthorization: Basic dTpw\r\n\r\n"));
    }

    private static Task<string> RespondAsync(string requests) =>
        DifferentialHttpResponderTests.RespondAsync([new DigestRoute()], new DifferentialResponderState(), requests);

    private static string Md5(string text) => Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(text)));
}
