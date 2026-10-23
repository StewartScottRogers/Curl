using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/auth-redir</c>.</summary>
[TestClass]
public sealed class AuthRedirectRouteTests
{
    [TestMethod]
    public async Task RespondAsync_AuthRedirectPath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: /needauth\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new AuthRedirectRoute(), "/auth-redir"));
    }
}
