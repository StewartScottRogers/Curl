using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/loc-dotdot</c>.</summary>
[TestClass]
public sealed class LocationDotDotRouteTests
{
    [TestMethod]
    public async Task RespondAsync_LocationDotDotPath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: ../../x/./y\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new LocationDotDotRoute(), "/loc-dotdot"));
    }
}
