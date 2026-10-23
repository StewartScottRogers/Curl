using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/post-303</c>.</summary>
[TestClass]
public sealed class Post303RouteTests
{
    [TestMethod]
    public async Task RespondAsync_Post303Path_Answers303AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 303 See Other\r\n{RedirectRouteRequests.Date}Location: /echo\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new Post303Route(), "/post-303"));
    }
}
