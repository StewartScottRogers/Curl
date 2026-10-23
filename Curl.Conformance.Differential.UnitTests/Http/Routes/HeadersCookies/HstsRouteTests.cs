using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/hsts</c>.</summary>
[TestClass]
public sealed class HstsRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersOneHourHstsAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Strict-Transport-Security: max-age=3600\r\n"
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new HstsRoute(), "/hsts", "/hsts", "/hstsx"));
    }
}
