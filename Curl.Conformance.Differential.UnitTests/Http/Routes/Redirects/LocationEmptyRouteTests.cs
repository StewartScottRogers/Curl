using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/loc-empty</c>.</summary>
[TestClass]
public sealed class LocationEmptyRouteTests
{
    [TestMethod]
    public async Task RespondAsync_LocationEmptyPath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: \r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new LocationEmptyRoute(), "/loc-empty"));
    }
}
