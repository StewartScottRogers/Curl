using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/loc-scheme-rel</c>.</summary>
[TestClass]
public sealed class LocationSchemeRelativeRouteTests
{
    [TestMethod]
    public async Task RespondAsync_LocationSchemeRelativePath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: //127.0.0.1:18080/echo\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new LocationSchemeRelativeRoute(), "/loc-scheme-rel"));
    }
}
