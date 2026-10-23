using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/rftp</c>.</summary>
[TestClass]
public sealed class RedirectFtpRouteTests
{
    [TestMethod]
    public async Task RespondAsync_RedirectFtpPath_Answers302AndKeepsTheConnection()
    {
        const string Response = $"HTTP/1.1 302 Found\r\n{RedirectRouteRequests.Date}Location: ftp://127.0.0.1:1/x\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(Response + Response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new RedirectFtpRoute(), "/rftp"));
    }
}
