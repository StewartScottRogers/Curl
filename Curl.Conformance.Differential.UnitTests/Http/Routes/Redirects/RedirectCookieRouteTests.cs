using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/redir-cookie</c>.</summary>
[TestClass]
public sealed class RedirectCookieRouteTests
{
    [TestMethod]
    public async Task RespondAsync_RedirectCookiePath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: /echo\r\nSet-Cookie: rc=1; Path=/\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new RedirectCookieRoute(), "/redir-cookie"));
    }
}
