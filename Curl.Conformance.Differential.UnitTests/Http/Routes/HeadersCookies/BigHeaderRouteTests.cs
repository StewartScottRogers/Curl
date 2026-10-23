using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/bighdr</c>.</summary>
[TestClass]
public sealed class BigHeaderRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersOneTwoHundredThousandByteHeaderAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "X-Big: " + new string('v', 200000) + "\r\n"
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new BigHeaderRoute(), "/bighdr", "/bighdr", "/bighdrx"));
    }
}
