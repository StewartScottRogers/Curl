using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/hugehdr</c>.</summary>
[TestClass]
public sealed class HugeHeaderRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersOneFourHundredThousandByteHeaderAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "X-Big: " + new string('v', 400000) + "\r\n"
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new HugeHeaderRoute(), "/hugehdr", "/hugehdr", "/hugehdrx"));
    }
}
