using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/loc-utf8</c>.</summary>
[TestClass]
public sealed class LocationUtf8RouteTests
{
    [TestMethod]
    public async Task RespondAsync_LocationUtf8Path_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: /echo?q=\u00C3\u0083\u00C2\u00A9\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new LocationUtf8Route(), "/loc-utf8"));
    }
}
