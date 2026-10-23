using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/rabs</c>.</summary>
[TestClass]
public sealed class RedirectAbsoluteRouteTests
{
    [TestMethod]
    public async Task RespondAsync_RedirectAbsolutePath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: http://127.0.0.1:18080/echo#frag\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new RedirectAbsoluteRoute(), "/rabs"));
    }
}
