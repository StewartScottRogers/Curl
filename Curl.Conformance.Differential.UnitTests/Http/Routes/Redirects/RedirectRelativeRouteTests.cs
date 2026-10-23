using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/rrel</c>.</summary>
[TestClass]
public sealed class RedirectRelativeRouteTests
{
    [TestMethod]
    public async Task RespondAsync_RedirectRelativePath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: ../echo?x=1\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new RedirectRelativeRoute(), "/rrel"));
    }
}
