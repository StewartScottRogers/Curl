using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/need-proxy-auth-too</c>.</summary>
[TestClass]
public sealed class NeedProxyAuthTooRouteTests
{
    [TestMethod]
    public async Task RespondAsync_AnyRequest_AnswersProxyChallengeAndKeepsTheConnection()
    {
        const string Challenge =
            "HTTP/1.1 407 Proxy Authentication Required\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\n"
            + "Proxy-Authenticate: Basic realm=\"p\"\r\nContent-Length: 1\r\n\r\nx";
        Assert.AreEqual(
            Challenge + Challenge,
            await DifferentialHttpResponderTests.RespondAsync(
                [new NeedProxyAuthTooRoute()],
                new DifferentialResponderState(),
                "GET /need-proxy-auth-too HTTP/1.1\r\n\r\nGET /need-proxy-auth-too HTTP/1.1\r\n\r\n"));
    }
}
