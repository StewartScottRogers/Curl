using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/authall</c>.</summary>
[TestClass]
public sealed class AuthAllRouteTests
{
    [TestMethod]
    public async Task RespondAsync_AnyRequest_AnswersFourChallengesInOrderAndKeepsTheConnection()
    {
        const string Challenge =
            "HTTP/1.1 401 Unauthorized\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\n"
            + "WWW-Authenticate: Negotiate\r\nWWW-Authenticate: NTLM\r\n"
            + "WWW-Authenticate: Digest realm=\"r\", nonce=\"abc\", qop=\"auth\"\r\nWWW-Authenticate: Basic realm=\"r\"\r\n"
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(
            Challenge + Challenge,
            await DifferentialHttpResponderTests.RespondAsync(
                [new AuthAllRoute()],
                new DifferentialResponderState(),
                "GET /authall HTTP/1.1\r\n\r\nGET /authall HTTP/1.1\r\n\r\n"));
    }
}
