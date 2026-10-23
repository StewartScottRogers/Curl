using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/rother</c>.</summary>
[TestClass]
public sealed class RedirectOtherHostRouteTests
{
    [TestMethod]
    public async Task RespondAsync_RedirectOtherHostPath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: http://localhost:18080/echo\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new RedirectOtherHostRoute(), "/rother"));
    }
}
