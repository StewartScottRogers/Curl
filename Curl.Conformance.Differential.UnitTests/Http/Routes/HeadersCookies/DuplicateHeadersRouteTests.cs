using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/dupheaders</c>.</summary>
[TestClass]
public sealed class DuplicateHeadersRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersRepeatedPaddedAndEmptyHeadersAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "X-A: 1\r\nX-A: 2\r\nX-B:   spaced  \r\nX-Empty: \r\n"
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new DuplicateHeadersRoute(), "/dupheaders", "/dupheaders", "/dupheadersx"));
    }
}
