using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/loc-307-body</c>.</summary>
[TestClass]
public sealed class Location307BodyRouteTests
{
    [TestMethod]
    public async Task RespondAsync_Location307BodyPath_Answers307AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 307 Temporary Redirect\r\n{RedirectRouteRequests.Date}Location: /echo\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new Location307BodyRoute(), "/loc-307-body"));
    }
}
