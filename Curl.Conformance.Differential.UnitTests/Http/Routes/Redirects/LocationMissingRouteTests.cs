using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/loc-missing</c>.</summary>
[TestClass]
public sealed class LocationMissingRouteTests
{
    [TestMethod]
    public async Task RespondAsync_LocationMissingPath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Content-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new LocationMissingRoute(), "/loc-missing"));
    }
}
