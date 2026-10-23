using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/hdr-count</c>.</summary>
[TestClass]
public sealed class HeaderCountRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersFiveThousandHeadersAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + string.Concat(Enumerable.Range(0, 5000).Select(index => $"X-{index}: 1\r\n"))
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new HeaderCountRoute(), "/hdr-count", "/hdr-count", "/hdr-countx"));
    }
}
