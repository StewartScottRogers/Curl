using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/loc-file</c>.</summary>
[TestClass]
public sealed class LocationFileRouteTests
{
    [TestMethod]
    public async Task RespondAsync_LocationFilePath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: file:///etc/passwd\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new LocationFileRoute(), "/loc-file"));
    }
}
