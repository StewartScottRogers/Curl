using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cd-nofile</c>.</summary>
[TestClass]
public sealed class ContentDispositionInlineRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersInlineWithNoFileNameAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: inline\r\n"
            + "Content-Length: 3\r\n\r\ncd\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionInlineRoute(), "/cd-nofile", "/cd-nofile", "/cd-nofilex"));
    }
}
