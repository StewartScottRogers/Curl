using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/refresh</c>.</summary>
[TestClass]
public sealed class RefreshRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersRefreshToEchoAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Refresh: 0; url=/echo\r\n"
            + "Content-Length: 8\r\n\r\nrefresh\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new RefreshRoute(), "/refresh", "/refresh", "/refreshx"));
    }
}
