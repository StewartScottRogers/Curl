using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/proxyauth</c>.</summary>
[TestClass]
public sealed class ProxyAuthRouteTests
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
                [new ProxyAuthRoute()],
                new DifferentialResponderState(),
                "GET http://h/proxyauth HTTP/1.1\r\n\r\nGET /proxyauth HTTP/1.1\r\nProxy-Authorization: Basic dTpw\r\n\r\n"));
    }
}
