using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/sethsts</c>.</summary>
[TestClass]
public sealed class SetHstsRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersOneYearHstsWithSubdomainsAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Strict-Transport-Security: max-age=31536000; includeSubDomains\r\n"
            + "Content-Length: 2\r\n\r\nh\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new SetHstsRoute(), "/sethsts", "/sethsts", "/sethstsx"));
    }
}
