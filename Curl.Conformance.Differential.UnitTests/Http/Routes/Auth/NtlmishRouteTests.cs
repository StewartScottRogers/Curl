using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/ntlmish</c>.</summary>
[TestClass]
public sealed class NtlmishRouteTests
{
    [TestMethod]
    public async Task RespondAsync_AnyRequest_AnswersNtlmAndBasicChallengesAndKeepsTheConnection()
    {
        const string Challenge =
            "HTTP/1.1 401 Unauthorized\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\n"
            + "WWW-Authenticate: NTLM\r\nWWW-Authenticate: Basic realm=\"r\"\r\nContent-Length: 1\r\n\r\nx";
        Assert.AreEqual(
            Challenge + Challenge,
            await DifferentialHttpResponderTests.RespondAsync(
                [new NtlmishRoute()],
                new DifferentialResponderState(),
                "GET /ntlmish HTTP/1.1\r\n\r\nGET /ntlmish HTTP/1.1\r\nAuthorization: Basic dTpw\r\n\r\n"));
    }
}
